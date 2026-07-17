using LabAssistant.Services.Diagnostics;

namespace LabAssistant.Services.Logging;

public interface IStructuredLogViewerService
{
    Task<StructuredLogViewerLoadResult> LoadAsync(StructuredLogViewerFilter filter, CancellationToken cancellationToken = default);

    string GetStructuredLogFilePath();
}

public sealed class StructuredLogViewerFilter
{
    public string? OperationId { get; init; }

    public string? Event { get; init; }

    public string? TextSearch { get; init; }

    public DateTimeOffset? StartUtc { get; init; }

    public DateTimeOffset? EndUtc { get; init; }

    /// <summary>
    /// Facility bytes to include. When null or empty, every facility is shown. An entry with no
    /// resolved facility byte (a legacy, code-less event) is only shown when this set is empty.
    /// </summary>
    public IReadOnlyCollection<byte>? Facilities { get; init; }

    /// <summary>
    /// The lowest projected level to show, on the debug &lt; info &lt; warn &lt; error ladder. When null,
    /// every level is shown. Success-severity events project to <c>info</c>, so they survive an
    /// <c>info</c>-or-lower threshold but are hidden by a <c>warn</c>/<c>error</c> threshold.
    /// </summary>
    public StatusLevelRank? MinimumLevel { get; init; }
}

/// <summary>
/// The four projected log levels, ordered from least to most severe, used by the viewer's minimum
/// level filter. This is the coarse projection of the richer <see cref="StatusCodeSeverity"/> nibble.
/// </summary>
public enum StatusLevelRank
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3
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

    /// <summary>The canonical code as emitted (<c>0xSSFFOOCC</c>), or empty for a legacy code-less event.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>The parsed numeric code, or null when the entry carries no code.</summary>
    public uint? CodeValue { get; init; }

    /// <summary>The facility byte decomposed from the code, or null when the entry carries no code.</summary>
    public byte? FacilityByte { get; init; }

    /// <summary>The dotted facility name (for example <c>hyperv</c>), resolved from the code when available.</summary>
    public string Facility { get; init; } = string.Empty;

    /// <summary>The operation name within the facility, resolved from the code when available.</summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>The lifecycle phase (start/progress/end/atomic).</summary>
    public string Phase { get; init; } = string.Empty;

    /// <summary>The severity name decomposed from the code's high nibble (for example <c>Error</c>).</summary>
    public string Severity { get; init; } = string.Empty;

    /// <summary>The set flag names, or empty.</summary>
    public IReadOnlyList<string> Flags { get; init; } = Array.Empty<string>();

    /// <summary>The emitting managed thread id, when present.</summary>
    public int? Thread { get; init; }

    /// <summary>The emitting call site (<c>file:line</c>), when present.</summary>
    public string Callsite { get; init; } = string.Empty;

    /// <summary>The status/outcome byte decomposed from the code (<c>0x00</c> = OK), or empty when code-less.</summary>
    public string StatusByteText { get; init; } = string.Empty;

    /// <summary>A friendly title for the code, resolved from the registry when the code is registered.</summary>
    public string? Title { get; init; }

    /// <summary>The plain-language message for the code, resolved from the registry when registered.</summary>
    public string? Message { get; init; }

    /// <summary>Remediation guidance for the code, resolved from the registry when present.</summary>
    public string? Remediation { get; init; }

    /// <summary>The projected level rank, used by the minimum-level filter.</summary>
    public StatusLevelRank LevelRank { get; init; } = StatusLevelRank.Info;
}
