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
                "template-multivm" => RunMultiVmTemplateDeployProof(args),
                "template-dc" => RunDcTemplateDeployProof(args),
                "template-dc-member" => RunDcMemberTemplateDeployProof(args),
                "template-forest-trust" => RunForestTrustTemplateDeployProof(args),
                "template-forest-trust-routed" => RunForestTrustRoutedTemplateDeployProof(args),
                "template-forest-trust-rollback" => RunForestTrustRollbackTemplateDeployProof(args),
                "template-router" => RunRouterTemplateDeployProof(args),
                "template-guest-static" => RunGuestStaticTemplateDeployProof(args),
                "template-guest-static-multivm" => RunGuestStaticMultiVmTemplateDeployProof(args),
                "template-switch-autocreate" => RunSwitchAutoCreateRegression(args),
                "template-switch-autocreate-live" => RunSwitchAutoCreateLiveRegression(args),
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
    /// Multi-VM end-to-end template deploy proof: seeds a tagged V2 template with three
    /// standalone VMs (distinct memory/cpu each), drives Deploy &gt; From Template to deploy the
    /// whole plan, validates every resulting VM against Hyper-V ground truth, then tears
    /// everything down by tag (all VMs + template file) and proves no orphans. Exits non-zero on
    /// any Error/Crash finding.
    /// </summary>
    private static int RunMultiVmTemplateDeployProof(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new TemplateDeployMultiVmScenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
    }

    /// <summary>
    /// Single Domain Controller deploy scenario: ensures the real base image is guest-configurable,
    /// seeds a tagged single-DC V2 template, and drives Deploy &gt; From Template to a startable plan.
    /// With LABASSISTANT_SMOKE_ADMIN_PASSWORD set it also Starts the deploy, waits for promotion, and
    /// validates the real forest/domain over PowerShell Direct, then tears the DC VM down by tag and
    /// proves no orphans. Exits non-zero on any Error/Crash finding.
    /// </summary>
    private static int RunDcTemplateDeployProof(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new TemplateDeployDcScenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
    }

    /// <summary>
    /// DC + domain-joined member deploy scenario: ensures the real base image is guest-configurable,
    /// seeds a tagged two-VM V2 template (a FirstDomainController that promotes a forest + a DomainMember
    /// that joins it), and drives Deploy &gt; From Template to a startable plan. With
    /// LABASSISTANT_SMOKE_ADMIN_PASSWORD set it also Starts the deploy, waits for both VMs, confirms the
    /// DC promoted the forest, and validates over PowerShell Direct that the member reports
    /// PartOfDomain=true for the templated domain, then tears both VMs down by tag and proves no orphans.
    /// Exits non-zero on any Error/Crash finding.
    /// </summary>
    private static int RunDcMemberTemplateDeployProof(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new TemplateDeployDcMemberScenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
    }

    /// <summary>
    /// Two-forest + bidirectional forest-trust deploy scenario (the top complexity rung): ensures the
    /// real base image is guest-configurable, seeds a tagged two-VM V2 template (two FirstDomainControllers,
    /// each promoting its own forest on a shared Internal switch, linked by one bidirectional Forest trust),
    /// and drives Deploy &gt; From Template to a startable plan. With LABASSISTANT_SMOKE_ADMIN_PASSWORD set it
    /// also Starts the deploy, waits for both DCs, confirms each promoted its own forest, and validates over
    /// PowerShell Direct that a forest+bidirectional trust exists BOTH ways (Get-ADTrust from each side),
    /// then tears both DCs down by tag and proves no orphans. Exits non-zero on any Error/Crash finding.
    /// </summary>
    private static int RunForestTrustTemplateDeployProof(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new TemplateDeployForestTrustScenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
    }

    /// <summary>
    /// The routed cross-forest capstone: ensures the real base image is guest-configurable, seeds a tagged
    /// two-forest + bidirectional forest-trust V2 template where each forest sits on its OWN gatewayed
    /// Internal switch bridged by a standalone 3-NIC router (each DC's default gateway is the router's LAN
    /// leg on its subnet), and drives Deploy &gt; From Template to a startable plan. With
    /// LABASSISTANT_SMOKE_ADMIN_PASSWORD set it also Starts the deploy, waits for the router + both DCs,
    /// confirms each promoted its own forest, validates over PowerShell Direct that a forest+bidirectional
    /// trust exists BOTH ways, asserts from the app's step log that the router finished enabling routing
    /// BEFORE the first forest-trust DNS prep started (the #922 RouterReady-&gt;prepareDns edge) and that
    /// validateCrossSwitchRouting is correctly Skipped, proves the trust traffic crossed the router via a
    /// guest route-hop (next hop = the router LAN leg to reach the peer on the other subnet), then tears all
    /// three VMs + both auto-created Internal switches down by tag and proves no orphans. Exits non-zero on
    /// any Error/Crash finding.
    /// </summary>
    private static int RunForestTrustRoutedTemplateDeployProof(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new TemplateDeployForestTrustRoutedScenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
    }

    /// <summary>
    /// Forest-trust ROLLBACK deploy scenario (finding 79): reuses the #918 two-forest template, but on the
    /// live path Starts the deploy, waits for the app's log to report the create-trust step started, then
    /// cancels by navigating away and proves the runtime's cleanupForestTrust wrap ran
    /// (deploy.forest-trust.cleanup.start -&gt; cleanup.end) with zero orphans host-side before the gate's
    /// backstop sweep. A cancel that lands after the trust is already validated is reported INCONCLUSIVE, never
    /// a false pass. The planning-only path (no password) just proves the plan is startable. Exits non-zero on
    /// any Error/Crash finding.
    /// </summary>
    private static int RunForestTrustRollbackTemplateDeployProof(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new TemplateDeployForestTrustRollbackScenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
    }

    /// <summary>
    /// Single-router deploy scenario: ensures the real base image is guest-configurable, seeds a tagged
    /// single-router V2 template (multi-NIC, RRAS/NAT egress), and drives Deploy &gt; From Template to a
    /// startable plan. With LABASSISTANT_SMOKE_ADMIN_PASSWORD set it also Starts the deploy, waits for the
    /// router to settle, and validates over PowerShell Direct that the guest holds the templated LAN
    /// gateway IP, then tears the router VM down by tag and proves no orphans. Exits non-zero on any
    /// Error/Crash finding.
    /// </summary>
    private static int RunRouterTemplateDeployProof(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new TemplateDeployRouterScenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
    }

    /// <summary>
    /// Single guest-static-IP deploy scenario: ensures the real base image is guest-configurable, seeds a
    /// tagged single-VM V2 template whose NIC carries a templated static IP, and drives Deploy &gt; From
    /// Template to a startable plan. With LABASSISTANT_SMOKE_ADMIN_PASSWORD set it also Starts the deploy,
    /// waits for the VM to settle, and validates over PowerShell Direct that the guest holds the templated
    /// static IP, then tears the VM down by tag and proves no orphans. Exits non-zero on any Error/Crash
    /// finding.
    /// </summary>
    private static int RunGuestStaticTemplateDeployProof(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new TemplateDeployGuestStaticScenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
    }

    /// <summary>
    /// Two-VM guest-static-IP deploy scenario: ensures the real base image is guest-configurable, seeds a
    /// tagged V2 template with two VMs on one Internal switch each holding a DISTINCT static IP, and drives
    /// Deploy &gt; From Template to a startable plan. With LABASSISTANT_SMOKE_ADMIN_PASSWORD set it also Starts
    /// the deploy, waits for both VMs to settle, and validates over PowerShell Direct that EACH guest holds
    /// its OWN templated static IP (per-adapter MAC binding), then tears both VMs down by tag and proves no
    /// orphans. Exits non-zero on any Error/Crash finding.
    /// </summary>
    private static int RunGuestStaticMultiVmTemplateDeployProof(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new TemplateDeployGuestStaticMultiVmScenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
    }

    /// <summary>
    /// Live switch-auto-create regression: seeds a tagged standalone V2 template whose NIC references a
    /// virtual switch the harness never provisions, drives Deploy &gt; From Template, and - once the plan is
    /// startable - Starts the deploy so the runtime creates the missing switch, then asserts via Get-VMSwitch
    /// that the created switch is Internal before the gate tears down VM + switch by tag. Fails on master
    /// (documenting the bug, before Start) and proves the runtime half of the fix once it lands. Exits
    /// non-zero on any Error/Crash finding.
    /// </summary>
    private static int RunSwitchAutoCreateLiveRegression(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new SwitchAutoCreateLiveScenario()
        };

        var harness = new ScenarioHarness(exePath, config, repoRoot);
        var recorder = harness.Run(scenarios);
        return recorder.HasFailures ? 2 : 0;
    }

    /// <summary>
    /// Switch-auto-create planning regression: seeds a tagged standalone V2 template whose NIC references
    /// a virtual switch the harness never provisions, drives Deploy &gt; From Template, and asserts the plan
    /// is startable (the deploy should create the missing switch). Fails on master (documenting the bug)
    /// and passes once the deploy-network fix lands. Planning-only: no VM or switch is created. Exits
    /// non-zero on any Error/Crash finding.
    /// </summary>
    private static int RunSwitchAutoCreateRegression(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string repoRoot = LocateRepoRoot(baseDir);
        string testEnvPath = Path.Combine(baseDir, "Fixtures", "testenv.json");

        var config = HarnessConfig.Load(testEnvPath);
        string exePath = config.ResolveAppExePath(repoRoot);

        var scenarios = new List<IScenario>
        {
            new SwitchAutoCreateScenario()
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
        Console.WriteLine("  template-multivm Seed a 3-VM V2 template, deploy it via From Template, validate every VM, and tear down.");
        Console.WriteLine("  template-dc Seed a single-DC V2 template, drive it to a startable plan; with LABASSISTANT_SMOKE_ADMIN_PASSWORD set, deploy + validate AD over PowerShell Direct.");
        Console.WriteLine("  template-dc-member Seed a DC + domain-joined member V2 template, drive it to a startable plan; with LABASSISTANT_SMOKE_ADMIN_PASSWORD set, deploy + validate the DC forest and the member's domain membership over PowerShell Direct.");
        Console.WriteLine("  template-forest-trust Seed a two-forest V2 template (two DCs on one Internal switch + a bidirectional Forest trust), drive it to a startable plan; with LABASSISTANT_SMOKE_ADMIN_PASSWORD set, deploy + validate each forest and the trust BOTH ways over PowerShell Direct (Get-ADTrust).");
        Console.WriteLine("  template-forest-trust-routed Seed a ROUTED two-forest V2 template (two DCs on separate gatewayed Internal switches bridged by a 3-NIC router + a bidirectional Forest trust), drive it to a startable plan; with LABASSISTANT_SMOKE_ADMIN_PASSWORD set, deploy + validate the trust BOTH ways, assert (from the app step log) that router routing finished before the first forest-trust DNS prep started, and prove the trust crossed the router via a guest route-hop.");
        Console.WriteLine("  template-forest-trust-rollback Seed the same two-forest V2 template; with LABASSISTANT_SMOKE_ADMIN_PASSWORD set, deploy, cancel mid-create by navigating away, and prove the cleanupForestTrust wrap ran (structured log) with zero orphans host-side. Without a password, proves the plan is startable.");
        Console.WriteLine("  template-router Seed a single-router V2 template (multi-NIC RRAS/NAT), drive it to a startable plan; with LABASSISTANT_SMOKE_ADMIN_PASSWORD set, deploy + validate the LAN gateway IP over PowerShell Direct.");
        Console.WriteLine("  template-guest-static Seed a single-VM V2 template with a templated static IP, drive it to a startable plan; with LABASSISTANT_SMOKE_ADMIN_PASSWORD set, deploy + validate the in-guest static IP over PowerShell Direct.");
        Console.WriteLine("  template-guest-static-multivm Seed a 2-VM V2 template (distinct static IPs on one Internal switch), drive it to a startable plan; with LABASSISTANT_SMOKE_ADMIN_PASSWORD set, deploy + validate each VM's own in-guest static IP over PowerShell Direct.");
        Console.WriteLine("  template-switch-autocreate Regression: seed a template referencing an absent switch and assert the plan is startable (the deploy should create it).");
        Console.WriteLine("  template-switch-autocreate-live Regression: seed a template referencing an absent switch, Start the deploy, and assert the runtime creates it as an Internal switch (Get-VMSwitch).");
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
