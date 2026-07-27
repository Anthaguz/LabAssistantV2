namespace LabAssistant.UITesting.Runner;

/// <summary>Severity of a recorded finding.</summary>
public enum FindingSeverity
{
    Info,
    Warning,
    Error,
    Crash
}

/// <summary>A single observed problem, with optional evidence paths.</summary>
public sealed class Finding
{
    public string Scenario { get; init; } = string.Empty;
    public string Step { get; init; } = string.Empty;
    public FindingSeverity Severity { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string? ScreenshotFile { get; init; }

    /// <summary>
    /// Relative path (from the run directory) to a JSONL slice of the app's structured
    /// event log captured for this failure, or null when none was captured. This is the
    /// app's own account of the failing operation - the ordered operationId / stepKey /
    /// result / error stream - and is the primary evidence for triaging a deploy failure.
    /// </summary>
    public string? AppLogFile { get; init; }

    public string? ExceptionType { get; init; }
    public string? StackTrace { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
