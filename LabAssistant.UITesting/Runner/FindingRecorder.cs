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
    private int _screenshotCounter;

    public FindingRecorder(string runDir)
    {
        RunDir = runDir;
        _screenshotsDir = Path.Combine(runDir, "screenshots");
        Directory.CreateDirectory(_screenshotsDir);
    }

    public string RunDir { get; }

    public IReadOnlyList<Finding> Findings => _findings;

    public bool HasFailures => _findings.Any(f => f.Severity is FindingSeverity.Error or FindingSeverity.Crash);

    /// <summary>Captures a screenshot of the app window (falls back to full screen) and returns the file path.</summary>
    public string? Capture(AppHost host, string label)
    {
        try
        {
            string safe = Sanitize(label);
            string file = Path.Combine(_screenshotsDir, $"{++_screenshotCounter:D3}-{safe}.png");
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

    /// <summary>Convenience: record a failure with a screenshot captured now.</summary>
    public void RecordFailure(
        AppHost host,
        string scenario,
        string step,
        FindingSeverity severity,
        string title,
        string detail,
        Exception? exception = null)
    {
        string? shot = Capture(host, $"{scenario}-{step}");
        Record(new Finding
        {
            Scenario = scenario,
            Step = step,
            Severity = severity,
            Title = title,
            Detail = detail,
            ScreenshotFile = shot is null ? null : Path.GetRelativePath(RunDir, shot),
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
