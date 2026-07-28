using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.ResourceProvider;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// The two-VM variant of the guest-static-IP proof: two standalone client VMs on the SAME Internal
/// lab switch, each with a NIC bound by networkId to its OWN distinct static IP. Both VMs reference
/// the REAL prepared Windows Server base image, so the V2 plan requires guest work (in-guest static-IP
/// config) for each and therefore a bootstrap-capable base image plus a resolved local bootstrap
/// credential slot. It seeds a tagged template, drives Deploy &gt; From Template to plan it, resolves
/// the credential slot(s), and asserts the plan becomes startable. That planning path runs with or
/// without a real image password.
///
/// When the base image's local Administrator password is supplied (env var
/// LABASSISTANT_SMOKE_ADMIN_PASSWORD), the scenario goes all the way: Start Deploy, wait for BOTH VMs
/// to provision and settle, then authenticate into EACH over PowerShell Direct as the local
/// ".\Administrator" and assert each guest holds ITS OWN templated static IP - the direct test of
/// per-adapter MAC binding without the router/Default-Switch egress complication. Two VMs on one
/// segment, each getting the right address, is exactly the shape that a swapped/mis-mapped MAC binding
/// would break (a VM ending up with its sibling's address, or none). Every VM and the template file
/// carry the run tag so the gate tears them down and proves no orphans; the real base image is only
/// annotated with a bootstrap profile (never deleted).
/// </summary>
public sealed class TemplateDeployGuestStaticMultiVmScenario : IScenario
{
    // The real prepared Windows Server base image already in the user's catalog. Overridable so the
    // scenario is not pinned to one machine, but defaulted to the known WS2022 image id here.
    private static string RealBaseImageId =>
        Environment.GetEnvironmentVariable("LABASSISTANT_SMOKE_BASE_IMAGE_ID") ?? "b0a5e0222022400080000000000000a1";

    public string Name => "template-deploy-guest-static-multivm";

    public string Capability => "Deploy";

    public ScenarioRequirements Requirements => ScenarioRequirements.HyperV;

    public void Run(ScenarioContext context)
    {
        var recorder = context.Recorder;
        var hyperV = context.RequireHyperV();
        var resources = hyperV.Provisioned;
        var probe = hyperV.Probe;
        bool live = GuestDirectoryProbe.HasAdminPassword;

        // 1) Make the REAL base image guest-configurable: give its existing catalog entry a bootstrap
        //    profile pointing at the local bootstrap slot. This never touches the id/path or deletes
        //    the VHDX, so the gate's tag-based sweep leaves the real image intact.
        var catalog = new CatalogSeeder(new AppDataLocations());
        bool profiled = catalog.EnsureBaseDiskBootstrapProfile(
            RealBaseImageId,
            expectedLocalUser: "Administrator",
            localCredentialSlotRef: TemplateSeeder.DcLocalBootstrapSlotKey,
            guestOsFamily: "WindowsServer",
            guestTransport: "powershell-direct",
            notes: "Harness-authored so the V2 planner accepts guest work (static-IP config) for this image.");

        if (!profiled)
        {
            recorder.RecordFailure(
                context.Host, Name, "seed-bootstrap", FindingSeverity.Error,
                $"Real base image '{RealBaseImageId}' is not registered in the catalog",
                "The guest-static multi-VM scenario needs a prepared, bootable Windows Server base image registered in " +
                "the app catalog to apply an in-guest static IP. Register the image (Assets > Base disks) or set " +
                "LABASSISTANT_SMOKE_BASE_IMAGE_ID to an existing catalog id, then re-run.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "seed-bootstrap",
            Severity = FindingSeverity.Info,
            Title = $"Ensured bootstrap profile on real base image '{RealBaseImageId}'",
            Detail = $"expectedLocalUser 'Administrator', localCredentialSlotRef '{TemplateSeeder.DcLocalBootstrapSlotKey}', " +
                     "guestOsFamily 'WindowsServer', guestTransport 'powershell-direct'. The backing VHDX is untouched."
        });

        // 2) Seed the tagged two-VM guest-static template referencing the real image + the gate's Internal switch.
        var seeder = new TemplateSeeder(new AppDataLocations());
        SeededGuestStaticMultiVmTemplate seeded = seeder.SeedGuestStaticMultiVmTemplate(hyperV.Tagger, RealBaseImageId, resources.SwitchName);

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "seed-template",
            Severity = FindingSeverity.Info,
            Title = $"Seeded two-VM guest-static V2 template '{seeded.TemplateName}' ({seeded.Vms.Count} VMs)",
            Detail = $"File '{seeded.FilePath}', switch '{resources.SwitchName}', image '{RealBaseImageId}'. VMs: " +
                     string.Join("; ", seeded.Vms.Select(v => $"{v.VmName} ({v.StaticIpAddress})")) + ". " +
                     $"Mode: {(live ? "LIVE (deploy + per-VM guest IP validation)" : "PLANNING-ONLY (no admin password supplied)")}."
        });

        // 2b) Clear this scenario's credential slot so the deploy uses the password we enter now, not
        //     a value cached from an earlier run, and restore the prior store verbatim in the finally.
        var credSeeder = new CredentialSlotSeeder(new AppDataLocations());
        var priorSlot = credSeeder.Capture(seeded.LocalBootstrapSlotKey);
        credSeeder.Remove(seeded.LocalBootstrapSlotKey);

        try
        {
            RunDeploy(context, recorder, probe, seeded, resources, live);
        }
        finally
        {
            credSeeder.Restore(priorSlot);
        }
    }

    private void RunDeploy(
        ScenarioContext context,
        FindingRecorder recorder,
        HyperVProbe probe,
        SeededGuestStaticMultiVmTemplate seeded,
        ProvisionedResources resources,
        bool live)
    {
        // 3) Drive Deploy > From Template: select the template (auto-evaluates the plan).
        var page = new DeployFromTemplatePage(context.Host);
        page.Open();
        recorder.Capture(context.Host, "guest-static-multivm-deploy-opened");

        page.ReloadLibrary();
        if (!page.SelectTemplateByName(seeded.TemplateName, TimeSpan.FromSeconds(30)))
        {
            recorder.RecordFailure(
                context.Host, Name, "select-template", FindingSeverity.Error,
                $"Seeded guest-static multi-VM template '{seeded.TemplateName}' never appeared in the deploy library",
                $"The From Template selector did not list the seeded template even after reloading. Expected file: '{seeded.FilePath}'.");
            return;
        }

        recorder.Capture(context.Host, "guest-static-multivm-deploy-selected");
        page.EvaluatePlan();

        // 4) Resolve the local bootstrap credential slot(s). In live mode this is the REAL image
        //    password (from the env var); in planning-only mode a placeholder proves startability.
        //    Both VMs share the same localBootstrap slot ref, so the planner surfaces one slot.
        string password = live
            ? Environment.GetEnvironmentVariable(GuestDirectoryProbe.PasswordEnvVar)!
            : "P@ssw0rd!HarnessPlaceholder";

        int resolved = page.ResolveCredentialSlots("Administrator", password, TimeSpan.FromSeconds(30));
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "credentials",
            Severity = FindingSeverity.Info,
            Title = $"Resolved {resolved} credential slot(s) for the guest-static multi-VM template",
            Detail = live
                ? "Filled the local bootstrap slot with the real image admin password (from the env var)."
                : "Filled the local bootstrap slot with a PLACEHOLDER to prove startability. A live run overwrites it with the real password."
        });
        page.EvaluatePlan();

        // 5) CORE ASSERTION (both modes): a two-VM static-IP template must reach a startable plan.
        if (!page.WaitForStartEnabled(TimeSpan.FromSeconds(60)))
        {
            recorder.RecordFailure(
                context.Host, Name, "readiness", FindingSeverity.Error,
                "Start Deploy never became enabled for the guest-static multi-VM template",
                $"The V2 plan did not reach a startable state. Status: '{page.ActionStatusText()}'. A static-IP client " +
                "template requires a bootstrap-capable base image (seeded above) and a resolved local bootstrap " +
                "credential slot; one of those requirements is still unmet.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "readiness",
            Severity = FindingSeverity.Info,
            Title = "Two-VM guest-static plan is startable",
            Detail = "The UI accepted a two-VM static-IP guest-work template end to end: import, plan evaluation, " +
                     "credential-slot resolution, and a startable plan (per-VM in-guest static-IP config planned)."
        });
        recorder.Capture(context.Host, "guest-static-multivm-deploy-startable");

        if (!live)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "gated",
                Severity = FindingSeverity.Warning,
                Title = "Live guest-static multi-VM deploy + IP validation skipped: no admin password supplied",
                Detail = $"Set the '{GuestDirectoryProbe.PasswordEnvVar}' environment variable to the real base-image " +
                         "local Administrator password before running, and the scenario will Start Deploy, wait for both " +
                         "VMs to settle, and validate over PowerShell Direct that EACH guest holds its own templated static " +
                         $"IP ([{string.Join(", ", seeded.Vms.Select(v => v.StaticIpAddress))}]). The plan was proven " +
                         "startable, but no VMs were created (no orphan risk)."
            });
            return;
        }

        // 6) LIVE: start the deploy, wait for BOTH VMs to provision, then validate each guest IP.
        page.StartDeploy();
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "start-deploy",
            Severity = FindingSeverity.Info,
            Title = $"Start Deploy clicked for {seeded.Vms.Count}-VM guest-static template '{seeded.TemplateName}'",
            Detail = $"Applying per-VM static IPs on switch '{resources.SwitchName}' (image '{RealBaseImageId}'). " +
                     $"Expecting: [{string.Join(", ", seeded.Vms.Select(v => $"{v.VmName}={v.StaticIpAddress}"))}]."
        });

        // Wait for EVERY VM to reach Running before probing any guest, so a slow second VM does not
        // get misreported as a failure just because the first settled first.
        var vmNames = seeded.Vms.Select(v => v.VmName).ToList();
        if (!WaitForAllVmsRunning(probe, vmNames, TimeSpan.FromMinutes(8)))
        {
            var notRunning = vmNames.Where(n =>
            {
                var t = probe.GetVm(n);
                return t is null || !string.Equals(t.State, "Running", StringComparison.OrdinalIgnoreCase);
            }).ToList();

            recorder.RecordFailure(
                context.Host, Name, "provision", FindingSeverity.Error,
                $"{notRunning.Count} of {seeded.Vms.Count} guest-static VM(s) did not reach Running after Start Deploy",
                $"Not-Running VMs: [{string.Join(", ", notRunning)}]. Either provisioning failed for those VMs or the app " +
                "rolled one back before network config completed. Check the app's deploy logs.");
            return;
        }

        recorder.Capture(context.Host, "guest-static-multivm-deploy-vms-running");
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "provision",
            Severity = FindingSeverity.Info,
            Title = $"All {seeded.Vms.Count} VMs are Running; waiting for each guest's static-IP config",
            Detail = "Every VM provisioned and started. Per-VM in-guest static-IP configuration (with a possible reboot) " +
                     "can take a few minutes; the guest probe polls PowerShell Direct on each until it answers."
        });

        // 7) THE PRIZE: validate EACH guest holds ITS OWN templated static IP. A per-VM failure is
        //    recorded per VM so a swapped/mis-mapped address surfaces on the exact VM that lost it.
        var guest = new GuestNetworkProbe();
        int passed = 0;
        foreach (var vm in seeded.Vms)
        {
            IReadOnlyList<string>? addresses = guest.QueryIpv4Addresses(
                vm.VmName,
                vm.StaticIpAddress,
                TimeSpan.FromMinutes(10),
                abortIf: () => VmIsGone(probe, vm.VmName));

            if (addresses is null)
            {
                bool rolledBack = VmIsGone(probe, vm.VmName);
                recorder.RecordFailure(
                    context.Host, Name, "validate-guest", FindingSeverity.Error,
                    rolledBack
                        ? $"Deploy failed and the app rolled back VM '{vm.VmName}' before network config completed"
                        : $"Guest '{vm.VmName}' never answered over PowerShell Direct",
                    rolledBack
                        ? "The VM was Running but then disappeared, so a deploy step failed and the app tore it down " +
                          "(cleanup worked - no orphan). Check the app diagnostics log for the failing deploy.step."
                        : $"Could not read Get-NetIPAddress over PowerShell Direct as '.\\Administrator' on '{vm.VmName}' " +
                          "within the timeout. Configuration may still be in progress, or the admin password does not match " +
                          "the base image. See the console output for the last PowerShell Direct error.");
                continue;
            }

            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "validate-guest",
                Severity = FindingSeverity.Info,
                Title = $"VM '{vm.VmName}' reports IPv4 addresses: {string.Join(", ", addresses)}",
                Detail = "Read live from the guest over PowerShell Direct via Get-NetIPAddress."
            });

            bool ownIpPresent = addresses.Any(a => string.Equals(a, vm.StaticIpAddress, StringComparison.OrdinalIgnoreCase));

            // A sibling's address landing here is the specific MAC-mapping failure this scenario hunts.
            var siblingIp = seeded.Vms.FirstOrDefault(o =>
                !string.Equals(o.VmName, vm.VmName, StringComparison.OrdinalIgnoreCase)
                && addresses.Any(a => string.Equals(a, o.StaticIpAddress, StringComparison.OrdinalIgnoreCase)));

            if (ownIpPresent)
            {
                passed++;
                recorder.Record(new Finding
                {
                    Scenario = Name,
                    Step = "validate-guest",
                    Severity = FindingSeverity.Info,
                    Title = $"GUEST VALIDATION PASSED: VM '{vm.VmName}' holds its own static IP '{vm.StaticIpAddress}'",
                    Detail = $"The templated static IP '{vm.StaticIpAddress}' is present on '{vm.VmName}', so its address " +
                             "landed on the correct adapter of the correct VM. Validated against the running guest, not the UI."
                });
            }
            else
            {
                recorder.RecordFailure(
                    context.Host, Name, "validate-guest", FindingSeverity.Error,
                    $"VM '{vm.VmName}' is missing its templated static IP '{vm.StaticIpAddress}'",
                    $"Expected '{vm.StaticIpAddress}' on '{vm.VmName}', but the guest reported only: [{string.Join(", ", addresses)}]." +
                    (siblingIp is not null
                        ? $" It instead holds sibling '{siblingIp.VmName}'s address '{siblingIp.StaticIpAddress}', which is the " +
                          "exact per-adapter MAC-mapping bug this scenario hunts (static IPs swapped across VMs)."
                        : " The static IP was stapled to the wrong adapter, or guest network config did not apply (APIPA/DHCP fallback)."));
            }
        }

        if (passed == seeded.Vms.Count)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "validate-guest",
                Severity = FindingSeverity.Info,
                Title = $"ALL {seeded.Vms.Count} VMs hold their own templated static IP",
                Detail = "Every VM on the shared Internal segment ended up with exactly its own address, proving per-adapter " +
                         "MAC binding across a multi-VM static-IP deploy."
            });
        }
    }

    private static bool WaitForAllVmsRunning(HyperVProbe probe, IReadOnlyList<string> vmNames, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            bool allRunning = vmNames.All(name =>
            {
                var truth = probe.GetVm(name);
                return truth is not null && string.Equals(truth.State, "Running", StringComparison.OrdinalIgnoreCase);
            });

            if (allRunning)
            {
                return true;
            }

            Thread.Sleep(3000);
        }

        return false;
    }

    /// <summary>
    /// True when the VM no longer exists. Swallows a transient inventory-probe failure by returning
    /// false, so a momentary Get-VM hiccup during the poll never aborts it or gets misread as a
    /// rollback; a genuine rollback (VM removed) reports true.
    /// </summary>
    private static bool VmIsGone(HyperVProbe probe, string vmName)
    {
        try
        {
            return probe.GetVm(vmName) is null;
        }
        catch
        {
            return false;
        }
    }
}
