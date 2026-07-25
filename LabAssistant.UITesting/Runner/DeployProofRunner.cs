using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.Cleanup;
using LabAssistant.UITesting.Infrastructure.ResourceProvider;
using LabAssistant.UITesting.Scenarios;

namespace LabAssistant.UITesting.Runner;

/// <summary>
/// Orchestrates one supervised deploy-and-cleanup proof. It owns the full
/// lifecycle so nothing leaks: sweep leftovers from prior runs, seed a tagged
/// base disk + switch, launch the app, drive a single-VM Quick Deploy, validate
/// the VM against Hyper-V, then unconditionally tear down everything the harness
/// created and confirm no tagged resource survives. The teardown runs in a
/// finally block, so a crash mid-scenario still cleans up.
/// </summary>
public sealed class DeployProofRunner
{
    private readonly string _exePath;
    private readonly HarnessConfig _config;
    private readonly string _repoRoot;

    public DeployProofRunner(string exePath, HarnessConfig config, string repoRoot)
    {
        _exePath = exePath;
        _config = config;
        _repoRoot = repoRoot;
    }

    public FindingRecorder Run()
    {
        string runDir = Path.Combine(_repoRoot, "LabAssistant.UITesting", "runs",
            "deploy-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(runDir);
        var recorder = new FindingRecorder(runDir);

        var tagger = ResourceTagger.CreateNew(_config.RunTagPrefix);
        var appData = new AppDataLocations();
        var probe = new HyperVProbe();
        var catalog = new CatalogSeeder(appData);
        var sweeper = new TeardownSweeper(tagger, probe, appData, catalog);

        Console.WriteLine($"Run directory: {runDir}");
        Console.WriteLine($"Run tag prefix: {tagger.RunPrefix}");

        string vmName = tagger.Name("vm1");
        AppHost? host = null;
        IReadOnlyList<string> preRunVms = Array.Empty<string>();

        try
        {
            // 1. Wipe any leftovers from a prior crashed run before we start.
            var preSweep = sweeper.SweepAll();
            Console.WriteLine("Pre-run sweep:\n" + preSweep);

            // Snapshot ALL existing VM names as a backstop: if the deploy creates a
            // VM the harness cannot tag (e.g. the UI reverts our tagged name), the
            // tagged sweep misses it, but this pre/post diff still catches it. A failure
            // here throws into the outer catch (Crash finding) rather than proceeding with
            // an empty baseline that would silently weaken the post-run untagged-VM check.
            preRunVms = probe.ListVmNames(string.Empty).ToList();

            // 2. Seed the base disk + switch the deploy needs.
            var provider = new DiscoverExistingResourceProvider(tagger, probe, catalog);
            var resources = provider.Provision();
            Console.WriteLine($"Provisioned: switch='{resources.SwitchName}' " +
                $"(created={resources.SwitchCreatedByHarness}), base disk='{resources.BaseDiskDisplayLabel}' " +
                $"at {resources.BaseDiskPath}");
            recorder.Record(new Finding
            {
                Scenario = "orchestrator",
                Step = "provision",
                Severity = FindingSeverity.Info,
                Title = "Seeded deploy resources",
                Detail = $"Switch '{resources.SwitchName}' (created by harness: {resources.SwitchCreatedByHarness}); " +
                         $"base disk '{resources.BaseDiskDisplayLabel}' at {resources.BaseDiskPath}."
            });

            // 3. Launch the app (reads the seeded catalog at startup).
            host = AppHost.Launch(_exePath);
            Console.WriteLine($"Main window ready: \"{host.MainWindow.Title}\"");

            // 4. Drive the deploy and validate.
            var context = new RunContext(host, recorder, _config, _repoRoot);
            var scenario = new QuickDeploySingleVmScenario(resources, vmName, probe);
            try
            {
                scenario.Run(context);
            }
            catch (Exception ex)
            {
                var severity = host.Application.HasExited ? FindingSeverity.Crash : FindingSeverity.Error;
                recorder.RecordFailure(host, scenario.Name, "unhandled", severity,
                    $"Scenario threw {ex.GetType().Name}", ex.Message, ex);
            }
        }
        catch (Exception ex)
        {
            recorder.Record(new Finding
            {
                Scenario = "orchestrator",
                Step = "setup",
                Severity = FindingSeverity.Crash,
                Title = $"Deploy proof failed before/at launch: {ex.GetType().Name}",
                Detail = ex.Message,
                ExceptionType = ex.GetType().Name,
                StackTrace = ex.StackTrace
            });
        }
        finally
        {
            // 5. Always tear down the app first, then every tagged resource. Give the
            // app a moment after it exits so Hyper-V settles the VM state and the OS
            // releases disk handles before the sweep. If the first pass still reports
            // errors (a transient lock or a VM caught mid-transition), wait and sweep
            // again - the no-orphans invariant is non-negotiable.
            host?.Dispose();
            Thread.Sleep(3000);

            var postSweep = sweeper.SweepAll();
            if (postSweep.Errors.Count > 0)
            {
                Console.WriteLine("First teardown pass had errors; retrying after a grace delay...");
                Thread.Sleep(5000);
                postSweep = sweeper.SweepAll();
            }

            Console.WriteLine("Post-run teardown:\n" + postSweep);
            recorder.Record(new Finding
            {
                Scenario = "orchestrator",
                Step = "teardown",
                Severity = postSweep.Errors.Count > 0 ? FindingSeverity.Warning : FindingSeverity.Info,
                Title = "Teardown sweep completed",
                Detail = postSweep.ToString()
            });

            // 6. Prove no orphans: nothing carrying the tag may survive.
            ConfirmNoOrphans(recorder, tagger, probe, appData);

            // 6b. Backstop for the untagged-VM hazard: any VM that exists now but did
            // not before the run is something the deploy created that the tagged sweep
            // could not attribute. Flag it loudly for manual review (we never auto-
            // delete an untagged VM - it might belong to the user).
            ConfirmNoUntaggedNewVms(recorder, probe, preRunVms);

            recorder.Flush();
        }

        Console.WriteLine();
        Console.WriteLine($"Findings: {recorder.Findings.Count} (failures: {recorder.HasFailures})");
        Console.WriteLine($"See: {Path.Combine(runDir, "findings.md")}");
        return recorder;
    }

    private static void ConfirmNoOrphans(
        FindingRecorder recorder, ResourceTagger tagger, HyperVProbe probe, AppDataLocations appData)
    {
        // The no-orphans invariant is non-negotiable, so this final verification must fail CLOSED:
        // if we cannot even enumerate Hyper-V, we cannot assert the environment is clean. Treating a
        // failed listing as "found nothing" would let a wedged vmms (which also fails Remove-VM in the
        // sweep) report a green run while a tagged VM survives. Record an Error and stop instead.
        List<string> orphanVms;
        List<string> orphanSwitches;
        try
        {
            orphanVms = probe.ListVmNames(tagger.GlobalPrefix + "-").ToList();
            orphanSwitches = probe.ListSwitchNames(tagger.GlobalPrefix + "-").ToList();
        }
        catch (Exception ex)
        {
            recorder.Record(new Finding
            {
                Scenario = "orchestrator",
                Step = "no-orphans",
                Severity = FindingSeverity.Error,
                Title = "Cleanup could NOT be verified: Hyper-V enumeration failed",
                Detail = $"Listing tagged VMs/switches threw '{ex.Message}'. The no-orphans invariant " +
                         $"cannot be confirmed, so the run fails closed. Inspect Hyper-V manually for " +
                         $"surviving '{tagger.GlobalPrefix}-' VMs and switches."
            });
            return;
        }

        var orphanDisks = new List<string>();
        var orphanDirs = new List<string>();
        foreach (var root in new[] { appData.DifferencingDiskBasePath, appData.VmBasePath })
        {
            if (Directory.Exists(root))
            {
                orphanDisks.AddRange(Directory.EnumerateFiles(root, tagger.GlobalPrefix + "-*", SearchOption.AllDirectories));
                // Empty tagged folders (Remove-VM leaves the config sub-tree behind) still count
                // as cruft, so a leftover directory is an orphan even with no files under it.
                orphanDirs.AddRange(Directory.EnumerateDirectories(root, tagger.GlobalPrefix + "-*", SearchOption.AllDirectories));
            }
        }

        int total = orphanVms.Count + orphanSwitches.Count + orphanDisks.Count + orphanDirs.Count;
        if (total == 0)
        {
            recorder.Record(new Finding
            {
                Scenario = "orchestrator",
                Step = "no-orphans",
                Severity = FindingSeverity.Info,
                Title = "Cleanup verified: no harness-tagged resources remain",
                Detail = $"No VMs, switches, disk files, or folders with prefix '{tagger.GlobalPrefix}-' survive."
            });
        }
        else
        {
            recorder.Record(new Finding
            {
                Scenario = "orchestrator",
                Step = "no-orphans",
                Severity = FindingSeverity.Error,
                Title = $"Orphaned harness resources survived teardown ({total})",
                Detail = $"VMs: [{string.Join(", ", orphanVms)}]; switches: [{string.Join(", ", orphanSwitches)}]; " +
                         $"disk files: [{string.Join(", ", orphanDisks)}]; folders: [{string.Join(", ", orphanDirs)}]. " +
                         "Manual cleanup required."
            });
        }
    }

    private static void ConfirmNoUntaggedNewVms(
        FindingRecorder recorder, HyperVProbe probe, IReadOnlyList<string> preRunVms)
    {
        // Fail closed for the same reason as ConfirmNoOrphans: an unreadable Hyper-V must not be
        // reported as "no new VMs survived".
        List<string> currentVms;
        try
        {
            currentVms = probe.ListVmNames(string.Empty).ToList();
        }
        catch (Exception ex)
        {
            recorder.Record(new Finding
            {
                Scenario = "orchestrator",
                Step = "no-untagged-vms",
                Severity = FindingSeverity.Error,
                Title = "Could NOT verify untagged VMs: Hyper-V enumeration failed",
                Detail = $"Listing all VMs threw '{ex.Message}'. The run fails closed; inspect Hyper-V " +
                         "manually for VMs the deploy may have created outside the harness tag."
            });
            return;
        }

        var newVms = currentVms
            .Where(v => !preRunVms.Contains(v, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (newVms.Count == 0)
        {
            recorder.Record(new Finding
            {
                Scenario = "orchestrator",
                Step = "no-untagged-vms",
                Severity = FindingSeverity.Info,
                Title = "No new VMs survived the run",
                Detail = "The set of Hyper-V VMs is identical to the pre-run snapshot."
            });
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = "orchestrator",
            Step = "no-untagged-vms",
            Severity = FindingSeverity.Error,
            Title = $"Untagged VM(s) survived the run ({newVms.Count})",
            Detail = $"These VMs did not exist before the run and were not swept: [{string.Join(", ", newVms)}]. " +
                     "They were likely created by the deploy with a name the harness could not tag. " +
                     "Review and remove manually - the harness never auto-deletes an untagged VM."
        });
    }
}
