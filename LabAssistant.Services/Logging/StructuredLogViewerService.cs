using System.Text.Json;
using LabAssistant.Models.Configuration;

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

            entry = new StructuredLogViewerEntry
            {
                LineNumber = lineNumber,
                TimestampUtc = timestamp,
                TimestampText = ts ?? string.Empty,
                Level = GetString(root, "level") ?? string.Empty,
                Event = GetString(root, "event") ?? string.Empty,
                OperationId = GetString(root, "operationId") ?? string.Empty,
                Result = GetString(root, "result") ?? string.Empty,
                ContextJson = contextJson,
                RawJsonLine = line
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool MatchesFilter(StructuredLogViewerEntry entry, StructuredLogViewerFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.OperationId) &&
            !entry.OperationId.Contains(filter.OperationId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Level) &&
            !string.Equals(entry.Level, filter.Level, StringComparison.OrdinalIgnoreCase))
        {
            return false;
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
