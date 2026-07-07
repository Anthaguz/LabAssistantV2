using System.Collections.Concurrent;
using LabAssistant.Business.Runtime.Scheduling;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

/// <summary>
/// Engine-level tests for <see cref="V2PlanScheduler"/> using fabricated tiny graphs and recording executors.
/// These prove scheduling semantics (readiness, concurrency caps, draining, reverse cleanup, fail-closed) with no
/// Hyper-V and no dependency on the planner.
/// </summary>
public sealed class V2PlanSchedulerTests
{
    private static readonly IStructuredLogger Log = NullStructuredLogger.Instance;

    [Fact]
    public async Task ExecuteAsync_LinearChain_AdmitsNodesInDependencyOrder()
    {
        var plan = BuildPlan(
            nodes:
            [
                Node("a", V2PlanNodeKind.ProvisionVm),
                Node("b", V2PlanNodeKind.StartVm),
                Node("c", V2PlanNodeKind.GuestTransportReady)
            ],
            dependencies:
            [
                Edge("a", "b"),
                Edge("b", "c")
            ]);

        var recorder = new Recorder();
        var registry = RegistryFor(recorder, V2PlanNodeKind.ProvisionVm, V2PlanNodeKind.StartVm, V2PlanNodeKind.GuestTransportReady);

        var result = await new V2PlanScheduler().ExecuteAsync(plan, registry, Options(), Log, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new[] { "a", "b", "c" }, result.AdmissionOrder);
        Assert.All(result.Outcomes, outcome => Assert.Equal(V2NodeOutcomeStatus.Completed, outcome.Status));
    }

    [Fact]
    public async Task ExecuteAsync_PerWorkloadClassCap_LimitsConcurrentExecution()
    {
        const int nodeCount = 6;
        const int classCap = 2;

        var nodes = Enumerable.Range(0, nodeCount)
            .Select(i => Node($"n{i}", V2PlanNodeKind.ProvisionVm))
            .ToArray();
        var plan = BuildPlan(nodes, dependencies: []);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        var maxObserved = 0;
        var executor = new ProbeExecutor(V2PlanNodeKind.ProvisionVm, async (_, _) =>
        {
            var current = Interlocked.Increment(ref arrived);
            InterlockedMax(ref maxObserved, current);
            await gate.Task;
            Interlocked.Decrement(ref arrived);
        });
        var registry = new V2NodeExecutorRegistry(new[] { (IV2NodeExecutor)executor });

        var options = new V2SchedulerOptions(
            new Dictionary<V2WorkloadClass, int> { [V2WorkloadClass.HeavyHost] = classCap },
            GlobalMaxConcurrency: 100,
            GateRetryDelay: TimeSpan.Zero,
            GateMaxRetries: 0);

        var runTask = new V2PlanScheduler().ExecuteAsync(plan, registry, options, Log, CancellationToken.None);

        await WaitUntilAsync(() => Volatile.Read(ref arrived) == classCap);
        await Task.Delay(50);
        Assert.Equal(classCap, Volatile.Read(ref arrived));
        Assert.Equal(classCap, Volatile.Read(ref maxObserved));

        gate.SetResult();
        var result = await runTask;

        Assert.True(result.Success);
        Assert.Equal(classCap, Volatile.Read(ref maxObserved));
        Assert.Equal(nodeCount, result.AdmissionOrder.Count);
    }

    [Fact]
    public async Task ExecuteAsync_GlobalCap_LimitsConcurrentExecutionAcrossClasses()
    {
        var nodes = new[]
        {
            Node("h1", V2PlanNodeKind.ProvisionVm, V2WorkloadClass.HeavyHost),
            Node("g1", V2PlanNodeKind.PromoteFirstDomainController, V2WorkloadClass.HeavyGuest),
            Node("m1", V2PlanNodeKind.JoinDomain, V2WorkloadClass.MediumGuest)
        };
        var plan = BuildPlan(nodes, dependencies: []);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        var maxObserved = 0;

        async Task Body(V2NodeExecutionContext _, CancellationToken __)
        {
            var current = Interlocked.Increment(ref arrived);
            InterlockedMax(ref maxObserved, current);
            await gate.Task;
            Interlocked.Decrement(ref arrived);
        }

        var registry = new V2NodeExecutorRegistry(new IV2NodeExecutor[]
        {
            new ProbeExecutor(V2PlanNodeKind.ProvisionVm, Body),
            new ProbeExecutor(V2PlanNodeKind.PromoteFirstDomainController, Body),
            new ProbeExecutor(V2PlanNodeKind.JoinDomain, Body)
        });

        var options = new V2SchedulerOptions(
            new Dictionary<V2WorkloadClass, int>(),
            GlobalMaxConcurrency: 2,
            GateRetryDelay: TimeSpan.Zero,
            GateMaxRetries: 0);

        var runTask = new V2PlanScheduler().ExecuteAsync(plan, registry, options, Log, CancellationToken.None);

        await WaitUntilAsync(() => Volatile.Read(ref arrived) == 2);
        await Task.Delay(50);
        Assert.Equal(2, Volatile.Read(ref maxObserved));

        gate.SetResult();
        var result = await runTask;

        Assert.True(result.Success);
        Assert.Equal(2, Volatile.Read(ref maxObserved));
    }

    [Fact]
    public async Task ExecuteAsync_NodeFailure_DrainsAndCleansUpInReverseCompletionOrder()
    {
        // a -> b -> c ; d is independent. b fails after a completes; c must never be admitted.
        var plan = BuildPlan(
            nodes:
            [
                Node("a", V2PlanNodeKind.ProvisionVm),
                Node("b", V2PlanNodeKind.StartVm),
                Node("c", V2PlanNodeKind.GuestTransportReady),
                Node("d", V2PlanNodeKind.EnableGuestServices)
            ],
            dependencies:
            [
                Edge("a", "b"),
                Edge("b", "c")
            ]);

        var recorder = new Recorder();
        var registry = new V2NodeExecutorRegistry(new IV2NodeExecutor[]
        {
            new ProbeExecutor(V2PlanNodeKind.ProvisionVm, recorder.RunSuccess("a"), recorder.Cleanup("a")),
            new ProbeExecutor(V2PlanNodeKind.StartVm, recorder.RunFail("b"), recorder.Cleanup("b")),
            new ProbeExecutor(V2PlanNodeKind.GuestTransportReady, recorder.RunSuccess("c"), recorder.Cleanup("c")),
            new ProbeExecutor(V2PlanNodeKind.EnableGuestServices, recorder.RunSuccess("d"), recorder.Cleanup("d"))
        });

        var result = await new V2PlanScheduler().ExecuteAsync(plan, registry, Options(), Log, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("b", result.FailingNodeId);
        Assert.Equal(V2NodeOutcomeStatus.Skipped, OutcomeFor(result, "c"));
        Assert.Equal(V2NodeOutcomeStatus.Failed, OutcomeFor(result, "b"));
        Assert.Equal(V2NodeOutcomeStatus.Completed, OutcomeFor(result, "a"));

        // c never ran, so it is never cleaned. Cleanup runs in reverse completion order over admitted nodes only.
        Assert.DoesNotContain("c", recorder.Cleanups);
        Assert.Contains("a", recorder.Cleanups);
        Assert.Contains("b", recorder.Cleanups);
        var aIndex = recorder.Cleanups.IndexOf("a");
        var bIndex = recorder.Cleanups.IndexOf("b");
        Assert.True(bIndex < aIndex, "cleanup must run in reverse completion order (b before a)");
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_MarksCancelledAndRunsCleanup()
    {
        var plan = BuildPlan(
            nodes: [Node("a", V2PlanNodeKind.ProvisionVm)],
            dependencies: []);

        using var cts = new CancellationTokenSource();
        var recorder = new Recorder();
        var executor = new ProbeExecutor(
            V2PlanNodeKind.ProvisionVm,
            async (_, ct) =>
            {
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, ct);
            },
            recorder.Cleanup("a"));
        var registry = new V2NodeExecutorRegistry(new[] { (IV2NodeExecutor)executor });

        var result = await new V2PlanScheduler().ExecuteAsync(plan, registry, Options(), Log, cts.Token);

        Assert.False(result.Success);
        Assert.True(result.WasCancelled);
        Assert.Equal(V2NodeOutcomeStatus.Cancelled, OutcomeFor(result, "a"));
        Assert.Contains("a", recorder.Cleanups);
    }

    [Fact]
    public async Task ExecuteAsync_UnregisteredKind_FailsClosedAtValidation()
    {
        var plan = BuildPlan(
            nodes:
            [
                Node("a", V2PlanNodeKind.ProvisionVm),
                Node("cap", V2PlanNodeKind.ApplyCapabilityRole)
            ],
            dependencies: [Edge("a", "cap")]);

        var recorder = new Recorder();
        var registry = RegistryFor(recorder, V2PlanNodeKind.ProvisionVm);

        var ex = await Assert.ThrowsAsync<V2SchedulerValidationException>(() =>
            new V2PlanScheduler().ExecuteAsync(plan, registry, Options(), Log, CancellationToken.None));

        Assert.Contains(ex.Errors, error => error.Contains("ApplyCapabilityRole", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_Cycle_FailsClosedAtValidation()
    {
        var plan = BuildPlan(
            nodes:
            [
                Node("a", V2PlanNodeKind.ProvisionVm),
                Node("b", V2PlanNodeKind.StartVm)
            ],
            dependencies:
            [
                Edge("a", "b"),
                Edge("b", "a")
            ]);

        var recorder = new Recorder();
        var registry = RegistryFor(recorder, V2PlanNodeKind.ProvisionVm, V2PlanNodeKind.StartVm);

        var ex = await Assert.ThrowsAsync<V2SchedulerValidationException>(() =>
            new V2PlanScheduler().ExecuteAsync(plan, registry, Options(), Log, CancellationToken.None));

        Assert.Contains(ex.Errors, error => error.Contains("cycle", StringComparison.OrdinalIgnoreCase));
    }

    private static V2NodeOutcomeStatus OutcomeFor(V2SchedulerRunResult result, string nodeId) =>
        result.Outcomes.Single(outcome => outcome.NodeId == nodeId).Status;

    private static V2SchedulerOptions Options() => new(
        new Dictionary<V2WorkloadClass, int>(),
        GlobalMaxConcurrency: 8,
        GateRetryDelay: TimeSpan.Zero,
        GateMaxRetries: 0);

    private static V2NodeExecutorRegistry RegistryFor(Recorder recorder, params V2PlanNodeKind[] kinds) =>
        new(kinds.Select(kind => (IV2NodeExecutor)new ProbeExecutor(
            kind,
            recorder.RunSuccessByNode(),
            recorder.CleanupByNode())));

    private static V2PlanBuildResult BuildPlan(
        IReadOnlyList<V2PlanNode> nodes,
        IReadOnlyList<V2PlanDependency> dependencies) =>
        new()
        {
            Success = true,
            Nodes = nodes,
            Dependencies = dependencies
        };

    private static V2PlanNode Node(
        string id,
        V2PlanNodeKind kind,
        V2WorkloadClass workloadClass = V2WorkloadClass.HeavyHost,
        int waveHint = 0) =>
        new()
        {
            NodeId = id,
            VmId = id,
            VmName = id,
            Kind = kind,
            DisplayName = id,
            WorkloadClass = workloadClass,
            WaveHint = waveHint
        };

    private static V2PlanDependency Edge(string from, string to) => new()
    {
        FromNodeId = from,
        ToNodeId = to,
        ReasonCode = V2PlanDependencyReasonCode.VmLifecycle,
        IsBlockingGate = true
    };

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)))
        {
            if (Interlocked.CompareExchange(ref target, value, current) == current)
            {
                return;
            }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met within the timeout.");
    }

    /// <summary>An <see cref="IV2NodeExecutor"/> whose execute/cleanup bodies are supplied as delegates.</summary>
    private sealed class ProbeExecutor : IV2NodeExecutor
    {
        private readonly Func<V2NodeExecutionContext, CancellationToken, Task> _execute;
        private readonly Func<V2NodeExecutionContext, CancellationToken, Task>? _cleanup;

        public ProbeExecutor(
            V2PlanNodeKind kind,
            Func<V2NodeExecutionContext, CancellationToken, Task> execute,
            Func<V2NodeExecutionContext, CancellationToken, Task>? cleanup = null)
        {
            Kind = kind;
            _execute = execute;
            _cleanup = cleanup;
        }

        public V2PlanNodeKind Kind { get; }

        public Task ExecuteAsync(V2NodeExecutionContext context, CancellationToken cancellationToken)
            => _execute(context, cancellationToken);

        public Task CleanupAsync(V2NodeExecutionContext context, CancellationToken cancellationToken)
            => _cleanup?.Invoke(context, cancellationToken) ?? Task.CompletedTask;
    }

    /// <summary>Thread-safe recorder of execution and cleanup, keyed by node id.</summary>
    private sealed class Recorder
    {
        private readonly ConcurrentQueue<string> _cleanups = new();

        public List<string> Cleanups => _cleanups.ToList();

        public Func<V2NodeExecutionContext, CancellationToken, Task> RunSuccess(string _) =>
            (_, _) => Task.CompletedTask;

        public Func<V2NodeExecutionContext, CancellationToken, Task> RunFail(string nodeId) =>
            (_, _) => throw new InvalidOperationException($"Node '{nodeId}' failed by design.");

        public Func<V2NodeExecutionContext, CancellationToken, Task> Cleanup(string nodeId) =>
            (_, _) =>
            {
                _cleanups.Enqueue(nodeId);
                return Task.CompletedTask;
            };

        public Func<V2NodeExecutionContext, CancellationToken, Task> RunSuccessByNode() =>
            (_, _) => Task.CompletedTask;

        public Func<V2NodeExecutionContext, CancellationToken, Task> CleanupByNode() =>
            (context, _) =>
            {
                _cleanups.Enqueue(context.Node.NodeId);
                return Task.CompletedTask;
            };
    }
}
