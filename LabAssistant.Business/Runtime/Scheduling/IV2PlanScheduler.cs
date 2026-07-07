using LabAssistant.Models.Templates;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// Executes a V2 deployment plan by consuming its dependency graph directly: it maintains a ready-set, dispatches
/// ready nodes concurrently under per-workload-class and global caps, and on failure or cancellation drains and runs
/// cleanup in reverse completion order. It is topology-agnostic; node behavior lives entirely in executors.
/// </summary>
public interface IV2PlanScheduler
{
    /// <summary>
    /// Runs <paramref name="plan"/> to completion (or to a drained terminal state) and returns per-node outcomes.
    /// </summary>
    /// <param name="plan">The planner-built graph of nodes and dependencies.</param>
    /// <param name="executors">Resolves the executor for each node kind. Must cover every kind in the plan.</param>
    /// <param name="options">Concurrency and gate-retry policy.</param>
    /// <param name="log">Structured logger for scheduler-level events.</param>
    /// <param name="cancellationToken">Cancels the run; triggers draining and cleanup.</param>
    Task<V2SchedulerRunResult> ExecuteAsync(
        V2PlanBuildResult plan,
        IV2NodeExecutorRegistry executors,
        V2SchedulerOptions options,
        IStructuredLogger log,
        CancellationToken cancellationToken);
}
