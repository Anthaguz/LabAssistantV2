using LabAssistant.UITesting.Infrastructure;

namespace LabAssistant.UITesting.Runner;

/// <summary>
/// Launches the app, runs a sequence of scenarios, and writes findings. Each
/// scenario is isolated: a throw is captured as a finding and the run continues,
/// so one broken workflow never hides the others. The app process is always
/// torn down at the end.
/// </summary>
public sealed class ScenarioRunner
{
    private readonly string _exePath;
    private readonly HarnessConfig _config;
    private readonly string _repoRoot;

    public ScenarioRunner(string exePath, HarnessConfig config, string repoRoot)
    {
        _exePath = exePath;
        _config = config;
        _repoRoot = repoRoot;
    }

    /// <summary>Runs the scenarios and returns the run directory. Non-zero exit is decided by the caller from findings.</summary>
    public FindingRecorder Run(IReadOnlyList<IScenario> scenarios)
    {
        string runDir = Path.Combine(_repoRoot, "LabAssistant.UITesting", "runs",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(runDir);
        var recorder = new FindingRecorder(runDir);

        Console.WriteLine($"Run directory: {runDir}");
        Console.WriteLine($"Launching: {_exePath}");

        AppHost? host = null;
        try
        {
            host = AppHost.Launch(_exePath);
            Console.WriteLine($"Main window ready: \"{host.MainWindow.Title}\"");
            var context = new RunContext(host, recorder, _config, _repoRoot);

            foreach (var scenario in scenarios)
            {
                Console.WriteLine($"== Scenario: {scenario.Name} ==");

                if (host.Application.HasExited)
                {
                    recorder.Record(new Finding
                    {
                        Scenario = scenario.Name,
                        Step = "precheck",
                        Severity = FindingSeverity.Crash,
                        Title = "App process had already exited before this scenario",
                        Detail = "A previous scenario likely crashed the app. Remaining scenarios cannot run."
                    });
                    break;
                }

                try
                {
                    scenario.Run(context);
                }
                catch (Exception ex)
                {
                    var severity = host.Application.HasExited ? FindingSeverity.Crash : FindingSeverity.Error;
                    recorder.RecordFailure(
                        host,
                        scenario.Name,
                        "unhandled",
                        severity,
                        $"Scenario threw {ex.GetType().Name}",
                        ex.Message,
                        ex);
                }
            }
        }
        catch (Exception ex)
        {
            recorder.Record(new Finding
            {
                Scenario = "runner",
                Step = "startup",
                Severity = FindingSeverity.Crash,
                Title = $"Harness failed to start the app: {ex.GetType().Name}",
                Detail = ex.Message,
                ExceptionType = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
        finally
        {
            host?.Dispose();
            recorder.Flush();
        }

        Console.WriteLine();
        Console.WriteLine($"Findings: {recorder.Findings.Count} (failures: {recorder.HasFailures})");
        Console.WriteLine($"See: {Path.Combine(runDir, "findings.md")}");
        return recorder;
    }
}
