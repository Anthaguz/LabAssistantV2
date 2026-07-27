using System.Text.Json;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>
/// A point in time the harness starts watching the app's structured event log. Opened
/// when a scenario's app instance launches, and passed back to
/// <see cref="AppEventLogCapture.WriteSlice"/> on failure so the captured slice is scoped
/// to just that scenario's app session rather than the whole (possibly reused) log file.
/// </summary>
public readonly record struct AppLogWindow(DateTimeOffset StartUtc);

/// <summary>
/// Reads the app's structured event log (<c>structured-events.jsonl</c>) and writes the
/// slice relevant to a failed scenario into the run's evidence folder.
///
/// This exists so a deploy failure carries the app's own account of what happened - the
/// ordered stream of operationId / stepKey / result / error events - alongside the
/// screenshot. When a live deploy fails at, say, <c>v2.prepareGuestNetwork</c>, the
/// captured slice is what points an investigator at the failing step and its error,
/// without them having to go dig in %APPDATA% after the fact (by which point a later run
/// may have rotated the log away).
///
/// Every method is failure-tolerant: capturing evidence must never itself throw into the
/// failure-recording path, so parse errors, locked files, and a missing log all degrade
/// to "no slice" rather than masking the original finding.
/// </summary>
public sealed class AppEventLogCapture
{
    // The app rotates the active file at ~5 MB and keeps a handful of history files, all
    // named structured-events*.jsonl. A single deploy is far smaller than one rotation,
    // but reading every matching file and filtering by timestamp keeps the slice correct
    // even if a rotation happens to land mid-scenario.
    private const string LogGlob = "structured-events*.jsonl";

    private readonly string _logsFolder;

    public AppEventLogCapture(AppDataLocations locations)
    {
        _logsFolder = locations.LogsFolder;
    }

    /// <summary>Marks "now" as the start of a scenario's log window.</summary>
    public AppLogWindow OpenWindow() => new(DateTimeOffset.UtcNow);

    /// <summary>
    /// Writes the log events at or after <paramref name="window"/> (optionally narrowed to
    /// a single <paramref name="operationId"/>) to <paramref name="destPath"/> as JSONL,
    /// newest-capped to <paramref name="maxLines"/>. Returns the number of events written;
    /// 0 means nothing matched (or the log was unreadable) and no file was created.
    /// </summary>
    public int WriteSlice(
        AppLogWindow window,
        string? operationId,
        string destPath,
        int maxLines = 4000)
    {
        try
        {
            List<(DateTimeOffset Ts, string Line)> matches = CollectMatches(window, operationId);
            if (matches.Count == 0)
            {
                return 0;
            }

            // OrderBy is a stable sort, so events sharing an identical ts keep their read
            // order (per-file, then line order). That matters: the coarse system-clock
            // granularity means a burst within one deploy step often carries the same ts,
            // and this slice is only useful if that stream stays in real sequence.
            List<(DateTimeOffset Ts, string Line)> ordered = matches.OrderBy(m => m.Ts).ToList();

            IEnumerable<string> lines = ordered.Count > maxLines
                ? ordered.Skip(ordered.Count - maxLines).Select(m => m.Line)
                : ordered.Select(m => m.Line);

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.WriteAllLines(destPath, lines);
            return Math.Min(ordered.Count, maxLines);
        }
        catch
        {
            // Evidence capture is best-effort; never let it mask the real finding.
            return 0;
        }
    }

    private List<(DateTimeOffset Ts, string Line)> CollectMatches(AppLogWindow window, string? operationId)
    {
        var matches = new List<(DateTimeOffset Ts, string Line)>();
        if (!Directory.Exists(_logsFolder))
        {
            return matches;
        }

        foreach (string file in Directory.EnumerateFiles(_logsFolder, LogGlob))
        {
            foreach (string line in ReadLinesShared(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (!TryReadEvent(line, out DateTimeOffset ts, out string? lineOperationId))
                {
                    continue;
                }

                if (ts < window.StartUtc)
                {
                    continue;
                }

                if (operationId is not null &&
                    !string.Equals(operationId, lineOperationId, StringComparison.Ordinal))
                {
                    continue;
                }

                matches.Add((ts, line));
            }
        }

        return matches;
    }

    /// <summary>
    /// Reads a line's <c>ts</c> and <c>operationId</c>. Returns false for a line we cannot
    /// parse or that lacks a timestamp, so malformed lines are skipped rather than aborting
    /// the whole capture.
    /// </summary>
    private static bool TryReadEvent(string line, out DateTimeOffset ts, out string? operationId)
    {
        ts = default;
        operationId = null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("ts", out JsonElement tsElement) ||
                tsElement.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(
                    tsElement.GetString(),
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out ts))
            {
                return false;
            }

            if (root.TryGetProperty("operationId", out JsonElement opElement) &&
                opElement.ValueKind == JsonValueKind.String)
            {
                operationId = opElement.GetString();
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads a file the app may still hold open for writing. Opening with
    /// <see cref="FileShare.ReadWrite"/> avoids a sharing violation against the live logger.
    /// </summary>
    private static IEnumerable<string> ReadLinesShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            yield return line;
        }
    }
}
