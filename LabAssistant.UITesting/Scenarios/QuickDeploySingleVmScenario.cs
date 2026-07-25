using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.ResourceProvider;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// Drives a single-VM Quick Deploy end to end against a harness-seeded base disk
/// and switch, then asserts the resulting VM against Hyper-V ground truth. It
/// never trusts the UI's success text: the VM must actually exist with the
/// requested memory, cpu, generation, switch, and differencing disk. Cleanup of
/// the created VM is owned by the orchestrator, not this scenario.
/// </summary>
public sealed class QuickDeploySingleVmScenario : IScenario
{
    private readonly ProvisionedResources _resources;
    private readonly string _vmName;
    private readonly HyperVProbe _probe;
    private readonly int _memoryMb;
    private readonly int _cpu;

    public QuickDeploySingleVmScenario(
        ProvisionedResources resources,
        string vmName,
        HyperVProbe probe,
        int memoryMb = 1024,
        int cpu = 2)
    {
        _resources = resources;
        _vmName = vmName;
        _probe = probe;
        _memoryMb = memoryMb;
        _cpu = cpu;
    }

    public string Name => "quick-deploy-single-vm";

    public void Run(RunContext context)
    {
        var recorder = context.Recorder;
        var page = new QuickDeployPage(context.Host);

        page.Open();
        recorder.Capture(context.Host, "quickdeploy-opened");

        page.SelectFirstVm();

        // The Quick Deploy editor is a debounced draft with no Save button and racy
        // row rebuilds, so drive it to readiness with a convergence loop that keeps
        // re-applying whatever the entry is still missing. The tagged VM name must
        // land on the entry (that is the Hyper-V VM name, hence sweepable) before we
        // deploy; if readiness never clears, abort WITHOUT deploying.
        bool ready = page.ConfigureSingleVmAndWaitReady(
            _vmName, _memoryMb, _cpu, "LAT Harness", _resources.SwitchName, TimeSpan.FromSeconds(120));
        recorder.Capture(context.Host, "quickdeploy-configured");

        // Guard: never deploy unless the tagged name actually committed to the row,
        // otherwise the created VM would be untaggable and evade cleanup.
        if (!page.WaitForVmRowNamed(_vmName, TimeSpan.FromSeconds(3)))
        {
            recorder.RecordFailure(
                context.Host,
                Name,
                "name-commit",
                FindingSeverity.Error,
                $"VM name '{_vmName}' never committed to the entry row",
                "The debounced editor draft did not persist the harness-tagged name. Aborting before " +
                "deploy so no untaggable VM is created (it would evade cleanup).");
            return;
        }

        if (!ready)
        {
            recorder.RecordFailure(
                context.Host,
                Name,
                "readiness",
                FindingSeverity.Error,
                "Start Deploy never became enabled after configuring a complete single VM",
                $"Readiness did not clear with base disk '{_resources.BaseDiskDisplayLabel}' and switch " +
                $"'{_resources.SwitchName}' selected. Row state: {page.FirstRowText()}");
            return;
        }

        // The (optional) switch draft is as racy as the other editor fields, so the harness
        // cannot always land it. Capture whether it actually committed to the entry BEFORE
        // deploying so the switch finding can distinguish "harness never attached it" (Warning)
        // from "app dropped a configured switch" (Error).
        bool switchConfigured = page.FirstRowText().Contains(_resources.SwitchName, StringComparison.OrdinalIgnoreCase);

        page.StartDeploy();
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "start-deploy",
            Severity = FindingSeverity.Info,
            Title = $"Start Deploy clicked for '{_vmName}'",
            Detail = $"Base disk '{_resources.BaseDiskDisplayLabel}', switch '{_resources.SwitchName}' " +
                     $"(attached in editor: {switchConfigured})."
        });

        // A bare standalone VM plans to ProvisionVm -> StartVm. ProvisionVm creates the VM
        // (memory + cpu are set atomically by New-VM/Set-VMProcessor) and only THEN connects
        // the switch; StartVm is the terminal node. Waiting for the VM to reach Running before
        // reading ground truth guarantees ProvisionVm finished, so we never validate a
        // half-provisioned VM (which would spuriously report a missing switch).
        var truth = WaitForDeployedVm(TimeSpan.FromSeconds(180));
        recorder.Capture(context.Host, "quickdeploy-after-start");

        if (truth is null)
        {
            recorder.RecordFailure(
                context.Host,
                Name,
                "provision",
                FindingSeverity.Error,
                $"VM '{_vmName}' did not appear in Hyper-V after Start Deploy",
                "The deploy reported no VM within 120s. Either provisioning failed or the UI succeeded " +
                "without creating the VM.");
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

        ValidateAgainstRequest(context, truth, switchConfigured);
    }

    private void ValidateAgainstRequest(RunContext context, VmGroundTruth truth, bool switchConfigured)
    {
        var recorder = context.Recorder;
        long expectedBytes = (long)_memoryMb * 1024 * 1024;

        if (truth.Generation != 2)
        {
            Mismatch(context, "generation", $"expected Gen 2, got Gen {truth.Generation}");
        }

        if (truth.MemoryStartupBytes != expectedBytes)
        {
            Mismatch(context, "memory",
                $"expected {_memoryMb} MB ({expectedBytes} bytes), got {truth.MemoryStartupBytes} bytes");
        }

        if (truth.ProcessorCount != _cpu)
        {
            Mismatch(context, "cpu", $"expected {_cpu} vCPU, got {truth.ProcessorCount}");
        }

        bool switchAttached = truth.SwitchNames.Any(s =>
            string.Equals(s, _resources.SwitchName, StringComparison.OrdinalIgnoreCase));

        if (!switchAttached)
        {
            if (switchConfigured)
            {
                // The harness confirmed the switch in the editor, so a missing NIC is an app defect.
                Mismatch(context, "switch",
                    $"expected switch '{_resources.SwitchName}', got [{string.Join(", ", truth.SwitchNames)}]");
            }
            else
            {
                // The racy editor never committed the optional switch; report it, but do not
                // fail the run over a config the harness could not reliably apply.
                context.Recorder.RecordFailure(
                    context.Host, Name, "validate", FindingSeverity.Warning,
                    "Optional switch was not attached to the deployed VM",
                    $"The harness could not commit switch '{_resources.SwitchName}' through the debounced " +
                    $"Quick Deploy editor before deploy, so the VM has no NIC (switches: " +
                    $"[{string.Join(", ", truth.SwitchNames)}]). This is a harness-driving limitation on an " +
                    "optional field, not a confirmed app defect.");
            }
        }

        bool differencingOffBase = truth.Disks.Any(d =>
            (!string.IsNullOrEmpty(d.ParentPath) &&
                string.Equals(d.ParentPath, _resources.BaseDiskPath, StringComparison.OrdinalIgnoreCase))
            || string.Equals(d.VhdType, "Differencing", StringComparison.OrdinalIgnoreCase));

        if (!differencingOffBase)
        {
            Mismatch(context, "disk",
                $"expected a differencing disk off '{_resources.BaseDiskPath}'; disks: " +
                string.Join(", ", truth.Disks.Select(d => $"{Path.GetFileName(d.Path)}<-{d.ParentPath}({d.VhdType})")));
        }

        bool anyMismatch = recorder.Findings.Any(f =>
            f.Scenario == Name && f.Step == "validate" && f.Severity == FindingSeverity.Error);

        if (!anyMismatch)
        {
            var switchNote = switchAttached
                ? $"switch '{_resources.SwitchName}'"
                : "no switch (optional, not attached)";
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "validate",
                Severity = FindingSeverity.Info,
                Title = "VM ground truth matches the requested configuration",
                Detail = $"Gen 2, {_memoryMb} MB, {_cpu} vCPU, {switchNote}, differencing disk verified."
            });
        }
    }

    private void Mismatch(RunContext context, string field, string detail)
        => context.Recorder.RecordFailure(
            context.Host, Name, "validate", FindingSeverity.Error,
            $"Deployed VM {field} does not match request", detail);

    private VmGroundTruth? WaitForDeployedVm(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        VmGroundTruth? last = null;
        while (DateTime.UtcNow < deadline)
        {
            var truth = _probe.GetVm(_vmName);
            if (truth is not null)
            {
                last = truth;

                // StartVm is the terminal deploy node for a bare standalone VM, so a Running
                // state means ProvisionVm (create + switch connect + checkpoint disable) has
                // completed and the ground truth is stable to validate.
                if (string.Equals(truth.State, "Running", StringComparison.OrdinalIgnoreCase))
                {
                    return truth;
                }
            }

            Thread.Sleep(3000);
        }

        // Never reached Running (deploy stalled or failed at StartVm). Return whatever we last
        // saw so validation can still report what did materialize, rather than nothing.
        return last;
    }
}
