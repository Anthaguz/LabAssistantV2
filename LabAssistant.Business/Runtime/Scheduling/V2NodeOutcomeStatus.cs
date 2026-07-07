namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// Terminal status of a single plan node after a scheduler run.
/// </summary>
public enum V2NodeOutcomeStatus
{
    /// <summary>The node executor completed successfully (for gate nodes, the probe passed).</summary>
    Completed = 0,

    /// <summary>The node executor failed. The first such node flips the run to draining.</summary>
    Failed = 1,

    /// <summary>The node was admitted but observed cancellation before completing.</summary>
    Cancelled = 2,

    /// <summary>The node was never admitted because the run began draining first.</summary>
    Skipped = 3
}
