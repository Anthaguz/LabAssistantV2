using LabAssistant.Models.Templates;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// Per-invocation data handed to an <see cref="IV2NodeExecutor"/> for a single node.
/// The engine is topology-agnostic: it carries only the node, the owning plan, and a logger.
/// Real executors capture their shared runtime environment (VM/trust/switch state, coordinators) via closure at
/// registration time, so it is intentionally not part of this context.
/// </summary>
public sealed class V2NodeExecutionContext
{
    /// <summary>Creates a node execution context.</summary>
    public V2NodeExecutionContext(V2PlanNode node, V2PlanBuildResult plan, IStructuredLogger log)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        Log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>The node being executed or cleaned up.</summary>
    public V2PlanNode Node { get; }

    /// <summary>The plan the node belongs to.</summary>
    public V2PlanBuildResult Plan { get; }

    /// <summary>Structured logger for the run.</summary>
    public IStructuredLogger Log { get; }
}
