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
                "deploy" => RunDeployProof(args),
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
            new SmokeNavigationScenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
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
        Console.WriteLine("  deploy  Seed resources, drive a single-VM Quick Deploy, validate, and tear down.");
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
