using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// A planning-level regression for the switch-auto-create deploy bug: a template that references a
/// virtual switch which does not yet exist on the host should still be deployable - the deploy is
/// meant to CREATE the missing switch - but today planning refuses it with a "switch-reference-missing"
/// blocker, so the deploy never starts.
///
/// The scenario seeds a tagged minimal standalone template whose only NIC names a switch the harness
/// deliberately does NOT provision (a run-tagged ghost name, guaranteed absent from the host), drives
/// Deploy &gt; From Template to plan it, and asserts the plan becomes STARTABLE. Nothing else can block
/// this plan: the standalone fixture has no domain, no topology role, no credential slots, and a
/// bootable-agnostic bare disk, so the missing switch is the sole variable.
///
/// Expected outcome:
///   - On master (bug present): Start Deploy never enables -&gt; this records an Error finding that
///     documents the bug. The harness is not run in CI, so a red finding here is the intended signal.
///   - After the deploy-network fix lands (planning derives a create requirement for a referenced-but-
///     absent Internal/Private switch instead of blocking): Start Deploy enables -&gt; this records a
///     success finding.
///
/// This is a planning-only assertion: it never clicks Start Deploy, so no VM and no switch are created.
/// The seeded template file carries the run tag and is swept by the gate; even if a future live variant
/// were to create the tagged switch, the sweeper removes tag-owned switches by prefix, so there is no
/// orphan risk.
/// </summary>
public sealed class SwitchAutoCreateScenario : IScenario
{
    public string Name => "switch-auto-create";

    public string Capability => "Deploy";

    public ScenarioRequirements Requirements => ScenarioRequirements.HyperV;

    public void Run(ScenarioContext context)
    {
        var recorder = context.Recorder;
        var hyperV = context.RequireHyperV();
        var resources = hyperV.Provisioned;

        // A run-tagged switch name the harness never provisions: it cannot exist on the host, so it is
        // exactly the "referenced but absent" switch the deploy is supposed to auto-create.
        string ghostSwitch = hyperV.Tagger.Name("ghostsw");

        var seeder = new TemplateSeeder(new AppDataLocations());
        SeededTemplate seeded = seeder.SeedStandaloneTemplate(hyperV.Tagger, resources.BaseDiskCatalogId, ghostSwitch);

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "seed-template",
            Severity = FindingSeverity.Info,
            Title = $"Seeded standalone template '{seeded.TemplateName}' referencing absent switch '{ghostSwitch}'",
            Detail = $"File '{seeded.FilePath}', VM '{seeded.VmName}'. The NIC names a switch that is deliberately NOT " +
                     "provisioned, so the plan is startable only if the deploy is willing to create the missing switch."
        });

        // Drive Deploy > From Template: select the template (auto-evaluates the plan).
        var page = new DeployFromTemplatePage(context.Host);
        page.Open();
        recorder.Capture(context.Host, "switch-autocreate-opened");

        page.ReloadLibrary();
        if (!page.SelectTemplateByName(seeded.TemplateName, TimeSpan.FromSeconds(30)))
        {
            recorder.RecordFailure(
                context.Host, Name, "select-template", FindingSeverity.Error,
                $"Seeded template '{seeded.TemplateName}' never appeared in the deploy library",
                $"The From Template selector did not list the seeded template even after reloading. Expected file: '{seeded.FilePath}'.");
            return;
        }

        recorder.Capture(context.Host, "switch-autocreate-selected");
        page.EvaluatePlan();

        // The standalone template needs no credentials; this is defensive only.
        page.ResolveCredentialSlots("Administrator", "P@ssw0rd!HarnessPlaceholder", TimeSpan.FromSeconds(10));
        page.EvaluatePlan();

        // THE ASSERTION: a template that references a missing switch must still be startable, because
        // the deploy should create the switch.
        if (page.WaitForStartEnabled(TimeSpan.FromSeconds(45)))
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "readiness",
                Severity = FindingSeverity.Info,
                Title = $"Plan is startable despite the missing switch '{ghostSwitch}' (auto-create fix present)",
                Detail = "Planning accepted a template referencing a switch that does not exist on the host, so the deploy " +
                         "is prepared to create it. This is the fixed behavior; the switch-auto-create bug is not present."
            });
            recorder.Capture(context.Host, "switch-autocreate-startable");
            return;
        }

        recorder.RecordFailure(
            context.Host, Name, "readiness", FindingSeverity.Error,
            $"Deploy is blocked because switch '{ghostSwitch}' does not exist - the deploy should auto-create it",
            $"Start Deploy never enabled for a template whose only unmet requirement is a missing virtual switch. Status: " +
            $"'{page.ActionStatusText()}'. Per the deploy design a referenced-but-absent Internal/Private switch should be " +
            "created by the deploy, not block planning. This finding is EXPECTED on master and documents the switch-auto-" +
            "create bug; it flips to a pass once the deploy-network fix (planning derives a switch-creation requirement " +
            "instead of a 'switch-reference-missing' blocker) lands.");
    }
}
