namespace LabAssistant.Services.Logging;

public interface IStructuredLogViewerService
{
    Task<StructuredLogViewerLoadResult> LoadAsync(StructuredLogViewerFilter filter, CancellationToken cancellationToken = default);

    string GetStructuredLogFilePath();
}

public sealed class StructuredLogViewerFilter
{
    public string? OperationId { get; init; }

    public string? Level { get; init; }

    public string? Event { get; init; }

    public string? TextSearch { get; init; }

    public DateTimeOffset? StartUtc { get; init; }

    public DateTimeOffset? EndUtc { get; init; }
}

public sealed class StructuredLogViewerLoadResult
{
    public IReadOnlyList<StructuredLogViewerEntry> Entries { get; init; } = Array.Empty<StructuredLogViewerEntry>();

    public int ParseErrorCount { get; init; }

    public int TotalLineCount { get; init; }
}

public sealed class StructuredLogViewerEntry
{
    public int LineNumber { get; init; }

    public DateTimeOffset? TimestampUtc { get; init; }

    public string TimestampText { get; init; } = string.Empty;

    public string Level { get; init; } = string.Empty;

    public string Event { get; init; } = string.Empty;

    public string OperationId { get; init; } = string.Empty;

    public string Result { get; init; } = string.Empty;

    public string ContextJson { get; init; } = "{}";

    public string RawJsonLine { get; init; } = string.Empty;
}
