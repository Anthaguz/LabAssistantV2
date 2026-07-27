using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.ResourceProvider;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// The first guest-configured deploy scenario: a single Domain Controller. It seeds a tagged
/// V2 template with one forest (smoke.lab / SMOKE) whose single RootDomainController VM references
/// the REAL prepared Windows Server base image, drives Deploy &gt; From Template to plan it, resolves
/// the one local bootstrap credential slot, and asserts the plan becomes startable - proving the
/// UI accepts a guest-work DC template end to end (import -&gt; plan -&gt; credential resolution -&gt;
/// startable). That planning path runs with or without a real image password.
///
/// When the base image's local Administrator password is supplied (env var
/// LABASSISTANT_SMOKE_ADMIN_PASSWORD), the scenario goes all the way: Start Deploy, wait for the DC
/// VM to provision and Active Directory to come up, then authenticate into the promoted DC over
/// PowerShell Direct and assert the REAL forest/domain state (Get-ADForest / Get-ADDomain) matches
/// the template - the actual prize, validated against the guest, never the UI's success text. The
/// DC VM and template file carry the run tag so the harness gate tears them down and proves no
/// orphans; the real base image is only annotated with a bootstrap profile (never deleted).
/// </summary>
public sealed class TemplateDeployDcScenario : IScenario
{
    // The real prepared Windows Server base image already in the user's catalog. Overridable so the
    // scenario is not pinned to one machine, but defaulted to the known WS2022 image id/path here.
    private static string RealBaseImageId =>
        Environment.GetEnvironmentVariable("LABASSISTANT_SMOKE_BASE_IMAGE_ID") ?? "b0a5e0222022400080000000000000a1";

    public string Name => "template-deploy-dc";

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
            notes: "Harness-authored so the V2 planner accepts guest work (DC promotion) for this image.");

        if (!profiled)
        {
            recorder.RecordFailure(
                context.Host, Name, "seed-bootstrap", FindingSeverity.Error,
                $"Real base image '{RealBaseImageId}' is not registered in the catalog",
                "The DC scenario needs a prepared, bootable Windows Server base image registered in the app " +
                "catalog to promote a domain controller. Register the image (Assets > Base disks) or set " +
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

        // 2) Seed the tagged single-DC template referencing the real image + the gate's Internal switch.
        var seeder = new TemplateSeeder(new AppDataLocations());
        SeededDcTemplate seeded = seeder.SeedDomainControllerTemplate(hyperV.Tagger, RealBaseImageId, resources.SwitchName);

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "seed-template",
            Severity = FindingSeverity.Info,
            Title = $"Seeded single-DC V2 template '{seeded.TemplateName}'",
            Detail = $"File '{seeded.FilePath}', DC VM '{seeded.VmName}', forest '{seeded.DnsName}' " +
                     $"(NetBIOS '{seeded.NetBiosName}'), image '{RealBaseImageId}', switch '{resources.SwitchName}'. " +
                     $"Mode: {(live ? "LIVE (deploy + guest AD validation)" : "PLANNING-ONLY (no admin password supplied)")}."
        });

        // 2b) Clear this scenario's credential slot so the deploy uses the password we enter now,
        //     not a value cached from an earlier run. The app persists deploy credentials by slot
        //     key and reads them fresh on every plan eval, so a stale slot (e.g. a placeholder from
        //     a prior planning run) would silently deploy with the wrong password and the DC would
        //     never authenticate. We snapshot the prior state and restore it in the finally below,
        //     so the user's store is left exactly as it was (no placeholder pollution, no leaked
        //     real password) whether the run passes, fails, or throws.
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
        SeededDcTemplate seeded,
        ProvisionedResources resources,
        bool live)
    {
        // 3) Drive Deploy > From Template: select the template (auto-evaluates the plan).
        var page = new DeployFromTemplatePage(context.Host);
        page.Open();
        recorder.Capture(context.Host, "dc-deploy-opened");

        page.ReloadLibrary();
        if (!page.SelectTemplateByName(seeded.TemplateName, TimeSpan.FromSeconds(30)))
        {
            recorder.RecordFailure(
                context.Host, Name, "select-template", FindingSeverity.Error,
                $"Seeded DC template '{seeded.TemplateName}' never appeared in the deploy library",
                $"The From Template selector did not list the seeded DC template even after reloading. Expected file: '{seeded.FilePath}'.");
            return;
        }

        recorder.Capture(context.Host, "dc-deploy-selected");
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
            Title = $"Resolved {resolved} credential slot(s) for the DC template",
            Detail = live
                ? "Filled the local bootstrap slot with the real image admin password (from the env var); the " +
                  "planner reuses it for domain-admin and DSRM."
                : "Filled the local bootstrap slot with a PLACEHOLDER to prove startability. A live run overwrites " +
                  "it with the real password. The planner reuses this slot for domain-admin and DSRM."
        });
        page.EvaluatePlan();

        // 5) CORE ASSERTION (both modes): a guest-work DC template must reach a startable plan.
        if (!page.WaitForStartEnabled(TimeSpan.FromSeconds(60)))
        {
            recorder.RecordFailure(
                context.Host, Name, "readiness", FindingSeverity.Error,
                "Start Deploy never became enabled for the single-DC template",
                $"The V2 plan did not reach a startable state. Status: '{page.ActionStatusText()}'. A first-DC " +
                "template requires a bootstrap-capable base image (seeded above) and a resolved local bootstrap " +
                "credential slot; one of those requirements is still unmet.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "readiness",
            Severity = FindingSeverity.Info,
            Title = "Single-DC plan is startable",
            Detail = "The UI accepted a guest-work DC template end to end: import, plan evaluation, credential-slot " +
                     "resolution, and a startable plan (DC promotion planned)."
        });
        recorder.Capture(context.Host, "dc-deploy-startable");

        if (!live)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "gated",
                Severity = FindingSeverity.Warning,
                Title = "Live DC deploy + AD validation skipped: no admin password supplied",
                Detail = $"Set the '{GuestDirectoryProbe.PasswordEnvVar}' environment variable to the real base-image " +
                         "local Administrator password before running, and the scenario will Start Deploy, wait for " +
                         "the DC to promote, and validate Get-ADForest/Get-ADDomain over PowerShell Direct. The plan " +
                         "was proven startable, but no VM was created (no orphan risk)."
            });
            return;
        }

        // 6) LIVE: start the deploy, wait for the DC VM to provision, then validate the guest AD state.
        page.StartDeploy();
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "start-deploy",
            Severity = FindingSeverity.Info,
            Title = $"Start Deploy clicked for DC template '{seeded.TemplateName}'",
            Detail = $"Promoting forest '{seeded.DnsName}' on VM '{seeded.VmName}' (image '{RealBaseImageId}', switch '{resources.SwitchName}')."
        });

        if (!WaitForVmRunning(probe, seeded.VmName, TimeSpan.FromMinutes(6)))
        {
            recorder.RecordFailure(
                context.Host, Name, "provision", FindingSeverity.Error,
                $"DC VM '{seeded.VmName}' did not reach Running after Start Deploy",
                "The DC VM never appeared in Hyper-V in a Running state within the timeout, so provisioning or start " +
                "failed before promotion could begin. Check the app's deploy logs.");
            return;
        }

        recorder.Capture(context.Host, "dc-deploy-vm-running");
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "provision",
            Severity = FindingSeverity.Info,
            Title = $"DC VM '{seeded.VmName}' is Running; waiting for Active Directory",
            Detail = "The VM provisioned and started. DC promotion (and reboots) can take several minutes; the guest " +
                     "probe polls PowerShell Direct until Active Directory answers."
        });

        // 7) THE PRIZE: authenticate into the promoted DC and read the REAL forest/domain state.
        //    Abort the poll the moment the app rolls the VM back (a failed guest-config step deletes
        //    the VM), so a failed deploy fails fast instead of polling a deleted VM for the timeout.
        var guest = new GuestDirectoryProbe();
        GuestForestInfo? forest = guest.QueryForest(
            seeded.VmName,
            seeded.NetBiosName,
            TimeSpan.FromMinutes(12),
            abortIf: () => VmIsGone(probe, seeded.VmName));

        if (forest is null)
        {
            bool rolledBack = VmIsGone(probe, seeded.VmName);
            if (rolledBack)
            {
                recorder.RecordFailure(
                    context.Host, Name, "validate-guest", FindingSeverity.Error,
                    $"Deploy failed and the app rolled back DC VM '{seeded.VmName}' before promotion completed",
                    "The DC VM was Running but then disappeared, which means a deploy step failed and the app tore the VM " +
                    "down (cleanup worked - no orphan). This is a deploy failure inside the app, not a validation timeout: " +
                    "check the app diagnostics log (%APPDATA%\\LabAssistant\\Logs\\structured-events.jsonl) for the failing " +
                    "deploy.step (the guest-config steps around v2.prepareGuestNetwork are the usual culprit - a PowerShell " +
                    "Direct session can break across the guest's post-config reboot).");
                return;
            }

            recorder.RecordFailure(
                context.Host, Name, "validate-guest", FindingSeverity.Error,
                $"Active Directory never answered on DC '{seeded.VmName}'",
                $"The VM is still present but could not read Get-ADForest/Get-ADDomain over PowerShell Direct as " +
                $"'{seeded.NetBiosName}\\Administrator' within the timeout. Either promotion is still in progress past the " +
                "timeout, or the supplied admin password does not match the base image. See the console output for the " +
                "last PowerShell Direct error.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "validate-guest",
            Severity = FindingSeverity.Info,
            Title = $"DC '{seeded.VmName}' reports forest '{forest.ForestRootDomain}', domain '{forest.DomainDnsName}' ({forest.DomainNetBios})",
            Detail = "Read live from the guest over PowerShell Direct via Get-ADForest / Get-ADDomain."
        });

        bool forestOk = string.Equals(forest.ForestRootDomain, seeded.DnsName, StringComparison.OrdinalIgnoreCase);
        bool domainOk = string.Equals(forest.DomainDnsName, seeded.DnsName, StringComparison.OrdinalIgnoreCase);
        bool netbiosOk = string.IsNullOrEmpty(forest.DomainNetBios)
            || string.Equals(forest.DomainNetBios, seeded.NetBiosName, StringComparison.OrdinalIgnoreCase);

        if (forestOk && domainOk && netbiosOk)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "validate-guest",
                Severity = FindingSeverity.Info,
                Title = $"GUEST VALIDATION PASSED: DC promoted forest '{seeded.DnsName}' exactly as templated",
                Detail = $"Forest root '{forest.ForestRootDomain}' == '{seeded.DnsName}', domain DNS '{forest.DomainDnsName}' " +
                         $"== '{seeded.DnsName}', NetBIOS '{forest.DomainNetBios}' == '{seeded.NetBiosName}'. The deploy did " +
                         "what the template asked, validated against the running guest - not the UI."
            });
        }
        else
        {
            recorder.RecordFailure(
                context.Host, Name, "validate-guest", FindingSeverity.Error,
                $"DC guest AD state does not match the template",
                $"Expected forest/domain '{seeded.DnsName}' (NetBIOS '{seeded.NetBiosName}'), but the guest reported " +
                $"forest root '{forest.ForestRootDomain}', domain DNS '{forest.DomainDnsName}', NetBIOS '{forest.DomainNetBios}'.");
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
    /// True when the VM no longer exists. Swallows a transient inventory-probe failure by
    /// returning false, so a momentary Get-VM hiccup during the AD poll never aborts the poll
    /// or gets misread as a rollback; a genuine rollback (VM removed) reports true.
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
