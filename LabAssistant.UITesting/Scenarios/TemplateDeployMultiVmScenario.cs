using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.ResourceProvider;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// A multi-VM end-to-end template deploy: seeds a tagged V2 template containing three
/// standalone Gen2 VMs - each with a DISTINCT memory/cpu and a bare NIC on the harness
/// switch - drives Deploy &gt; From Template to deploy the whole plan in one Start Deploy,
/// then asserts every resulting VM against Hyper-V ground truth. Like the single-VM
/// template scenario it never trusts the UI's success text: each VM must actually exist
/// with the template's own memory, cpu, generation, switch, and a differencing disk.
///
/// The distinct per-VM memory/cpu is the point of this scenario over the single-VM one:
/// it proves the deploy applies each VM's OWN configuration rather than one shared value,
/// and that a multi-VM plan provisions every VM (not just the first). Every VM name and
/// the template file carry the run tag, so cleanup of all three VMs and the file is owned
/// by the harness gate (tag based), not this scenario.
/// </summary>
public sealed class TemplateDeployMultiVmScenario : IScenario
{
    public string Name => "template-deploy-multivm";

    public string Capability => "Deploy";

    public ScenarioRequirements Requirements => ScenarioRequirements.HyperV;

    public void Run(ScenarioContext context)
    {
        var recorder = context.Recorder;
        var hyperV = context.RequireHyperV();
        var resources = hyperV.Provisioned;
        var probe = hyperV.Probe;

        // Seed the tagged multi-VM template BEFORE opening the deploy view. Every VM name and
        // the template file name carry the run prefix (that is what makes each created VM and
        // the file sweepable), and each NIC names the harness switch directly so no guest work
        // is needed for any VM.
        var seeder = new TemplateSeeder(new AppDataLocations());
        SeededMultiVmTemplate seeded = seeder.SeedMultiVmTemplate(
            hyperV.Tagger, resources.BaseDiskCatalogId, resources.SwitchName);

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "seed-template",
            Severity = FindingSeverity.Info,
            Title = $"Seeded multi-VM V2 template '{seeded.TemplateName}' ({seeded.Vms.Count} VMs)",
            Detail = $"File '{seeded.FilePath}', base disk '{resources.BaseDiskCatalogId}', switch " +
                     $"'{resources.SwitchName}'. VMs: " +
                     string.Join("; ", seeded.Vms.Select(v => $"{v.VmName} ({v.ExpectedMemoryMb}MB/{v.ExpectedCpu}cpu)")) + "."
        });

        var page = new DeployFromTemplatePage(context.Host);
        page.Open();
        recorder.Capture(context.Host, "multivm-deploy-opened");

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
                "The From Template selector did not list the seeded multi-VM template even after reloading. " +
                $"Expected file: '{seeded.FilePath}'.");
            return;
        }

        recorder.Capture(context.Host, "multivm-deploy-selected");

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
                    Detail = "The bare-switch multi-VM template should need none; slots were filled defensively so " +
                             "the plan could become startable. The seeded disk never boots, so the values are inert."
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
                "Start Deploy never became enabled for the seeded multi-VM template",
                $"The V2 plan did not reach a startable state. Status: '{page.ActionStatusText()}'. " +
                "This usually means the plan surfaced an unresolved requirement (for example a bootstrap " +
                "profile or credential slot) that the bare-switch multi-VM template was not expected to need.");
            return;
        }

        page.StartDeploy();
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "start-deploy",
            Severity = FindingSeverity.Info,
            Title = $"Start Deploy clicked for {seeded.Vms.Count}-VM template '{seeded.TemplateName}'",
            Detail = $"Base disk '{resources.BaseDiskDisplayLabel}', switch '{resources.SwitchName}'. " +
                     $"Expecting VMs: [{string.Join(", ", seeded.Vms.Select(v => v.VmName))}]."
        });

        // Each VM plans to ProvisionVm -> StartVm (no guest work), so every VM reaching Running
        // means each ProvisionVm (create + switch connect) finished and the ground truth is stable
        // to read. Wait for ALL of them, not just the first.
        var truths = WaitForAllDeployedVms(probe, seeded.Vms.Select(v => v.VmName).ToList(), TimeSpan.FromSeconds(300));
        recorder.Capture(context.Host, "multivm-deploy-after-start");

        var missing = seeded.Vms.Where(v => truths.GetValueOrDefault(v.VmName) is null).Select(v => v.VmName).ToList();
        if (missing.Count > 0)
        {
            recorder.RecordFailure(
                context.Host,
                Name,
                "provision",
                FindingSeverity.Error,
                $"{missing.Count} of {seeded.Vms.Count} template VM(s) did not appear in Hyper-V after Start Deploy",
                $"Missing VMs: [{string.Join(", ", missing)}]. Either provisioning failed for those VMs, or the " +
                "multi-VM deploy stopped short of provisioning every VM in the plan (or the UI reported success " +
                "without creating them). VMs that did appear are still validated below.");
        }

        // Validate every VM that materialized against its OWN expected memory/cpu, so a per-VM
        // config bug surfaces per VM rather than being masked by a single aggregate check.
        foreach (var expected in seeded.Vms)
        {
            var truth = truths.GetValueOrDefault(expected.VmName);
            if (truth is null)
            {
                continue; // already reported as missing above
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

            ValidateVm(context, resources, expected, truth);
        }

        bool anyMismatch = recorder.Findings.Any(f =>
            f.Scenario == Name && f.Step == "validate" && f.Severity == FindingSeverity.Error);

        if (missing.Count == 0 && !anyMismatch)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "validate",
                Severity = FindingSeverity.Info,
                Title = $"All {seeded.Vms.Count} deployed VMs match the template",
                Detail = "Each VM is Gen 2 with its own memory/cpu, attached to switch " +
                         $"'{resources.SwitchName}', on a differencing disk off '{resources.BaseDiskPath}'."
            });
        }
    }

    private void ValidateVm(ScenarioContext context, ProvisionedResources resources, SeededVm expected, VmGroundTruth truth)
    {
        long expectedBytes = (long)expected.ExpectedMemoryMb * 1024 * 1024;

        if (truth.Generation != 2)
        {
            Mismatch(context, expected.VmName, "generation", $"expected Gen 2, got Gen {truth.Generation}");
        }

        if (truth.MemoryStartupBytes != expectedBytes)
        {
            Mismatch(context, expected.VmName, "memory",
                $"expected {expected.ExpectedMemoryMb} MB ({expectedBytes} bytes), got {truth.MemoryStartupBytes} bytes");
        }

        if (truth.ProcessorCount != expected.ExpectedCpu)
        {
            Mismatch(context, expected.VmName, "cpu", $"expected {expected.ExpectedCpu} vCPU, got {truth.ProcessorCount}");
        }

        // The switch is authored INTO each VM's NIC (nic.switchName), not typed into a racy
        // editor, so it is guaranteed to be part of the request. A missing NIC is therefore a
        // real app defect, not a harness-driving limitation - hence Error.
        bool switchAttached = truth.SwitchNames.Any(s =>
            string.Equals(s, resources.SwitchName, StringComparison.OrdinalIgnoreCase));

        if (!switchAttached)
        {
            Mismatch(context, expected.VmName, "switch",
                $"expected switch '{resources.SwitchName}', got [{string.Join(", ", truth.SwitchNames)}]");
        }

        bool differencingOffBase = truth.Disks.Any(d =>
            (!string.IsNullOrEmpty(d.ParentPath) &&
                string.Equals(d.ParentPath, resources.BaseDiskPath, StringComparison.OrdinalIgnoreCase))
            || string.Equals(d.VhdType, "Differencing", StringComparison.OrdinalIgnoreCase));

        if (!differencingOffBase)
        {
            Mismatch(context, expected.VmName, "disk",
                $"expected a differencing disk off '{resources.BaseDiskPath}'; disks: " +
                string.Join(", ", truth.Disks.Select(d => $"{Path.GetFileName(d.Path)}<-{d.ParentPath}({d.VhdType})")));
        }
    }

    private void Mismatch(ScenarioContext context, string vmName, string field, string detail)
        => context.Recorder.RecordFailure(
            context.Host, Name, "validate", FindingSeverity.Error,
            $"Deployed VM '{vmName}' {field} does not match the template", detail);

    private static IReadOnlyDictionary<string, VmGroundTruth> WaitForAllDeployedVms(
        HyperVProbe probe, IReadOnlyList<string> vmNames, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var last = new Dictionary<string, VmGroundTruth>(StringComparer.OrdinalIgnoreCase);

        while (DateTime.UtcNow < deadline)
        {
            bool allRunning = true;
            foreach (var name in vmNames)
            {
                var truth = probe.GetVm(name);
                if (truth is not null)
                {
                    last[name] = truth;
                    if (!string.Equals(truth.State, "Running", StringComparison.OrdinalIgnoreCase))
                    {
                        allRunning = false;
                    }
                }
                else
                {
                    allRunning = false;
                }
            }

            if (allRunning)
            {
                return last;
            }

            Thread.Sleep(3000);
        }

        // Not all VMs reached Running before the deadline. Return whatever was last seen so
        // validation can still report what materialized (and the missing ones are flagged).
        return last;
    }
}
