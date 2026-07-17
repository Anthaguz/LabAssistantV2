using System.Text.Json;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.Diagnostics;

namespace LabAssistant.Services.Logging;

public sealed class StructuredLogViewerService : IStructuredLogViewerService
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IAppPaths _appPaths;

    public StructuredLogViewerService(IAppSettingsStore settingsStore, IAppPaths appPaths)
    {
        _settingsStore = settingsStore;
        _appPaths = appPaths;
    }

    public async Task<StructuredLogViewerLoadResult> LoadAsync(
        StructuredLogViewerFilter filter,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetStructuredLogFilePath();
        if (!File.Exists(filePath))
        {
            return new StructuredLogViewerLoadResult();
        }

        var entries = new List<StructuredLogViewerEntry>();
        var parseErrorCount = 0;
        var totalLineCount = 0;

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            totalLineCount++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!TryParseEntry(totalLineCount, line, out var entry))
            {
                parseErrorCount++;
                continue;
            }

            if (MatchesFilter(entry, filter))
            {
                entries.Add(entry);
            }
        }

        return new StructuredLogViewerLoadResult
        {
            Entries = entries
                .OrderByDescending(item => item.TimestampUtc ?? DateTimeOffset.MinValue)
                .ThenByDescending(item => item.LineNumber)
                .ToList(),
            ParseErrorCount = parseErrorCount,
            TotalLineCount = totalLineCount
        };
    }

    public string GetStructuredLogFilePath()
    {
        var logFolder = string.IsNullOrWhiteSpace(_settingsStore.Settings.LogFolder)
            ? _appPaths.LogsFolder
            : _settingsStore.Settings.LogFolder;
        return Path.Combine(logFolder, StructuredLoggingDefaults.StructuredEventsFileName);
    }

    private static bool TryParseEntry(int lineNumber, string line, out StructuredLogViewerEntry entry)
    {
        entry = new StructuredLogViewerEntry();

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var ts = GetString(root, "ts");
            var timestamp = TryParseTimestamp(ts);
            var contextJson = root.TryGetProperty("context", out var contextElement)
                ? contextElement.GetRawText()
                : "{}";

            var level = GetString(root, "level") ?? string.Empty;
            var codeText = GetString(root, "code") ?? string.Empty;
            var codeValue = TryParseCode(codeText);

            // The code is the canonical identity; prefer the registry description over the raw JSON
            // fields so display strings and remediation can never drift from the code. Fall back to
            // the emitted JSON fields (then to bit decomposition) so legacy, code-less lines still render.
            StatusCodeDescriptor? descriptor = codeValue is { } c ? StatusCodes.Describe(c) : null;

            var facilityByte = codeValue.HasValue ? StatusCodes.FacilityOf(codeValue.Value) : (byte?)null;
            var facility = descriptor?.FacilityName ?? GetString(root, "facility") ?? string.Empty;
            var operation = descriptor?.OperationName ?? GetString(root, "operation") ?? string.Empty;
            var phase = descriptor is { } d ? d.Phase.ToString().ToLowerInvariant() : GetString(root, "phase") ?? string.Empty;
            var severity = descriptor is { } sd
                ? sd.Severity.ToString()
                : codeValue is { } sc
                    ? StatusCodes.SeverityOf(sc).ToString()
                    : GetString(root, "severity") ?? string.Empty;
            var statusByteText = codeValue is { } stc ? $"0x{StatusCodes.StatusOf(stc):X2}" : string.Empty;
            var flags = codeValue is { } fc ? StatusCodes.FlagNames(fc) : ReadFlags(root);

            entry = new StructuredLogViewerEntry
            {
                LineNumber = lineNumber,
                TimestampUtc = timestamp,
                TimestampText = ts ?? string.Empty,
                Level = level,
                Event = GetString(root, "event") ?? string.Empty,
                OperationId = GetString(root, "operationId") ?? string.Empty,
                Result = GetString(root, "result") ?? string.Empty,
                ContextJson = contextJson,
                RawJsonLine = line,
                Code = codeValue is { } cv ? $"0x{cv:X8}" : codeText,
                CodeValue = codeValue,
                FacilityByte = facilityByte,
                Facility = facility,
                Operation = operation,
                Phase = phase,
                Severity = severity,
                Flags = flags,
                Thread = TryParseThread(root),
                Callsite = GetString(root, "callsite") ?? string.Empty,
                StatusByteText = statusByteText,
                Title = descriptor?.Title,
                Message = descriptor?.Message,
                Remediation = descriptor?.Remediation,
                LevelRank = RankForLevel(level)
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static uint? TryParseCode(string? codeText)
    {
        if (string.IsNullOrWhiteSpace(codeText))
        {
            return null;
        }

        var span = codeText.AsSpan().Trim();
        if (span.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || span.StartsWith("0X", StringComparison.Ordinal))
        {
            span = span[2..];
        }

        return uint.TryParse(span, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static IReadOnlyList<string> ReadFlags(JsonElement root)
    {
        if (!root.TryGetProperty("flags", out var flagsElement) || flagsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var names = new List<string>();
        foreach (var item in flagsElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } name)
            {
                names.Add(name);
            }
        }

        return names.Count == 0 ? Array.Empty<string>() : names;
    }

    private static int? TryParseThread(JsonElement root)
    {
        if (root.TryGetProperty("thread", out var threadElement) &&
            threadElement.ValueKind == JsonValueKind.Number &&
            threadElement.TryGetInt32(out var thread))
        {
            return thread;
        }

        return null;
    }

    /// <summary>Maps a projected level string onto the filter ladder. Unknown values sort as Info.</summary>
    internal static StatusLevelRank RankForLevel(string? level) => level?.Trim().ToLowerInvariant() switch
    {
        "debug" => StatusLevelRank.Debug,
        "trace" => StatusLevelRank.Debug,
        "warn" => StatusLevelRank.Warn,
        "warning" => StatusLevelRank.Warn,
        "error" => StatusLevelRank.Error,
        "critical" => StatusLevelRank.Error,
        "fatal" => StatusLevelRank.Error,
        _ => StatusLevelRank.Info
    };

    private static bool MatchesFilter(StructuredLogViewerEntry entry, StructuredLogViewerFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.OperationId) &&
            !entry.OperationId.Contains(filter.OperationId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.MinimumLevel is { } minimumLevel && entry.LevelRank < minimumLevel)
        {
            return false;
        }

        if (filter.Facilities is { Count: > 0 } facilities)
        {
            if (entry.FacilityByte is not { } facilityByte || !facilities.Contains(facilityByte))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Event) &&
            !entry.Event.Contains(filter.Event, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.StartUtc.HasValue)
        {
            if (!entry.TimestampUtc.HasValue || entry.TimestampUtc.Value < filter.StartUtc.Value)
            {
                return false;
            }
        }

        if (filter.EndUtc.HasValue)
        {
            if (!entry.TimestampUtc.HasValue || entry.TimestampUtc.Value > filter.EndUtc.Value)
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.TextSearch))
        {
            var aggregateText = string.Join(
                " ",
                entry.TimestampText,
                entry.Level,
                entry.Event,
                entry.OperationId,
                entry.Result,
                entry.Code,
                entry.ContextJson,
                entry.RawJsonLine);
            if (!aggregateText.Contains(filter.TextSearch, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static DateTimeOffset? TryParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.GetRawText()
        };
    }
}
