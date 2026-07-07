using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// Executes and, if needed, undoes the work for a single <see cref="V2PlanNodeKind"/>.
/// This is the only place node-kind-specific behavior lives; the scheduler itself knows nothing about what a node does.
/// </summary>
public interface IV2NodeExecutor
{
    /// <summary>The node kind this executor handles.</summary>
    V2PlanNodeKind Kind { get; }

    /// <summary>
    /// Performs the node's work (provision, run guest script, configure switch, ...).
    /// For gate kinds, this method IS the probe: it polls until the readiness condition holds or its retry budget is
    /// exhausted, honoring <paramref name="cancellationToken"/>. Returning without throwing means the gate is open.
    /// </summary>
    Task ExecuteAsync(V2NodeExecutionContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Undoes side effects this node created, if any. Called during failure/cancel unwind in reverse completion order.
    /// Most guest nodes are no-ops here because whole-VM deletion covers them; switch and trust nodes have real undo.
    /// Implementations must be idempotent.
    /// </summary>
    Task CleanupAsync(V2NodeExecutionContext context, CancellationToken cancellationToken);
}
