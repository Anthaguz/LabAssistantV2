using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.ResourceProvider;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// The first true end-to-end template deploy: seeds a tagged minimal standalone V2
/// template on disk that references the harness-provisioned base disk and switch,
/// drives Deploy &gt; From Template to select, plan, and deploy it, then asserts the
/// resulting VM against Hyper-V ground truth. Like the Quick Deploy scenario it
/// never trusts the UI's success text - the VM must actually exist with the
/// template's memory, cpu, generation, switch, and differencing disk.
///
/// Unlike Quick Deploy, the VM name and switch are baked into the template at seed
/// time rather than typed into a debounced editor, so they are guaranteed to commit;
/// the switch is therefore a REQUIRED part of the template and a missing NIC is an
/// app defect (Error), not a harness-driving limitation (Warning). Cleanup of both
/// the created VM and the seeded template file is owned by the harness gate (tag
/// based), not this scenario.
/// </summary>
public sealed class TemplateDeployV2Scenario : IScenario
{
    // Baked into the fixture; kept here as the validation ground truth.
    private const int ExpectedMemoryMb = 2048;
    private const int ExpectedCpu = 2;

    public string Name => "template-deploy-v2";

    public string Capability => "Deploy";

    public ScenarioRequirements Requirements => ScenarioRequirements.HyperV;

    public void Run(ScenarioContext context)
    {
        var recorder = context.Recorder;
        var hyperV = context.RequireHyperV();
        var resources = hyperV.Provisioned;
        var probe = hyperV.Probe;

        // Seed the tagged template BEFORE opening the deploy view. The VM name and template
        // file name both carry the run prefix (that is what makes the created VM and the file
        // sweepable), and the NIC names the harness switch directly so no guest work is needed.
        var seeder = new TemplateSeeder(new AppDataLocations());
        SeededTemplate seeded = seeder.SeedStandaloneTemplate(
            hyperV.Tagger, resources.BaseDiskCatalogId, resources.SwitchName);

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "seed-template",
            Severity = FindingSeverity.Info,
            Title = $"Seeded V2 template '{seeded.TemplateName}'",
            Detail = $"File '{seeded.FilePath}', VM '{seeded.VmName}', base disk " +
                     $"'{resources.BaseDiskCatalogId}', switch '{resources.SwitchName}'."
        });

        var page = new DeployFromTemplatePage(context.Host);
        page.Open();
        recorder.Capture(context.Host, "template-deploy-opened");

        // The template file was written post-launch, so the library must be reloaded before it
        // appears; SelectTemplateByName also reloads-and-retries until it shows or times out.
        page.ReloadLibrary();
        if (!page.SelectTemplateByName(seeded.TemplateName, TimeSpan.FromSeconds(30)))
        {
            recorder.RecordFailure(
                context.Host,
                Name,
                "select-template",
                FindingSeverity.Error,
                $"Seeded template '{seeded.TemplateName}' never appeared in the deploy library",
                "The From Template selector did not list the seeded template even after reloading. " +
                $"Expected file: '{seeded.FilePath}'.");
            return;
        }

        recorder.Capture(context.Host, "template-deploy-selected");

        // Selecting a V2 template auto-evaluates its plan; nudge it explicitly too in case the
        // selection did not (re)trigger evaluation, then defensively resolve any credential slots.
        page.EvaluatePlan();
        if (!page.CanStartDeploy())
        {
            int resolved = page.ResolveCredentialSlots("labadmin", "P@ssw0rd!Harness", TimeSpan.FromSeconds(20));
            if (resolved > 0)
            {
                recorder.Record(new Finding
                {
                    Scenario = Name,
                    Step = "credentials",
                    Severity = FindingSeverity.Info,
                    Title = $"Resolved {resolved} credential slot(s) with throwaway values",
                    Detail = "The minimal template should need none; slots were filled defensively so the " +
                             "plan could become startable. The seeded disk never boots, so the values are inert."
                });
                page.EvaluatePlan();
            }
        }

        if (!page.WaitForStartEnabled(TimeSpan.FromSeconds(60)))
        {
            recorder.RecordFailure(
                context.Host,
                Name,
                "readiness",
                FindingSeverity.Error,
                "Start Deploy never became enabled for the seeded V2 template",
                $"The V2 plan did not reach a startable state. Status: '{page.ActionStatusText()}'. " +
                "This usually means the plan surfaced an unresolved requirement (for example a bootstrap " +
                "profile or credential slot) that the minimal standalone template was not expected to need.");
            return;
        }

        page.StartDeploy();
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "start-deploy",
            Severity = FindingSeverity.Info,
            Title = $"Start Deploy clicked for template VM '{seeded.VmName}'",
            Detail = $"Base disk '{resources.BaseDiskDisplayLabel}', switch '{resources.SwitchName}'."
        });

        // The minimal standalone template plans to ProvisionVm -> StartVm (no guest work), so the
        // VM reaching Running means ProvisionVm (create + switch connect) finished and the ground
        // truth is stable to read - identical terminal semantics to the Quick Deploy scenario.
        var truth = WaitForDeployedVm(probe, seeded.VmName, TimeSpan.FromSeconds(180));
        recorder.Capture(context.Host, "template-deploy-after-start");

        if (truth is null)
        {
            recorder.RecordFailure(
                context.Host,
                Name,
                "provision",
                FindingSeverity.Error,
                $"VM '{seeded.VmName}' did not appear in Hyper-V after Start Deploy",
                "The deploy reported no VM within the timeout. Either provisioning failed or the UI " +
                "reported success without creating the VM.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "provision",
            Severity = FindingSeverity.Info,
            Title = $"VM '{truth.Name}' exists in Hyper-V (state {truth.State})",
            Detail = $"Gen {truth.Generation}, {truth.MemoryStartupBytes / (1024 * 1024)} MB, " +
                     $"{truth.ProcessorCount} vCPU, switches [{string.Join(", ", truth.SwitchNames)}]."
        });

        ValidateAgainstTemplate(context, resources, truth);
    }

    private void ValidateAgainstTemplate(ScenarioContext context, ProvisionedResources resources, VmGroundTruth truth)
    {
        var recorder = context.Recorder;
        long expectedBytes = (long)ExpectedMemoryMb * 1024 * 1024;

        if (truth.Generation != 2)
        {
            Mismatch(context, "generation", $"expected Gen 2, got Gen {truth.Generation}");
        }

        if (truth.MemoryStartupBytes != expectedBytes)
        {
            Mismatch(context, "memory",
                $"expected {ExpectedMemoryMb} MB ({expectedBytes} bytes), got {truth.MemoryStartupBytes} bytes");
        }

        if (truth.ProcessorCount != ExpectedCpu)
        {
            Mismatch(context, "cpu", $"expected {ExpectedCpu} vCPU, got {truth.ProcessorCount}");
        }

        // The switch is authored INTO the template (nic.switchName), not typed into a racy editor,
        // so it is guaranteed to be part of the request. A missing NIC is therefore a real app
        // defect, not a harness-driving limitation - hence Error, unlike the Quick Deploy scenario.
        bool switchAttached = truth.SwitchNames.Any(s =>
            string.Equals(s, resources.SwitchName, StringComparison.OrdinalIgnoreCase));

        if (!switchAttached)
        {
            Mismatch(context, "switch",
                $"expected switch '{resources.SwitchName}', got [{string.Join(", ", truth.SwitchNames)}]");
        }

        bool differencingOffBase = truth.Disks.Any(d =>
            (!string.IsNullOrEmpty(d.ParentPath) &&
                string.Equals(d.ParentPath, resources.BaseDiskPath, StringComparison.OrdinalIgnoreCase))
            || string.Equals(d.VhdType, "Differencing", StringComparison.OrdinalIgnoreCase));

        if (!differencingOffBase)
        {
            Mismatch(context, "disk",
                $"expected a differencing disk off '{resources.BaseDiskPath}'; disks: " +
                string.Join(", ", truth.Disks.Select(d => $"{Path.GetFileName(d.Path)}<-{d.ParentPath}({d.VhdType})")));
        }

        bool anyMismatch = recorder.Findings.Any(f =>
            f.Scenario == Name && f.Step == "validate" && f.Severity == FindingSeverity.Error);

        if (!anyMismatch)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "validate",
                Severity = FindingSeverity.Info,
                Title = "Deployed VM ground truth matches the template",
                Detail = $"Gen 2, {ExpectedMemoryMb} MB, {ExpectedCpu} vCPU, switch " +
                         $"'{resources.SwitchName}', differencing disk verified."
            });
        }
    }

    private void Mismatch(ScenarioContext context, string field, string detail)
        => context.Recorder.RecordFailure(
            context.Host, Name, "validate", FindingSeverity.Error,
            $"Deployed VM {field} does not match the template", detail);

    private static VmGroundTruth? WaitForDeployedVm(HyperVProbe probe, string vmName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        VmGroundTruth? last = null;
        while (DateTime.UtcNow < deadline)
        {
            var truth = probe.GetVm(vmName);
            if (truth is not null)
            {
                last = truth;

                // StartVm is the terminal node for the bare standalone template, so Running means
                // ProvisionVm (create + switch connect + checkpoint disable) has completed.
                if (string.Equals(truth.State, "Running", StringComparison.OrdinalIgnoreCase))
                {
                    return truth;
                }
            }

            Thread.Sleep(3000);
        }

        // Never reached Running (stalled or failed at StartVm). Return whatever we last saw so
        // validation can still report what materialized, rather than nothing.
        return last;
    }
}
