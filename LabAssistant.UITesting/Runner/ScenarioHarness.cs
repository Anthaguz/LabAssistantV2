using LabAssistant.UITesting.Infrastructure;

namespace LabAssistant.UITesting.Runner;

/// <summary>
/// Runs a sequence of scenarios and writes findings. Each scenario gets its own
/// fresh app instance, so a crash or leaked UI state in one can never bleed into
/// the next - the price is a relaunch per scenario, which is irrelevant for an
/// all-night harness and buys strong isolation.
///
/// A <see cref="ScenarioRequirements.HyperV"/> scenario is bracketed by a
/// <see cref="HyperVScenarioGate"/>: the harness seeds tagged resources before
/// launch and, in a finally block, tears everything down and proves no orphans
/// survived - even if the scenario throws or the app crashes. A
/// <see cref="ScenarioRequirements.None"/> scenario is a plain launch-run-dispose
/// pass with no Hyper-V footprint. All scenarios share one run directory and
/// recorder so findings aggregate into a single report.
/// </summary>
public sealed class ScenarioHarness
{
    private readonly string _exePath;
    private readonly HarnessConfig _config;
    private readonly string _repoRoot;

    public ScenarioHarness(string exePath, HarnessConfig config, string repoRoot)
    {
        _exePath = exePath;
        _config = config;
        _repoRoot = repoRoot;
    }

    /// <summary>Runs the scenarios and returns the recorder. The caller decides the exit code from <see cref="FindingRecorder.HasFailures"/>.</summary>
    public FindingRecorder Run(IReadOnlyList<IScenario> scenarios)
    {
        string runDir = Path.Combine(_repoRoot, "LabAssistant.UITesting", "runs",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(runDir);
        var recorder = new FindingRecorder(runDir);

        Console.WriteLine($"Run directory: {runDir}");
        Console.WriteLine($"App exe: {_exePath}");

        foreach (var scenario in scenarios)
        {
            Console.WriteLine();
            Console.WriteLine($"== Scenario: {scenario.Name} [{scenario.Capability}] " +
                $"(requires {scenario.Requirements}) ==");

            if (scenario.Requirements == ScenarioRequirements.HyperV)
            {
                RunHyperVScenario(scenario, recorder);
            }
            else
            {
                RunUiScenario(scenario, recorder);
            }

            // Flush after each scenario so a crash mid-suite still leaves a report
            // for everything that completed.
            recorder.Flush();
        }

        Console.WriteLine();
        Console.WriteLine($"Findings: {recorder.Findings.Count} (failures: {recorder.HasFailures})");
        Console.WriteLine($"See: {Path.Combine(runDir, "findings.md")}");
        return recorder;
    }

    private void RunHyperVScenario(IScenario scenario, FindingRecorder recorder)
    {
        var gate = new HyperVScenarioGate(_config);
        Console.WriteLine($"Run tag prefix: {gate.Tagger.RunPrefix}");

        AppHost? host = null;
        try
        {
            var resources = gate.Prepare(recorder, scenario.Name);

            host = AppHost.Launch(_exePath);
            Console.WriteLine($"Main window ready: \"{host.MainWindow.Title}\"");
            recorder.BeginAppLogWindow();

            var context = new ScenarioContext(host, recorder, _config, _repoRoot, resources);
            RunGuarded(scenario, context, host, recorder);
        }
        catch (Exception ex)
        {
            recorder.Record(new Finding
            {
                Scenario = scenario.Name,
                Step = "setup",
                Severity = FindingSeverity.Crash,
                Title = $"Scenario failed before/at launch: {ex.GetType().Name}",
                Detail = ex.Message,
                ExceptionType = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
        finally
        {
            // Tear down the app first, then every tagged resource, then verify no
            // orphans. This runs unconditionally so a mid-scenario crash still cleans up.
            host?.Dispose();
            gate.TeardownAndVerify(recorder, scenario.Name);
        }
    }

    private void RunUiScenario(IScenario scenario, FindingRecorder recorder)
    {
        AppHost? host = null;
        try
        {
            host = AppHost.Launch(_exePath);
            Console.WriteLine($"Main window ready: \"{host.MainWindow.Title}\"");
            recorder.BeginAppLogWindow();

            var context = new ScenarioContext(host, recorder, _config, _repoRoot, hyperV: null);
            RunGuarded(scenario, context, host, recorder);
        }
        catch (Exception ex)
        {
            recorder.Record(new Finding
            {
                Scenario = scenario.Name,
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
        }
    }

    /// <summary>Runs the scenario body, converting a throw into a captured failure finding.</summary>
    private static void RunGuarded(IScenario scenario, ScenarioContext context, AppHost host, FindingRecorder recorder)
    {
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
