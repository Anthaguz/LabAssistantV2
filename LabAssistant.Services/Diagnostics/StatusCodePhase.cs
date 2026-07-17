namespace LabAssistant.Services.Diagnostics;

/// <summary>
/// Lifecycle phase of an event. Orthogonal to the status code and carried as its own field, so a
/// "start" row and its matching "end" row are distinct rows with a phase chip in the UI.
/// </summary>
public enum StatusCodePhase : byte
{
    /// <summary>The operation is beginning. Carries no outcome.</summary>
    Start = 0,

    /// <summary>An interim progress point during a long operation (for example a retry attempt).</summary>
    Progress = 1,

    /// <summary>The operation finished. Carries the real outcome in the status byte.</summary>
    End = 2,

    /// <summary>A single instantaneous event with no separate start/end.</summary>
    Atomic = 3
}
