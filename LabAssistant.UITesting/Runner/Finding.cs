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
    public string? ExceptionType { get; init; }
    public string? StackTrace { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
