using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// Per-node terminal record produced by <see cref="IV2PlanScheduler"/>.
/// </summary>
/// <param name="NodeId">Stable plan node identifier.</param>
/// <param name="Kind">The node kind that was scheduled.</param>
/// <param name="Status">Terminal status for the node.</param>
/// <param name="Error">Failure detail when <paramref name="Status"/> is <see cref="V2NodeOutcomeStatus.Failed"/>.</param>
public sealed record V2NodeOutcome(
    string NodeId,
    V2PlanNodeKind Kind,
    V2NodeOutcomeStatus Status,
    string? Error = null);
