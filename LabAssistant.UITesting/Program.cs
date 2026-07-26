using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Runner;
using LabAssistant.UITesting.Scenarios;

namespace LabAssistant.UITesting;

/// <summary>
/// Entry point for the LabAssistant UI automation harness.
///
/// Verbs:
///   dump    Launch the app and print its UI Automation tree (selector discovery).
///   run     Launch the app and run the scenario suite, writing findings.
///
/// More verbs (nightly loop, targeted scenarios) are added as the harness grows.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

        try
        {
            return verb switch
            {
                "dump" => RunDump(args),
                "run" => RunScenarios(args),
                "coverage" => RunCoverage(args),
                "capture" => RunCaptureSelfTest(args),
                "deploy" => RunDeployProof(args),
                "template-e2e" => RunTemplateDeployProof(args),
                "sweep" => RunSweep(args),
                _ => PrintHelp()
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL: {ex.GetType().Name}: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static int RunScenarios(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new SmokeNavigationScenario(),
            new AutomationIdCoverageScenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
    }

    /// <summary>
    /// Runs only the AutomationId coverage audit, so the addressability report can
    /// be regenerated on demand without a full suite. Coverage gaps are advisory
    /// (Warning/Info), so this exits non-zero only on a hard failure (Error/Crash).
    /// </summary>
    private static int RunCoverage(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new AutomationIdCoverageScenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
    }

    /// <summary>
    /// Capture-health self-test: launches the app and captures its main window to a PNG
    /// via the same path the recorder uses, then reports the file, its dimensions, and
    /// whether it rendered real content. Lets a night run's evidence pipeline be verified
    /// before trusting it. Exits non-zero if the capture is missing or blank.
    /// </summary>
    private static int RunCaptureSelfTest(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        Console.WriteLine($"Launching: {exePath}");
        using var host = AppHost.Launch(exePath);
        Console.WriteLine($"Main window ready: \"{host.MainWindow.Title}\"");
        // Give the window a moment to finish its first composition pass.
        Thread.Sleep(1000);

        string runDir = Path.Combine(repoRoot, "LabAssistant.UITesting", "runs",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(runDir);
        string file = Path.Combine(runDir, "capture-selftest.png");

        IntPtr hwnd = host.MainWindow.Properties.NativeWindowHandle.ValueOrDefault;
        Console.WriteLine($"Main window HWND: 0x{hwnd.ToInt64():X}");

        bool wrote = Infrastructure.WindowCapture.TrySaveWindowPng(hwnd, file, out bool blank);
        if (!wrote)
        {
            Console.Error.WriteLine("PrintWindow capture FAILED (no file written).");
            return 1;
        }

        var info = new FileInfo(file);
        Console.WriteLine($"Wrote: {file} ({info.Length} bytes)");
        Console.WriteLine(blank
            ? "RESULT: BLANK - the capture rendered no real content."
            : "RESULT: OK - the capture contains real content.");
        return blank ? 1 : 0;
    }

    private static int RunDeployProof(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new QuickDeploySingleVmScenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
    }

    /// <summary>
    /// End-to-end template deploy proof: seeds a tagged minimal standalone V2 template that
    /// references the harness base disk and switch, drives Deploy &gt; From Template to deploy it,
    /// validates the resulting VM against Hyper-V ground truth, then tears everything down by tag
    /// (VM + template file) and proves no orphans. Exits non-zero on any Error/Crash finding.
    /// </summary>
    private static int RunTemplateDeployProof(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new TemplateDeployV2Scenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
    }

    /// <summary>
    /// Removes every Hyper-V resource carrying the harness tag prefix. Safe to run
    /// any time to guarantee a clean slate; never touches untagged resources.
    /// </summary>
    private static int RunSweep(string[] args)
    {
        var appData = new Infrastructure.AppDataLocations();
        var probe = new Infrastructure.HyperVProbe();
        var catalog = new Infrastructure.CatalogSeeder(appData);
        var config = HarnessConfig.Load(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "testenv.json"));

        // Synthetic tagger: only the global prefix matters for a cross-run sweep.
        var tagger = new Infrastructure.Cleanup.ResourceTagger(config.RunTagPrefix, "sweep");
        var sweeper = new Infrastructure.Cleanup.TeardownSweeper(tagger, probe, appData, catalog);

        var report = sweeper.SweepAll();
        Console.WriteLine("Sweep report:");
        Console.WriteLine(report);
        return 0;
    }

    private static int RunDump(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        string? capability = args.Length > 1 ? args[1] : null;

        Console.WriteLine($"Launching: {exePath}");
        using var host = AppHost.Launch(exePath);
        Console.WriteLine($"Main window ready: \"{host.MainWindow.Title}\"");

        if (!string.IsNullOrWhiteSpace(capability))
        {
            var nav = new Pages.ShellNav(host);
            nav.NavigateTo(capability);
            Thread.Sleep(800);
            Console.WriteLine($"Navigated to: {capability}");

            string? tab = args.Length > 2 ? args[2] : null;
            if (!string.IsNullOrWhiteSpace(tab))
            {
                var tabItem = host.MainWindow.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem).And(cf.ByName(tab)));
                if (tabItem is not null)
                {
                    tabItem.Activate();
                    Thread.Sleep(1000);
                    Console.WriteLine($"Selected tab: {tab}");
                }
                else
                {
                    Console.WriteLine($"Tab '{tab}' not found.");
                }
            }
        }

        string tree = host.DumpTree();

        string runDir = Path.Combine(repoRoot, "LabAssistant.UITesting", "runs",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(runDir);
        string treeFile = Path.Combine(runDir, "automation-tree.txt");
        File.WriteAllText(treeFile, tree);

        Console.WriteLine(tree);
        Console.WriteLine();
        Console.WriteLine($"Automation tree written to: {treeFile}");
        return 0;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("LabAssistant UI automation harness");
        Console.WriteLine();
        Console.WriteLine("Usage: LabAssistant.UITesting <verb>");
        Console.WriteLine();
        Console.WriteLine("Verbs:");
        Console.WriteLine("  dump    Launch the app and print its UI Automation tree.");
        Console.WriteLine("  run     Launch the app and run the scenario suite, writing findings.");
        Console.WriteLine("  coverage Audit each capability's live UI tree for controls missing a stable AutomationId.");
        Console.WriteLine("  capture Launch the app and self-test window screenshot capture (evidence health check).");
        Console.WriteLine("  deploy  Seed resources, drive a single-VM Quick Deploy, validate, and tear down.");
        Console.WriteLine("  template-e2e Seed a V2 template, deploy it via From Template, validate, and tear down.");
        Console.WriteLine("  sweep   Remove any leftover harness-tagged Hyper-V resources.");
        return 0;
    }

    /// <summary>
    /// Walks up from a starting directory until it finds the repo root (the
    /// directory containing LabAssistant.sln).
    /// </summary>
    private static string LocateRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LabAssistant.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate LabAssistant.sln walking up from '{start}'.");
    }
}
