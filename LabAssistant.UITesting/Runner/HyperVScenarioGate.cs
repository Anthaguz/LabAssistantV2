using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.Cleanup;
using LabAssistant.UITesting.Infrastructure.ResourceProvider;

namespace LabAssistant.UITesting.Runner;

/// <summary>
/// Brackets a single Hyper-V scenario with the full no-orphans lifecycle so any
/// scenario - not just Quick Deploy - inherits the same guarantees: sweep prior
/// leftovers, snapshot the pre-run VM set, seed a tagged base disk + switch,
/// and, after the app is disposed, unconditionally tear everything down and
/// prove nothing tagged (or any untagged new VM) survived.
///
/// One gate instance owns exactly one run's tag namespace, so it must not be
/// reused across scenarios: each Hyper-V scenario gets a fresh gate, which is
/// what makes the pre-run VM snapshot and the post-run diff meaningful per
/// scenario and gives an implicit no-orphans checkpoint between scenarios.
///
/// The verification methods fail CLOSED: if Hyper-V cannot be enumerated, the
/// run is reported as un-verifiable (Error), never as clean. A wedged vmms fails
/// both Remove-VM in the sweep AND the verify listing, so a fail-open check would
/// green a run while a tagged VM lived on.
/// </summary>
public sealed class HyperVScenarioGate
{
    private readonly ResourceTagger _tagger;
    private readonly AppDataLocations _appData;
    private readonly HyperVProbe _probe;
    private readonly CatalogSeeder _catalog;
    private readonly TeardownSweeper _sweeper;
    private IReadOnlyList<string> _preRunVms = Array.Empty<string>();

    public HyperVScenarioGate(HarnessConfig config)
    {
        _tagger = ResourceTagger.CreateNew(config.RunTagPrefix);
        _appData = new AppDataLocations();
        _probe = new HyperVProbe();
        _catalog = new CatalogSeeder(_appData);
        _sweeper = new TeardownSweeper(_tagger, _probe, _appData, _catalog);
    }

    /// <summary>The tag namespace for this scenario's run (used for logging/diagnostics).</summary>
    public ResourceTagger Tagger => _tagger;

    /// <summary>
    /// Wipes leftovers from any prior crashed run, snapshots every existing VM as
    /// a backstop against untaggable VMs, then seeds the base disk + switch the
    /// scenario needs. Throwing here is the caller's cue to record a setup crash;
    /// the pre-run snapshot deliberately throws rather than defaulting to an empty
    /// baseline that would silently weaken the post-run untagged-VM check.
    /// </summary>
    public HyperVScenarioResources Prepare(FindingRecorder recorder, string scenarioName)
    {
        var preSweep = _sweeper.SweepAll();
        Console.WriteLine("Pre-run sweep:\n" + preSweep);

        _preRunVms = _probe.ListVmNames(string.Empty).ToList();

        var provider = new DiscoverExistingResourceProvider(_tagger, _probe, _catalog);
        var resources = provider.Provision();
        Console.WriteLine($"Provisioned: switch='{resources.SwitchName}' " +
            $"(created={resources.SwitchCreatedByHarness}), base disk='{resources.BaseDiskDisplayLabel}' " +
            $"at {resources.BaseDiskPath}");

        recorder.Record(new Finding
        {
            Scenario = scenarioName,
            Step = "provision",
            Severity = FindingSeverity.Info,
            Title = "Seeded deploy resources",
            Detail = $"Switch '{resources.SwitchName}' (created by harness: {resources.SwitchCreatedByHarness}); " +
                     $"base disk '{resources.BaseDiskDisplayLabel}' at {resources.BaseDiskPath}."
        });

        return new HyperVScenarioResources(_tagger, _probe, resources);
    }

    /// <summary>
    /// Runs after the app is disposed: tears down every tagged resource (retrying
    /// once on a transient lock) then proves no orphan tagged resource and no new
    /// untagged VM survived. The no-orphans invariant is non-negotiable, so this
    /// always runs in the caller's finally block regardless of scenario outcome.
    /// </summary>
    public void TeardownAndVerify(FindingRecorder recorder, string scenarioName)
    {
        // Give Hyper-V a moment after the app exits so VM state settles and the OS
        // releases disk handles before the sweep.
        Thread.Sleep(3000);

        var postSweep = _sweeper.SweepAll();
        if (postSweep.Errors.Count > 0)
        {
            Console.WriteLine("First teardown pass had errors; retrying after a grace delay...");
            Thread.Sleep(5000);
            postSweep = _sweeper.SweepAll();
        }

        Console.WriteLine("Post-run teardown:\n" + postSweep);
        recorder.Record(new Finding
        {
            Scenario = scenarioName,
            Step = "teardown",
            Severity = postSweep.Errors.Count > 0 ? FindingSeverity.Warning : FindingSeverity.Info,
            Title = "Teardown sweep completed",
            Detail = postSweep.ToString()
        });

        ConfirmNoOrphans(recorder, scenarioName);
        ConfirmNoUntaggedNewVms(recorder, scenarioName);
    }

    private void ConfirmNoOrphans(FindingRecorder recorder, string scenarioName)
    {
        // Fail CLOSED: if we cannot even enumerate Hyper-V, we cannot assert the
        // environment is clean. Treating a failed listing as "found nothing" would
        // let a wedged vmms report a green run while a tagged VM survives.
        List<string> orphanVms;
        List<string> orphanSwitches;
        try
        {
            orphanVms = _probe.ListVmNames(_tagger.GlobalPrefix + "-").ToList();
            orphanSwitches = _probe.ListSwitchNames(_tagger.GlobalPrefix + "-").ToList();
        }
        catch (Exception ex)
        {
            recorder.Record(new Finding
            {
                Scenario = scenarioName,
                Step = "no-orphans",
                Severity = FindingSeverity.Error,
                Title = "Cleanup could NOT be verified: Hyper-V enumeration failed",
                Detail = $"Listing tagged VMs/switches threw '{ex.Message}'. The no-orphans invariant " +
                         $"cannot be confirmed, so the run fails closed. Inspect Hyper-V manually for " +
                         $"surviving '{_tagger.GlobalPrefix}-' VMs and switches."
            });
            return;
        }

        var orphanDisks = new List<string>();
        var orphanDirs = new List<string>();
        foreach (var root in new[] { _appData.DifferencingDiskBasePath, _appData.VmBasePath })
        {
            if (Directory.Exists(root))
            {
                orphanDisks.AddRange(Directory.EnumerateFiles(root, _tagger.GlobalPrefix + "-*", SearchOption.AllDirectories));
                // Empty tagged folders (Remove-VM leaves the config sub-tree behind) still count
                // as cruft, so a leftover directory is an orphan even with no files under it.
                orphanDirs.AddRange(Directory.EnumerateDirectories(root, _tagger.GlobalPrefix + "-*", SearchOption.AllDirectories));
            }
        }

        int total = orphanVms.Count + orphanSwitches.Count + orphanDisks.Count + orphanDirs.Count;
        if (total == 0)
        {
            recorder.Record(new Finding
            {
                Scenario = scenarioName,
                Step = "no-orphans",
                Severity = FindingSeverity.Info,
                Title = "Cleanup verified: no harness-tagged resources remain",
                Detail = $"No VMs, switches, disk files, or folders with prefix '{_tagger.GlobalPrefix}-' survive."
            });
        }
        else
        {
            recorder.Record(new Finding
            {
                Scenario = scenarioName,
                Step = "no-orphans",
                Severity = FindingSeverity.Error,
                Title = $"Orphaned harness resources survived teardown ({total})",
                Detail = $"VMs: [{string.Join(", ", orphanVms)}]; switches: [{string.Join(", ", orphanSwitches)}]; " +
                         $"disk files: [{string.Join(", ", orphanDisks)}]; folders: [{string.Join(", ", orphanDirs)}]. " +
                         "Manual cleanup required."
            });
        }
    }

    private void ConfirmNoUntaggedNewVms(FindingRecorder recorder, string scenarioName)
    {
        // Fail closed for the same reason as ConfirmNoOrphans: an unreadable Hyper-V
        // must not be reported as "no new VMs survived".
        List<string> currentVms;
        try
        {
            currentVms = _probe.ListVmNames(string.Empty).ToList();
        }
        catch (Exception ex)
        {
            recorder.Record(new Finding
            {
                Scenario = scenarioName,
                Step = "no-untagged-vms",
                Severity = FindingSeverity.Error,
                Title = "Could NOT verify untagged VMs: Hyper-V enumeration failed",
                Detail = $"Listing all VMs threw '{ex.Message}'. The run fails closed; inspect Hyper-V " +
                         "manually for VMs the deploy may have created outside the harness tag."
            });
            return;
        }

        var newVms = currentVms
            .Where(v => !_preRunVms.Contains(v, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (newVms.Count == 0)
        {
            recorder.Record(new Finding
            {
                Scenario = scenarioName,
                Step = "no-untagged-vms",
                Severity = FindingSeverity.Info,
                Title = "No new VMs survived the run",
                Detail = "The set of Hyper-V VMs is identical to the pre-run snapshot."
            });
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = scenarioName,
            Step = "no-untagged-vms",
            Severity = FindingSeverity.Error,
            Title = $"Untagged VM(s) survived the run ({newVms.Count})",
            Detail = $"These VMs did not exist before the run and were not swept: [{string.Join(", ", newVms)}]. " +
                     "They were likely created by the deploy with a name the harness could not tag. " +
                     "Review and remove manually - the harness never auto-deletes an untagged VM."
        });
    }
}
