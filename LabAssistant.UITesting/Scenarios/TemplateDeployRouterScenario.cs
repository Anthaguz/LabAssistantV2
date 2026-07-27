using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.ResourceProvider;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// A single-router deploy scenario: the first multi-NIC, egress-routing guest-work topology in the
/// harness. It seeds a tagged V2 template with one standalone Router VM that references the REAL
/// prepared Windows Server base image and has two NICs - a LAN NIC bound by networkId to the gate's
/// Internal switch and holding the segment's .1 gateway (a static IP, so the plan requires guest
/// work), plus an external NIC bridged onto the host Default Switch for egress. It drives Deploy &gt;
/// From Template to plan the router, resolves the one local bootstrap credential slot, and asserts
/// the plan becomes startable - proving the UI accepts a Router topology (RRAS/NAT + multi-NIC static
/// IP) end to end. That planning path runs with or without a real image password.
///
/// When the base image's local Administrator password is supplied (env var
/// LABASSISTANT_SMOKE_ADMIN_PASSWORD), the scenario goes all the way: Start Deploy, wait for the
/// router VM to provision and settle, then authenticate over PowerShell Direct as the local
/// ".\Administrator" and assert the guest actually holds the templated LAN gateway IP - the direct
/// test of the multi-NIC MAC-mapping fix (a static IP stapled to the wrong adapter shows up here as
/// the expected address being absent). The router VM and template file carry the run tag so the gate
/// tears them down and proves no orphans; the real base image is only annotated with a bootstrap
/// profile (never deleted), and the host Default Switch is never touched.
/// </summary>
public sealed class TemplateDeployRouterScenario : IScenario
{
    // The real prepared Windows Server base image already in the user's catalog. Overridable so the
    // scenario is not pinned to one machine, but defaulted to the known WS2022 image id here.
    private static string RealBaseImageId =>
        Environment.GetEnvironmentVariable("LABASSISTANT_SMOKE_BASE_IMAGE_ID") ?? "b0a5e0222022400080000000000000a1";

    public string Name => "template-deploy-router";

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
            notes: "Harness-authored so the V2 planner accepts guest work (router RRAS/NAT) for this image.");

        if (!profiled)
        {
            recorder.RecordFailure(
                context.Host, Name, "seed-bootstrap", FindingSeverity.Error,
                $"Real base image '{RealBaseImageId}' is not registered in the catalog",
                "The router scenario needs a prepared, bootable Windows Server base image registered in the app " +
                "catalog to configure RRAS/NAT. Register the image (Assets > Base disks) or set " +
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

        // 2) Seed the tagged single-router template: LAN NIC on the gate's Internal switch, external
        //    NIC on the host Default Switch.
        var seeder = new TemplateSeeder(new AppDataLocations());
        SeededRouterTemplate seeded = seeder.SeedRouterTemplate(hyperV.Tagger, RealBaseImageId, resources.SwitchName);

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "seed-template",
            Severity = FindingSeverity.Info,
            Title = $"Seeded single-router V2 template '{seeded.TemplateName}'",
            Detail = $"File '{seeded.FilePath}', router VM '{seeded.VmName}', LAN gateway '{seeded.LanIpAddress}' on switch " +
                     $"'{resources.SwitchName}', external NIC on 'Default Switch', image '{RealBaseImageId}'. " +
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
        SeededRouterTemplate seeded,
        ProvisionedResources resources,
        bool live)
    {
        // 3) Drive Deploy > From Template: select the template (auto-evaluates the plan).
        var page = new DeployFromTemplatePage(context.Host);
        page.Open();
        recorder.Capture(context.Host, "router-deploy-opened");

        page.ReloadLibrary();
        if (!page.SelectTemplateByName(seeded.TemplateName, TimeSpan.FromSeconds(30)))
        {
            recorder.RecordFailure(
                context.Host, Name, "select-template", FindingSeverity.Error,
                $"Seeded router template '{seeded.TemplateName}' never appeared in the deploy library",
                $"The From Template selector did not list the seeded router template even after reloading. Expected file: '{seeded.FilePath}'.");
            return;
        }

        recorder.Capture(context.Host, "router-deploy-selected");
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
            Title = $"Resolved {resolved} credential slot(s) for the router template",
            Detail = live
                ? "Filled the local bootstrap slot with the real image admin password (from the env var)."
                : "Filled the local bootstrap slot with a PLACEHOLDER to prove startability. A live run overwrites it with the real password."
        });
        page.EvaluatePlan();

        // 5) CORE ASSERTION (both modes): a Router topology template must reach a startable plan.
        if (!page.WaitForStartEnabled(TimeSpan.FromSeconds(60)))
        {
            recorder.RecordFailure(
                context.Host, Name, "readiness", FindingSeverity.Error,
                "Start Deploy never became enabled for the router template",
                $"The V2 plan did not reach a startable state. Status: '{page.ActionStatusText()}'. A Router template " +
                "requires a bootstrap-capable base image (seeded above), a resolved local bootstrap credential slot, and " +
                "both referenced switches (the gate's Internal switch plus the host Default Switch) to be resolvable; one " +
                "of those requirements is still unmet.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "readiness",
            Severity = FindingSeverity.Info,
            Title = "Single-router plan is startable",
            Detail = "The UI accepted a Router topology template end to end: import, plan evaluation, credential-slot " +
                     "resolution, and a startable plan (RRAS/NAT + multi-NIC static IP planned)."
        });
        recorder.Capture(context.Host, "router-deploy-startable");

        if (!live)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "gated",
                Severity = FindingSeverity.Warning,
                Title = "Live router deploy + IP validation skipped: no admin password supplied",
                Detail = $"Set the '{GuestDirectoryProbe.PasswordEnvVar}' environment variable to the real base-image " +
                         "local Administrator password before running, and the scenario will Start Deploy, wait for the " +
                         "router to settle, and validate over PowerShell Direct that the guest holds the templated LAN " +
                         $"gateway IP '{seeded.LanIpAddress}'. The plan was proven startable, but no VM was created (no orphan risk)."
            });
            return;
        }

        // 6) LIVE: start the deploy, wait for the router VM to provision, then validate the guest IPs.
        page.StartDeploy();
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "start-deploy",
            Severity = FindingSeverity.Info,
            Title = $"Start Deploy clicked for router template '{seeded.TemplateName}'",
            Detail = $"Configuring router '{seeded.VmName}' (image '{RealBaseImageId}', LAN switch '{resources.SwitchName}', external 'Default Switch')."
        });

        if (!WaitForVmRunning(probe, seeded.VmName, TimeSpan.FromMinutes(6)))
        {
            recorder.RecordFailure(
                context.Host, Name, "provision", FindingSeverity.Error,
                $"Router VM '{seeded.VmName}' did not reach Running after Start Deploy",
                "The router VM never appeared in Hyper-V in a Running state within the timeout, so provisioning or start " +
                "failed before network configuration could begin. Check the app's deploy logs.");
            return;
        }

        recorder.Capture(context.Host, "router-deploy-vm-running");
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "provision",
            Severity = FindingSeverity.Info,
            Title = $"Router VM '{seeded.VmName}' is Running; waiting for its network config",
            Detail = "The VM provisioned and started. RRAS/NAT setup and static-IP configuration (with a possible reboot) " +
                     "can take a few minutes; the guest probe polls PowerShell Direct until the guest answers."
        });

        // 7) THE PRIZE: authenticate into the router and read the real IPv4 addresses it holds. Abort
        //    the poll the moment the app rolls the VM back (a failed guest-config step deletes the VM),
        //    so a failed deploy fails fast instead of polling a deleted VM for the timeout.
        var guest = new GuestNetworkProbe();
        IReadOnlyList<string>? addresses = guest.QueryIpv4Addresses(
            seeded.VmName,
            seeded.LanIpAddress,
            TimeSpan.FromMinutes(10),
            abortIf: () => VmIsGone(probe, seeded.VmName));

        if (addresses is null)
        {
            bool rolledBack = VmIsGone(probe, seeded.VmName);
            if (rolledBack)
            {
                recorder.RecordFailure(
                    context.Host, Name, "validate-guest", FindingSeverity.Error,
                    $"Deploy failed and the app rolled back router VM '{seeded.VmName}' before network config completed",
                    "The router VM was Running but then disappeared, which means a deploy step failed and the app tore the VM " +
                    "down (cleanup worked - no orphan). This is a deploy failure inside the app, not a validation timeout: " +
                    "check the app diagnostics log (%APPDATA%\\LabAssistant\\Logs\\structured-events.jsonl) for the failing " +
                    "deploy.step (the guest-network steps around v2.prepareGuestNetwork are the usual culprit).");
                return;
            }

            recorder.RecordFailure(
                context.Host, Name, "validate-guest", FindingSeverity.Error,
                $"The router guest never answered over PowerShell Direct on '{seeded.VmName}'",
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
            Title = $"Router '{seeded.VmName}' reports IPv4 addresses: {string.Join(", ", addresses)}",
            Detail = "Read live from the guest over PowerShell Direct via Get-NetIPAddress."
        });

        bool lanIpPresent = addresses.Any(a => string.Equals(a, seeded.LanIpAddress, StringComparison.OrdinalIgnoreCase));
        if (lanIpPresent)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "validate-guest",
                Severity = FindingSeverity.Info,
                Title = $"GUEST VALIDATION PASSED: router holds the templated LAN gateway '{seeded.LanIpAddress}'",
                Detail = $"The static LAN IP '{seeded.LanIpAddress}' is present on the router guest, so the multi-NIC static " +
                         "address landed on the correct adapter. Validated against the running guest, not the UI."
            });
        }
        else
        {
            recorder.RecordFailure(
                context.Host, Name, "validate-guest", FindingSeverity.Error,
                $"Router guest is missing the templated LAN gateway IP '{seeded.LanIpAddress}'",
                $"Expected the LAN static IP '{seeded.LanIpAddress}' on the router, but the guest reported only: " +
                $"{string.Join(", ", addresses)}. A missing static IP points at the multi-NIC mapping (the static address " +
                "was stapled to the wrong adapter, or guest network config did not apply).");
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
