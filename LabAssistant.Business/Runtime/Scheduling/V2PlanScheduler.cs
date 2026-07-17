using LabAssistant.Models.Templates;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// Generic, topology-agnostic ready-set scheduler. It drives a plan graph to completion by admitting ready nodes under
/// per-workload-class and global concurrency caps, and on the first failure or on cancellation it drains and runs
/// cleanup in reverse completion order so no partially-created resources are orphaned.
/// </summary>
public sealed class V2PlanScheduler : IV2PlanScheduler
{
    private const string SchedulerOperation = "V2PlanScheduler";

    /// <inheritdoc />
    public async Task<V2SchedulerRunResult> ExecuteAsync(
        V2PlanBuildResult plan,
        IV2NodeExecutorRegistry executors,
        V2SchedulerOptions options,
        IStructuredLogger log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(executors);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(log);

        var graph = V2PlanGraph.Build(plan);
        graph.Validate(executors);

        var run = new SchedulerRun(graph, plan, executors, options, log, cancellationToken);
        return await run.RunAsync().ConfigureAwait(false);
    }

    /// <summary>Mutable per-invocation state, isolated so the scheduler instance itself stays stateless.</summary>
    private sealed class SchedulerRun
    {
        private readonly V2PlanGraph _graph;
        private readonly V2PlanBuildResult _plan;
        private readonly IV2NodeExecutorRegistry _executors;
        private readonly IStructuredLogger _log;
        private readonly CancellationToken _cancellationToken;

        private readonly Dictionary<string, int> _remainingInDegree;
        private readonly Dictionary<string, V2NodeOutcomeStatus> _statuses = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string?> _errors = new(StringComparer.Ordinal);
        private readonly List<string> _admissionOrder = new();
        private readonly List<string> _finishedOrder = new();
        private readonly List<string> _readyIds = new();

        private readonly SemaphoreSlim _globalSemaphore;
        private readonly IReadOnlyDictionary<V2WorkloadClass, SemaphoreSlim> _classSemaphores;

        private bool _draining;
        private bool _cancelled;
        private string? _failingNodeId;
        private string? _failureError;

        public SchedulerRun(
            V2PlanGraph graph,
            V2PlanBuildResult plan,
            IV2NodeExecutorRegistry executors,
            V2SchedulerOptions options,
            IStructuredLogger log,
            CancellationToken cancellationToken)
        {
            _graph = graph;
            _plan = plan;
            _executors = executors;
            _log = log;
            _cancellationToken = cancellationToken;

            _remainingInDegree = graph.Nodes.ToDictionary(
                node => node.NodeId,
                node => graph.InDegree(node.NodeId),
                StringComparer.Ordinal);

            var globalCap = options.GlobalMaxConcurrency > 0 ? options.GlobalMaxConcurrency : int.MaxValue;
            _globalSemaphore = new SemaphoreSlim(globalCap, globalCap);
            _classSemaphores = BuildClassSemaphores(options.MaxConcurrencyByClass);

            foreach (var node in graph.Nodes.Where(node => _remainingInDegree[node.NodeId] == 0))
            {
                _readyIds.Add(node.NodeId);
            }
        }

        public async Task<V2SchedulerRunResult> RunAsync()
        {
            _log.Log(
                LaStatus.DeployOrchestration_SchedulerStarted,
                SchedulerOperation,
                "started",
                new Dictionary<string, object?>
                {
                    ["nodeCount"] = _graph.Nodes.Count
                });

            var inFlight = new Dictionary<Task<NodeResult>, string>();

            try
            {
                while (true)
                {
                    ObserveCancellation();
                    AdmitReadyNodes(inFlight);

                    if (inFlight.Count == 0)
                    {
                        if (_readyIds.Count == 0 || _draining)
                        {
                            break;
                        }

                        // Ready nodes exist but none could be admitted and nothing is running to free capacity.
                        // With sane options this cannot happen; guard against a misconfigured zero-capacity class.
                        throw new InvalidOperationException(
                            "V2 scheduler stalled: ready nodes could not be admitted under the configured concurrency caps.");
                    }

                    var completed = await Task.WhenAny(inFlight.Keys).ConfigureAwait(false);
                    var nodeId = inFlight[completed];
                    inFlight.Remove(completed);
                    ReleaseSlots(nodeId);
                    HandleNodeResult(await completed.ConfigureAwait(false));
                }
            }
            finally
            {
                FinalizeSkippedNodes();
                await RunCleanupIfNeededAsync().ConfigureAwait(false);
            }

            return BuildResult();
        }

        private void AdmitReadyNodes(Dictionary<Task<NodeResult>, string> inFlight)
        {
            if (_draining || _readyIds.Count == 0)
            {
                return;
            }

            _readyIds.Sort(CompareReadyPriority);

            var index = 0;
            while (index < _readyIds.Count)
            {
                var nodeId = _readyIds[index];
                var node = _graph.NodeFor(nodeId);
                if (!TryAcquireSlots(node.WorkloadClass))
                {
                    index++;
                    continue;
                }

                _readyIds.RemoveAt(index);
                _admissionOrder.Add(nodeId);
                inFlight.Add(RunNodeAsync(node), nodeId);
            }
        }

        private int CompareReadyPriority(string leftId, string rightId)
        {
            var left = _graph.NodeFor(leftId);
            var right = _graph.NodeFor(rightId);
            var byWave = left.WaveHint.CompareTo(right.WaveHint);
            return byWave != 0 ? byWave : string.CompareOrdinal(leftId, rightId);
        }

        private async Task<NodeResult> RunNodeAsync(V2PlanNode node)
        {
            // Hop off the coordinator thread so executors run concurrently.
            await Task.Yield();

            var executor = _executors.Resolve(node.Kind);
            var context = new V2NodeExecutionContext(node, _plan, _log);
            try
            {
                await executor.ExecuteAsync(context, _cancellationToken).ConfigureAwait(false);
                return new NodeResult(node.NodeId, V2NodeOutcomeStatus.Completed, null);
            }
            catch (OperationCanceledException)
            {
                return new NodeResult(node.NodeId, V2NodeOutcomeStatus.Cancelled, null);
            }
            catch (Exception ex)
            {
                return new NodeResult(node.NodeId, V2NodeOutcomeStatus.Failed, ex.Message);
            }
        }

        private void HandleNodeResult(NodeResult result)
        {
            _finishedOrder.Add(result.NodeId);
            _statuses[result.NodeId] = result.Status;
            _errors[result.NodeId] = result.Error;

            switch (result.Status)
            {
                case V2NodeOutcomeStatus.Completed:
                    ReleaseSuccessors(result.NodeId);
                    break;

                case V2NodeOutcomeStatus.Failed:
                    _draining = true;
                    _failingNodeId ??= result.NodeId;
                    _failureError ??= result.Error;
                    break;

                case V2NodeOutcomeStatus.Cancelled:
                    _draining = true;
                    _cancelled = true;
                    break;
            }
        }

        private void ReleaseSuccessors(string nodeId)
        {
            if (_draining)
            {
                return;
            }

            foreach (var successor in _graph.Successors(nodeId))
            {
                if (--_remainingInDegree[successor] == 0)
                {
                    _readyIds.Add(successor);
                }
            }
        }

        private void ObserveCancellation()
        {
            if (_cancellationToken.IsCancellationRequested && !_draining)
            {
                _draining = true;
                _cancelled = true;
            }
        }

        private void FinalizeSkippedNodes()
        {
            foreach (var node in _graph.Nodes)
            {
                if (!_statuses.ContainsKey(node.NodeId))
                {
                    _statuses[node.NodeId] = V2NodeOutcomeStatus.Skipped;
                    _errors[node.NodeId] = null;
                }
            }
        }

        private async Task RunCleanupIfNeededAsync()
        {
            var succeeded = !_cancelled && _failingNodeId is null;
            if (succeeded)
            {
                return;
            }

            _log.Log(
                LaStatus.DeployOrchestration_SchedulerCleanupStarted,
                SchedulerOperation,
                _cancelled ? "cancelled" : "failed",
                new Dictionary<string, object?>
                {
                    ["failingNodeId"] = _failingNodeId,
                    ["cleanupNodeCount"] = _finishedOrder.Count
                });

            // Reverse completion order: undo the most recently finished work first. Cleanup ignores the run's
            // cancellation token so it always runs to completion (repo invariant: no orphaned resources).
            for (var index = _finishedOrder.Count - 1; index >= 0; index--)
            {
                var nodeId = _finishedOrder[index];
                var node = _graph.NodeFor(nodeId);
                try
                {
                    var executor = _executors.Resolve(node.Kind);
                    var context = new V2NodeExecutionContext(node, _plan, _log);
                    await executor.CleanupAsync(context, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.Log(
                        LaStatus.DeployOrchestration_SchedulerCleanupNodeFailed,
                        SchedulerOperation,
                        "error",
                        new Dictionary<string, object?>
                        {
                            ["nodeId"] = nodeId,
                            ["kind"] = node.Kind.ToString(),
                            ["error"] = ex.Message
                        });
                }
            }
        }

        private V2SchedulerRunResult BuildResult()
        {
            var outcomes = _graph.Nodes
                .Select(node => new V2NodeOutcome(
                    node.NodeId,
                    node.Kind,
                    _statuses[node.NodeId],
                    _errors.TryGetValue(node.NodeId, out var error) ? error : null))
                .ToList();

            var success = outcomes.All(outcome => outcome.Status == V2NodeOutcomeStatus.Completed);

            var schedulerCode = success
                ? LaStatus.DeployOrchestration_SchedulerCompleted
                : _cancelled
                    ? LaStatus.DeployOrchestration_SchedulerCancelled
                    : LaStatus.DeployOrchestration_SchedulerFailed;

            _log.Log(
                schedulerCode,
                SchedulerOperation,
                success ? "success" : _cancelled ? "cancelled" : "failed",
                new Dictionary<string, object?>
                {
                    ["failingNodeId"] = _failingNodeId,
                    ["admittedCount"] = _admissionOrder.Count
                });

            return new V2SchedulerRunResult
            {
                Success = success,
                WasCancelled = _cancelled,
                FailingNodeId = _failingNodeId,
                FailureError = _failureError,
                AdmissionOrder = _admissionOrder,
                Outcomes = outcomes
            };
        }

        private bool TryAcquireSlots(V2WorkloadClass workloadClass)
        {
            if (!_globalSemaphore.Wait(0))
            {
                return false;
            }

            if (_classSemaphores.TryGetValue(workloadClass, out var classSemaphore) && !classSemaphore.Wait(0))
            {
                _globalSemaphore.Release();
                return false;
            }

            return true;
        }

        private void ReleaseSlots(string nodeId)
        {
            var workloadClass = _graph.NodeFor(nodeId).WorkloadClass;
            if (_classSemaphores.TryGetValue(workloadClass, out var classSemaphore))
            {
                classSemaphore.Release();
            }

            _globalSemaphore.Release();
        }

        private static IReadOnlyDictionary<V2WorkloadClass, SemaphoreSlim> BuildClassSemaphores(
            IReadOnlyDictionary<V2WorkloadClass, int> maxConcurrencyByClass)
        {
            var semaphores = new Dictionary<V2WorkloadClass, SemaphoreSlim>();
            if (maxConcurrencyByClass is null)
            {
                return semaphores;
            }

            foreach (var pair in maxConcurrencyByClass)
            {
                if (pair.Value > 0)
                {
                    semaphores[pair.Key] = new SemaphoreSlim(pair.Value, pair.Value);
                }
            }

            return semaphores;
        }

        private readonly record struct NodeResult(string NodeId, V2NodeOutcomeStatus Status, string? Error);
    }
}
