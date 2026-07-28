using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.ResourceProvider;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// The lean guest-network proof for the SIMPLE case: a single standalone client VM on an Internal
/// lab switch whose only NIC carries a templated STATIC IP. Unlike the bare-switch e2e/multivm
/// scenarios (whose NICs just name the harness switch, so no in-guest work runs) this VM binds its
/// NIC by networkId with a static address, so the V2 plan requires guest work - in-guest static-IP
/// configuration - and therefore a bootstrap-capable base image plus a resolved local bootstrap
/// credential slot. It seeds a tagged template referencing the REAL prepared Windows Server base
/// image, drives Deploy &gt; From Template to plan it, resolves the one credential slot, and asserts
/// the plan becomes startable. That planning path runs with or without a real image password.
///
/// When the base image's local Administrator password is supplied (env var
/// LABASSISTANT_SMOKE_ADMIN_PASSWORD), the scenario goes all the way: Start Deploy, wait for the VM
/// to provision and settle, then authenticate over PowerShell Direct as the local ".\Administrator"
/// and assert the guest actually holds the templated static IP on its adapter - the direct test of
/// the multi-NIC/static-IP mapping fix for the non-DC, non-router case (a static IP stapled to the
/// wrong adapter, or never applied, shows up here as the expected address being absent - the guest
/// falling back to APIPA). The VM and template file carry the run tag so the gate tears them down and
/// proves no orphans; the real base image is only annotated with a bootstrap profile (never deleted).
/// </summary>
public sealed class TemplateDeployGuestStaticScenario : IScenario
{
    // The real prepared Windows Server base image already in the user's catalog. Overridable so the
    // scenario is not pinned to one machine, but defaulted to the known WS2022 image id here.
    private static string RealBaseImageId =>
        Environment.GetEnvironmentVariable("LABASSISTANT_SMOKE_BASE_IMAGE_ID") ?? "b0a5e0222022400080000000000000a1";

    public string Name => "template-deploy-guest-static";

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
                "The guest-static scenario needs a prepared, bootable Windows Server base image registered in the app " +
                "catalog to apply an in-guest static IP. Register the image (Assets > Base disks) or set " +
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

        // 2) Seed the tagged single guest-static template referencing the real image + the gate's Internal switch.
        var seeder = new TemplateSeeder(new AppDataLocations());
        SeededGuestStaticTemplate seeded = seeder.SeedGuestStaticTemplate(hyperV.Tagger, RealBaseImageId, resources.SwitchName);

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "seed-template",
            Severity = FindingSeverity.Info,
            Title = $"Seeded single guest-static V2 template '{seeded.TemplateName}'",
            Detail = $"File '{seeded.FilePath}', VM '{seeded.VmName}', static IP '{seeded.StaticIpAddress}' on switch " +
                     $"'{resources.SwitchName}', image '{RealBaseImageId}'. " +
                     $"Mode: {(live ? "LIVE (deploy + guest IP validation)" : "PLANNING-ONLY (no admin password supplied)")}."
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
        SeededGuestStaticTemplate seeded,
        ProvisionedResources resources,
        bool live)
    {
        // 3) Drive Deploy > From Template: select the template (auto-evaluates the plan).
        var page = new DeployFromTemplatePage(context.Host);
        page.Open();
        recorder.Capture(context.Host, "guest-static-deploy-opened");

        page.ReloadLibrary();
        if (!page.SelectTemplateByName(seeded.TemplateName, TimeSpan.FromSeconds(30)))
        {
            recorder.RecordFailure(
                context.Host, Name, "select-template", FindingSeverity.Error,
                $"Seeded guest-static template '{seeded.TemplateName}' never appeared in the deploy library",
                $"The From Template selector did not list the seeded guest-static template even after reloading. Expected file: '{seeded.FilePath}'.");
            return;
        }

        recorder.Capture(context.Host, "guest-static-deploy-selected");
        page.EvaluatePlan();

        // 4) Resolve the local bootstrap credential slot. In live mode this is the REAL image password
        //    (read from the env var) so the guest step can authenticate; in planning-only mode a
        //    placeholder is enough to prove the plan is startable (the value is never used without a deploy).
        string password = live
            ? Environment.GetEnvironmentVariable(GuestDirectoryProbe.PasswordEnvVar)!
            : "P@ssw0rd!HarnessPlaceholder";

        int resolved = page.ResolveCredentialSlots("Administrator", password, TimeSpan.FromSeconds(30));
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "credentials",
            Severity = FindingSeverity.Info,
            Title = $"Resolved {resolved} credential slot(s) for the guest-static template",
            Detail = live
                ? "Filled the local bootstrap slot with the real image admin password (from the env var)."
                : "Filled the local bootstrap slot with a PLACEHOLDER to prove startability. A live run overwrites it with the real password."
        });
        page.EvaluatePlan();

        // 5) CORE ASSERTION (both modes): a static-IP guest-work template must reach a startable plan.
        if (!page.WaitForStartEnabled(TimeSpan.FromSeconds(60)))
        {
            recorder.RecordFailure(
                context.Host, Name, "readiness", FindingSeverity.Error,
                "Start Deploy never became enabled for the guest-static template",
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
            Title = "Single guest-static plan is startable",
            Detail = "The UI accepted a static-IP guest-work template end to end: import, plan evaluation, credential-slot " +
                     "resolution, and a startable plan (in-guest static-IP config planned)."
        });
        recorder.Capture(context.Host, "guest-static-deploy-startable");

        if (!live)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "gated",
                Severity = FindingSeverity.Warning,
                Title = "Live guest-static deploy + IP validation skipped: no admin password supplied",
                Detail = $"Set the '{GuestDirectoryProbe.PasswordEnvVar}' environment variable to the real base-image " +
                         "local Administrator password before running, and the scenario will Start Deploy, wait for the " +
                         "VM to settle, and validate over PowerShell Direct that the guest holds the templated static IP " +
                         $"'{seeded.StaticIpAddress}'. The plan was proven startable, but no VM was created (no orphan risk)."
            });
            return;
        }

        // 6) LIVE: start the deploy, wait for the VM to provision, then validate the guest IP.
        page.StartDeploy();
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "start-deploy",
            Severity = FindingSeverity.Info,
            Title = $"Start Deploy clicked for guest-static template '{seeded.TemplateName}'",
            Detail = $"Applying static IP '{seeded.StaticIpAddress}' in guest '{seeded.VmName}' (image '{RealBaseImageId}', switch '{resources.SwitchName}')."
        });

        if (!WaitForVmRunning(probe, seeded.VmName, TimeSpan.FromMinutes(6)))
        {
            recorder.RecordFailure(
                context.Host, Name, "provision", FindingSeverity.Error,
                $"VM '{seeded.VmName}' did not reach Running after Start Deploy",
                "The VM never appeared in Hyper-V in a Running state within the timeout, so provisioning or start " +
                "failed before network configuration could begin. Check the app's deploy logs.");
            return;
        }

        recorder.Capture(context.Host, "guest-static-deploy-vm-running");
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "provision",
            Severity = FindingSeverity.Info,
            Title = $"VM '{seeded.VmName}' is Running; waiting for its static-IP config",
            Detail = "The VM provisioned and started. In-guest static-IP configuration (with a possible reboot) can take " +
                     "a few minutes; the guest probe polls PowerShell Direct until the guest answers."
        });

        // 7) THE PRIZE: authenticate into the guest and read the real IPv4 addresses it holds. Abort
        //    the poll the moment the app rolls the VM back (a failed guest-config step deletes the VM),
        //    so a failed deploy fails fast instead of polling a deleted VM for the timeout.
        var guest = new GuestNetworkProbe();
        IReadOnlyList<string>? addresses = guest.QueryIpv4Addresses(
            seeded.VmName,
            seeded.StaticIpAddress,
            TimeSpan.FromMinutes(10),
            abortIf: () => VmIsGone(probe, seeded.VmName));

        if (addresses is null)
        {
            bool rolledBack = VmIsGone(probe, seeded.VmName);
            if (rolledBack)
            {
                recorder.RecordFailure(
                    context.Host, Name, "validate-guest", FindingSeverity.Error,
                    $"Deploy failed and the app rolled back VM '{seeded.VmName}' before network config completed",
                    "The VM was Running but then disappeared, which means a deploy step failed and the app tore the VM " +
                    "down (cleanup worked - no orphan). This is a deploy failure inside the app, not a validation timeout: " +
                    "check the app diagnostics log (%APPDATA%\\LabAssistant\\Logs\\structured-events.jsonl) for the failing " +
                    "deploy.step (the guest-network steps around v2.prepareGuestNetwork are the usual culprit).");
                return;
            }

            recorder.RecordFailure(
                context.Host, Name, "validate-guest", FindingSeverity.Error,
                $"The guest never answered over PowerShell Direct on '{seeded.VmName}'",
                $"The VM is still present but could not read Get-NetIPAddress over PowerShell Direct as '.\\Administrator' " +
                "within the timeout. Either configuration is still in progress past the timeout, or the supplied admin " +
                "password does not match the base image. See the console output for the last PowerShell Direct error.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "validate-guest",
            Severity = FindingSeverity.Info,
            Title = $"VM '{seeded.VmName}' reports IPv4 addresses: {string.Join(", ", addresses)}",
            Detail = "Read live from the guest over PowerShell Direct via Get-NetIPAddress."
        });

        bool staticIpPresent = addresses.Any(a => string.Equals(a, seeded.StaticIpAddress, StringComparison.OrdinalIgnoreCase));
        if (staticIpPresent)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "validate-guest",
                Severity = FindingSeverity.Info,
                Title = $"GUEST VALIDATION PASSED: VM holds the templated static IP '{seeded.StaticIpAddress}'",
                Detail = $"The static IP '{seeded.StaticIpAddress}' is present on the guest, so the templated address landed on " +
                         "the correct adapter (no APIPA fallback). Validated against the running guest, not the UI."
            });
        }
        else
        {
            recorder.RecordFailure(
                context.Host, Name, "validate-guest", FindingSeverity.Error,
                $"Guest is missing the templated static IP '{seeded.StaticIpAddress}'",
                $"Expected the static IP '{seeded.StaticIpAddress}' on the VM, but the guest reported only: " +
                $"{string.Join(", ", addresses)}. A missing static IP points at the static-address mapping (the address was " +
                "stapled to the wrong adapter, or guest network config did not apply, leaving the guest on APIPA/DHCP).");
        }
    }

    private static bool WaitForVmRunning(HyperVProbe probe, string vmName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var truth = probe.GetVm(vmName);
            if (truth is not null && string.Equals(truth.State, "Running", StringComparison.OrdinalIgnoreCase))
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
