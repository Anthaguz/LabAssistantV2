using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using LabAssistant.UITesting.Infrastructure;

namespace LabAssistant.UITesting.Runner;

/// <summary>
/// Collects findings and evidence for one harness run. Screenshots and a
/// machine-readable findings.json plus a human-readable findings.md are written
/// under runs/&lt;runId&gt;/ so a person or an agent can triage them later.
/// </summary>
public sealed class FindingRecorder
{
    private readonly List<Finding> _findings = new();
    private readonly string _screenshotsDir;
    private readonly string _appLogsDir;
    private readonly AppEventLogCapture _appLog;
    private int _screenshotCounter;
    private int _appLogCounter;

    // The window into the app's structured event log for the scenario currently running.
    // Opened per scenario (each gets a fresh app instance) so a captured slice is scoped
    // to that scenario's session. Null until the first BeginAppLogWindow call.
    private AppLogWindow? _appLogWindow;

    public FindingRecorder(string runDir)
        : this(runDir, new AppEventLogCapture(new AppDataLocations()))
    {
    }

    // Overload lets tests inject a capture pointed at a scratch log folder.
    public FindingRecorder(string runDir, AppEventLogCapture appLog)
    {
        RunDir = runDir;
        _screenshotsDir = Path.Combine(runDir, "screenshots");
        _appLogsDir = Path.Combine(runDir, "app-logs");
        _appLog = appLog;
        Directory.CreateDirectory(_screenshotsDir);
    }

    public string RunDir { get; }

    public IReadOnlyList<Finding> Findings => _findings;

    public bool HasFailures => _findings.Any(f => f.Severity is FindingSeverity.Error or FindingSeverity.Crash);

    /// <summary>
    /// Starts a fresh app-log window for the scenario about to run. The harness calls this
    /// right after launching the app, so any failure recorded for the scenario captures
    /// only the app events from that launch onward.
    /// </summary>
    public void BeginAppLogWindow() => _appLogWindow = _appLog.OpenWindow();

    /// <summary>Captures a screenshot of the app window and returns the file path.</summary>
    /// <remarks>
    /// Prefers Win32 <c>PrintWindow</c> (see <see cref="WindowCapture"/>), which can read
    /// the app's DirectComposition-rendered content; the GDI paths FlaUI exposes come back
    /// blank for a WinUI 3 window. Falls back to FlaUI element capture, then a full-screen
    /// grab, if the window capture is unavailable or renders blank.
    /// </remarks>
    public string? Capture(AppHost host, string label)
    {
        try
        {
            string safe = Sanitize(label);
            string file = Path.Combine(_screenshotsDir, $"{++_screenshotCounter:D3}-{safe}.png");

            IntPtr hwnd = host.MainWindow.Properties.NativeWindowHandle.ValueOrDefault;
            if (WindowCapture.TrySaveWindowPng(hwnd, file, out bool blank) && !blank)
            {
                return file;
            }

            CaptureImage image;
            try
            {
                image = FlaUI.Core.Capturing.Capture.Element(host.MainWindow);
            }
            catch
            {
                image = FlaUI.Core.Capturing.Capture.Screen();
            }

            image.ToFile(file);
            return file;
        }
        catch
        {
            return null;
        }
    }

    public void Record(Finding finding)
    {
        _findings.Add(finding);
        Console.WriteLine($"  [{finding.Severity}] {finding.Scenario}/{finding.Step}: {finding.Title}");
    }

    /// <summary>
    /// Captures the current scenario's slice of the app structured event log to an evidence
    /// file and returns its path relative to the run directory, or null when no window is
    /// open or nothing matched. Optionally narrows the slice to a single operationId.
    /// </summary>
    public string? CaptureAppLog(string label, string? operationId = null)
    {
        if (_appLogWindow is not AppLogWindow window)
        {
            return null;
        }

        string safe = Sanitize(label);
        string file = Path.Combine(_appLogsDir, $"{++_appLogCounter:D3}-{safe}.jsonl");
        int written = _appLog.WriteSlice(window, operationId, file);
        return written > 0 ? Path.GetRelativePath(RunDir, file) : null;
    }

    /// <summary>Convenience: record a failure with a screenshot and the app log slice captured now.</summary>
    public void RecordFailure(
        AppHost host,
        string scenario,
        string step,
        FindingSeverity severity,
        string title,
        string detail,
        Exception? exception = null,
        string? operationId = null)
    {
        string? shot = Capture(host, $"{scenario}-{step}");
        string? appLog = CaptureAppLog($"{scenario}-{step}", operationId);
        Record(new Finding
        {
            Scenario = scenario,
            Step = step,
            Severity = severity,
            Title = title,
            Detail = detail,
            ScreenshotFile = shot is null ? null : Path.GetRelativePath(RunDir, shot),
            AppLogFile = appLog,
            ExceptionType = exception?.GetType().Name,
            StackTrace = exception?.StackTrace
        });
    }

    /// <summary>Writes findings.json and findings.md into the run directory.</summary>
    public void Flush()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        File.WriteAllText(Path.Combine(RunDir, "findings.json"),
            JsonSerializer.Serialize(_findings, options));

        var md = new StringBuilder();
        md.AppendLine("# UI harness findings");
        md.AppendLine();
        md.AppendLine($"Run directory: `{RunDir}`  ");
        md.AppendLine($"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}  ");
        md.AppendLine($"Total findings: {_findings.Count} " +
            $"(errors/crashes: {_findings.Count(f => f.Severity is FindingSeverity.Error or FindingSeverity.Crash)})");
        md.AppendLine();

        foreach (var group in _findings.GroupBy(f => f.Scenario))
        {
            md.AppendLine($"## {group.Key}");
            md.AppendLine();
            foreach (var f in group)
            {
                md.AppendLine($"### [{f.Severity}] {f.Step} - {f.Title}");
                md.AppendLine();
                if (!string.IsNullOrWhiteSpace(f.Detail))
                {
                    md.AppendLine(f.Detail);
                    md.AppendLine();
                }

                if (f.ExceptionType is not null)
                {
                    md.AppendLine($"Exception: `{f.ExceptionType}`");
                    md.AppendLine();
                }

                if (f.ScreenshotFile is not null)
                {
                    md.AppendLine($"![screenshot]({f.ScreenshotFile.Replace('\\', '/')})");
                    md.AppendLine();
                }

                if (f.AppLogFile is not null)
                {
                    md.AppendLine($"App event log slice: [`{f.AppLogFile.Replace('\\', '/')}`]" +
                        $"({f.AppLogFile.Replace('\\', '/')})");
                    md.AppendLine();
                }
            }
        }

        File.WriteAllText(Path.Combine(RunDir, "findings.md"), md.ToString());
    }

    private static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-');
        }

        return sb.ToString().Trim('-').ToLowerInvariant();
    }
}
