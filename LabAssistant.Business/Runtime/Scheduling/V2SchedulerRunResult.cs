namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// Aggregate result of a single <see cref="IV2PlanScheduler"/> run.
/// This is the scheduler-native result; the runtime adapter maps it to the deployment result shape.
/// </summary>
public sealed class V2SchedulerRunResult
{
    /// <summary>True only when every node reached <see cref="V2NodeOutcomeStatus.Completed"/>.</summary>
    public bool Success { get; init; }

    /// <summary>True when the run drained because of cancellation rather than a node failure.</summary>
    public bool WasCancelled { get; init; }

    /// <summary>The first node that failed, if any.</summary>
    public string? FailingNodeId { get; init; }

    /// <summary>Failure detail for <see cref="FailingNodeId"/>, if any.</summary>
    public string? FailureError { get; init; }

    /// <summary>Node identifiers in the order they were admitted for execution.</summary>
    public IReadOnlyList<string> AdmissionOrder { get; init; } = Array.Empty<string>();

    /// <summary>Terminal outcome for every node in the plan.</summary>
    public IReadOnlyList<V2NodeOutcome> Outcomes { get; init; } = Array.Empty<V2NodeOutcome>();
}
