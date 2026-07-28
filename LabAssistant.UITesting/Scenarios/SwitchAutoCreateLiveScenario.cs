using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// The runtime half of the switch-auto-create regression: unlike the planning-only
/// <see cref="SwitchAutoCreateScenario"/> (which asserts the plan is startable but never clicks Start,
/// so it never proves the switch is actually created), this scenario STARTS the deploy so the runtime
/// creates the referenced-but-absent switch, then asserts against Get-VMSwitch that the created switch
/// is of type Internal - the guarantee the planning-only verb cannot give.
///
/// It seeds a tagged minimal standalone template whose only NIC names a run-tagged switch the harness
/// deliberately does NOT provision (guaranteed absent from the host), drives Deploy &gt; From Template,
/// and - if the plan is startable - clicks Start Deploy and polls Get-VMSwitch until the ghost switch
/// appears, asserting its SwitchType is Internal. The ghost switch name carries the run prefix, so the
/// gate's tag-based switch sweep removes it afterwards (no orphan), exactly like the VM.
///
/// Expected outcome:
///   - On master (bug present): the plan never becomes startable, so Start is never clicked. This
///     records an Error finding that documents the bug (identical signal to the planning-only verb).
///   - After the deploy-network fix lands (planning derives a switch-creation requirement for a
///     referenced-but-absent Internal/Private switch): Start Deploy runs, the runtime creates the
///     switch, and this asserts it is Internal - the runtime proof.
///
/// The base disk is the gate's tiny non-bootable disk, so the VM cannot boot and the app rolls it back
/// after provisioning; the switch is created during provisioning (before guest work), so it is asserted
/// even when the VM is subsequently torn down. The gate then sweeps the tagged VM and switch and proves
/// no orphans.
/// </summary>
public sealed class SwitchAutoCreateLiveScenario : IScenario
{
    public string Name => "switch-auto-create-live";

    public string Capability => "Deploy";

    public ScenarioRequirements Requirements => ScenarioRequirements.HyperV;

    public void Run(ScenarioContext context)
    {
        var recorder = context.Recorder;
        var hyperV = context.RequireHyperV();
        var resources = hyperV.Provisioned;
        var probe = hyperV.Probe;

        // A run-tagged switch name the harness never provisions: it cannot exist on the host, so it is
        // exactly the "referenced but absent" switch the deploy is supposed to auto-create. Because it
        // carries the run prefix, the gate's switch sweep removes it if the runtime creates it.
        string ghostSwitch = hyperV.Tagger.Name("ghostswlive");

        var seeder = new TemplateSeeder(new AppDataLocations());
        SeededTemplate seeded = seeder.SeedSwitchAutoCreateLiveTemplate(hyperV.Tagger, resources.BaseDiskCatalogId, ghostSwitch);

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "seed-template",
            Severity = FindingSeverity.Info,
            Title = $"Seeded standalone template '{seeded.TemplateName}' referencing absent switch '{ghostSwitch}'",
            Detail = $"File '{seeded.FilePath}', VM '{seeded.VmName}'. The NIC names a switch that is deliberately NOT " +
                     "provisioned; on Start Deploy the runtime must create it, and this scenario asserts it is created as Internal."
        });

        // Guard against a pre-existing switch of that name (should be impossible - it is freshly tagged -
        // but a leftover would make an Internal assertion meaningless). Fail loudly rather than green a
        // stale switch.
        if (probe.GetSwitchType(ghostSwitch) is not null)
        {
            recorder.RecordFailure(
                context.Host, Name, "seed-template", FindingSeverity.Error,
                $"Ghost switch '{ghostSwitch}' already exists before the deploy",
                "The run-tagged switch name was expected to be absent so the deploy would create it, but a switch with " +
                "that name is already present on the host. A prior run may have leaked it; run the sweep verb and retry.");
            return;
        }

        // Drive Deploy > From Template: select the template (auto-evaluates the plan).
        var page = new DeployFromTemplatePage(context.Host);
        page.Open();
        recorder.Capture(context.Host, "switch-autocreate-live-opened");

        page.ReloadLibrary();
        if (!page.SelectTemplateByName(seeded.TemplateName, TimeSpan.FromSeconds(30)))
        {
            recorder.RecordFailure(
                context.Host, Name, "select-template", FindingSeverity.Error,
                $"Seeded template '{seeded.TemplateName}' never appeared in the deploy library",
                $"The From Template selector did not list the seeded template even after reloading. Expected file: '{seeded.FilePath}'.");
            return;
        }

        recorder.Capture(context.Host, "switch-autocreate-live-selected");
        page.EvaluatePlan();

        // The standalone template needs no credentials; this is defensive only.
        page.ResolveCredentialSlots("Administrator", "P@ssw0rd!HarnessPlaceholder", TimeSpan.FromSeconds(10));
        page.EvaluatePlan();

        // On master the plan is blocked because the switch is missing; document the bug and stop before
        // Start (identical signal to the planning-only verb). This flips to the live proof once the fix lands.
        if (!page.WaitForStartEnabled(TimeSpan.FromSeconds(45)))
        {
            recorder.RecordFailure(
                context.Host, Name, "readiness", FindingSeverity.Error,
                $"Deploy is blocked because switch '{ghostSwitch}' does not exist - the deploy should auto-create it",
                $"Start Deploy never enabled for a template whose only unmet requirement is a missing virtual switch. Status: " +
                $"'{page.ActionStatusText()}'. Per the deploy design a referenced-but-absent Internal/Private switch should be " +
                "created by the deploy, not block planning. This finding is EXPECTED on master and documents the switch-auto-" +
                "create bug; it flips to a pass (and the live Internal-switch assertion below runs) once the deploy-network " +
                "fix lands.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "readiness",
            Severity = FindingSeverity.Info,
            Title = $"Plan is startable despite the missing switch '{ghostSwitch}' (auto-create fix present)",
            Detail = "Planning accepted a template referencing a switch that does not exist on the host, so the deploy is " +
                     "prepared to create it. Proceeding to Start Deploy to prove the runtime actually creates it as Internal."
        });
        recorder.Capture(context.Host, "switch-autocreate-live-startable");

        // LIVE: start the deploy so the runtime creates the missing switch during provisioning.
        page.StartDeploy();
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "start-deploy",
            Severity = FindingSeverity.Info,
            Title = $"Start Deploy clicked for switch-auto-create template '{seeded.TemplateName}'",
            Detail = $"The runtime should create the absent switch '{ghostSwitch}' as Internal while provisioning VM '{seeded.VmName}'."
        });

        // Poll for the switch to appear. It is created during provisioning, BEFORE the non-bootable disk
        // fails guest work and the app rolls the VM back, so it is observable even though the VM is
        // eventually torn down. Read its type the moment it exists.
        string? switchType = WaitForSwitch(probe, ghostSwitch, TimeSpan.FromMinutes(3));
        recorder.Capture(context.Host, "switch-autocreate-live-after-start");

        if (switchType is null)
        {
            recorder.RecordFailure(
                context.Host, Name, "validate-switch", FindingSeverity.Error,
                $"Runtime never created the referenced switch '{ghostSwitch}' after Start Deploy",
                "The plan was startable and Start Deploy was clicked, but the referenced-but-absent switch never appeared on " +
                "the host within the timeout. The runtime accepted the plan but did not actually create the switch, so the " +
                "auto-create is only half-implemented (planning allows it, provisioning does not create it). Check the app's " +
                "deploy logs for the switch-creation step.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "validate-switch",
            Severity = FindingSeverity.Info,
            Title = $"Runtime created switch '{ghostSwitch}' (SwitchType={switchType})",
            Detail = "Read live from Hyper-V via Get-VMSwitch, not inferred from the UI's success text."
        });

        if (string.Equals(switchType, "Internal", StringComparison.OrdinalIgnoreCase))
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "validate-switch",
                Severity = FindingSeverity.Info,
                Title = $"RUNTIME VALIDATION PASSED: auto-created switch '{ghostSwitch}' is Internal",
                Detail = "The deploy created the referenced-but-absent switch as an Internal switch, proving the runtime half " +
                         "of the switch-auto-create fix that the planning-only verb cannot. The gate sweeps the tagged switch next."
            });
        }
        else
        {
            recorder.RecordFailure(
                context.Host, Name, "validate-switch", FindingSeverity.Error,
                $"Auto-created switch '{ghostSwitch}' has the wrong type '{switchType}' (expected Internal)",
                $"The runtime created the missing switch, but as '{switchType}' rather than Internal. A lab auto-create should " +
                "produce an Internal switch (host-and-guests, no physical uplink); an External or Private switch would change " +
                "the segment's connectivity. Check the switch-creation step's SwitchType.");
        }
    }

    /// <summary>
    /// Polls Get-VMSwitch until the named switch exists, returning its SwitchType, or null on timeout.
    /// Swallows a transient probe failure so a momentary vmms hiccup does not abort the wait.
    /// </summary>
    private static string? WaitForSwitch(HyperVProbe probe, string switchName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var type = probe.GetSwitchType(switchName);
                if (type is not null)
                {
                    return type;
                }
            }
            catch
            {
                // transient enumeration hiccup; keep polling
            }

            Thread.Sleep(3000);
        }

        return null;
    }
}
