using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.ResourceProvider;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// The domain-join proof: a two-VM lab with one forest (smoke.lab / SMOKE) whose FirstDomainController
/// VM promotes the forest and whose DomainMember VM then joins that domain. Both VMs reference the REAL
/// prepared Windows Server base image, so the V2 plan requires guest work (DC promotion + a domain
/// join) and therefore a bootstrap-capable base image plus a resolved local bootstrap credential slot.
/// It seeds a tagged template, drives Deploy &gt; From Template to plan it, resolves the credential
/// slot, and asserts the plan becomes startable. That planning path runs with or without a real image
/// password.
///
/// When the base image's local Administrator password is supplied (env var
/// LABASSISTANT_SMOKE_ADMIN_PASSWORD), the scenario goes all the way: Start Deploy, wait for BOTH VMs
/// to provision and settle, confirm the DC actually promoted the forest over PowerShell Direct
/// (Get-ADForest / Get-ADDomain as NETBIOS\Administrator - the member can only join a domain that
/// exists), then authenticate into the MEMBER as its local ".\Administrator" and assert it reports
/// PartOfDomain=true for the templated domain - the actual prize, validated against the running guest,
/// never the UI's success text. A member that silently failed to join (DNS could not find the DC,
/// wrong credentials, join timed out) surfaces here as PartOfDomain=false or the wrong domain even
/// when the deploy reported success. Both VMs and the template file carry the run tag so the harness
/// gate tears them down and proves no orphans; the real base image is only annotated with a bootstrap
/// profile (never deleted).
/// </summary>
public sealed class TemplateDeployDcMemberScenario : IScenario
{
    // The real prepared Windows Server base image already in the user's catalog. Overridable so the
    // scenario is not pinned to one machine, but defaulted to the known WS2022 image id here.
    private static string RealBaseImageId =>
        Environment.GetEnvironmentVariable("LABASSISTANT_SMOKE_BASE_IMAGE_ID") ?? "b0a5e0222022400080000000000000a1";

    public string Name => "template-deploy-dc-member";

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
            notes: "Harness-authored so the V2 planner accepts guest work (DC promotion + domain join) for this image.");

        if (!profiled)
        {
            recorder.RecordFailure(
                context.Host, Name, "seed-bootstrap", FindingSeverity.Error,
                $"Real base image '{RealBaseImageId}' is not registered in the catalog",
                "The DC-member scenario needs a prepared, bootable Windows Server base image registered in the app " +
                "catalog to promote a domain controller and join a member. Register the image (Assets > Base disks) or " +
                "set LABASSISTANT_SMOKE_BASE_IMAGE_ID to an existing catalog id, then re-run.");
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

        // 2) Seed the tagged DC + member template referencing the real image + the gate's Internal switch.
        var seeder = new TemplateSeeder(new AppDataLocations());
        SeededDcMemberTemplate seeded = seeder.SeedDomainControllerMemberTemplate(hyperV.Tagger, RealBaseImageId, resources.SwitchName);

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "seed-template",
            Severity = FindingSeverity.Info,
            Title = $"Seeded DC+member V2 template '{seeded.TemplateName}'",
            Detail = $"File '{seeded.FilePath}', DC VM '{seeded.DcVmName}', member VM '{seeded.MemberVmName}', forest " +
                     $"'{seeded.DnsName}' (NetBIOS '{seeded.NetBiosName}'), image '{RealBaseImageId}', switch " +
                     $"'{resources.SwitchName}'. Mode: {(live ? "LIVE (deploy + guest AD + member-join validation)" : "PLANNING-ONLY (no admin password supplied)")}."
        });

        // 2b) Clear this scenario's credential slot so the deploy uses the password we enter now, not
        //     a value cached from an earlier run, and restore the prior store verbatim in the finally.
        //     Both VMs share the same localBootstrap slot ref, so a single slot covers the pair.
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
        SeededDcMemberTemplate seeded,
        ProvisionedResources resources,
        bool live)
    {
        // 3) Drive Deploy > From Template: select the template (auto-evaluates the plan).
        var page = new DeployFromTemplatePage(context.Host);
        page.Open();
        recorder.Capture(context.Host, "dc-member-deploy-opened");

        page.ReloadLibrary();
        if (!page.SelectTemplateByName(seeded.TemplateName, TimeSpan.FromSeconds(30)))
        {
            recorder.RecordFailure(
                context.Host, Name, "select-template", FindingSeverity.Error,
                $"Seeded DC-member template '{seeded.TemplateName}' never appeared in the deploy library",
                $"The From Template selector did not list the seeded template even after reloading. Expected file: '{seeded.FilePath}'.");
            return;
        }

        recorder.Capture(context.Host, "dc-member-deploy-selected");
        page.EvaluatePlan();

        // 4) Resolve the local bootstrap credential slot. In live mode this is the REAL image password
        //    (from the env var) so the guest steps can authenticate; in planning-only mode a
        //    placeholder proves the plan is startable (the value is never used without a deploy). Both
        //    VMs share the localBootstrap slot, and the planner reuses it for domain-admin/domain-join.
        string password = live
            ? Environment.GetEnvironmentVariable(GuestDirectoryProbe.PasswordEnvVar)!
            : "P@ssw0rd!HarnessPlaceholder";

        int resolved = page.ResolveCredentialSlots("Administrator", password, TimeSpan.FromSeconds(30));
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "credentials",
            Severity = FindingSeverity.Info,
            Title = $"Resolved {resolved} credential slot(s) for the DC-member template",
            Detail = live
                ? "Filled the local bootstrap slot with the real image admin password (from the env var); the planner " +
                  "reuses it for domain-admin, DSRM (DC promotion) and domain-join (member)."
                : "Filled the local bootstrap slot with a PLACEHOLDER to prove startability. A live run overwrites it " +
                  "with the real password. The planner reuses this slot for domain-admin, DSRM and domain-join."
        });
        page.EvaluatePlan();

        // 5) CORE ASSERTION (both modes): a DC + domain-join template must reach a startable plan.
        if (!page.WaitForStartEnabled(TimeSpan.FromSeconds(60)))
        {
            recorder.RecordFailure(
                context.Host, Name, "readiness", FindingSeverity.Error,
                "Start Deploy never became enabled for the DC-member template",
                $"The V2 plan did not reach a startable state. Status: '{page.ActionStatusText()}'. A DC + domain-join " +
                "template requires a bootstrap-capable base image (seeded above) and a resolved local bootstrap " +
                "credential slot; one of those requirements is still unmet.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "readiness",
            Severity = FindingSeverity.Info,
            Title = "DC+member plan is startable",
            Detail = "The UI accepted a two-VM DC + domain-join guest-work template end to end: import, plan evaluation, " +
                     "credential-slot resolution, and a startable plan (DC promotion + member join planned)."
        });
        recorder.Capture(context.Host, "dc-member-deploy-startable");

        if (!live)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "gated",
                Severity = FindingSeverity.Warning,
                Title = "Live DC+member deploy + join validation skipped: no admin password supplied",
                Detail = $"Set the '{GuestDirectoryProbe.PasswordEnvVar}' environment variable to the real base-image " +
                         "local Administrator password before running, and the scenario will Start Deploy, wait for both " +
                         "VMs to settle, confirm the DC promoted the forest, and validate over PowerShell Direct that the " +
                         $"member joined '{seeded.DnsName}' (Win32_ComputerSystem PartOfDomain). The plan was proven " +
                         "startable, but no VMs were created (no orphan risk)."
            });
            return;
        }

        // 6) LIVE: start the deploy, wait for BOTH VMs to provision, then validate the guests.
        page.StartDeploy();
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "start-deploy",
            Severity = FindingSeverity.Info,
            Title = $"Start Deploy clicked for DC-member template '{seeded.TemplateName}'",
            Detail = $"Promoting forest '{seeded.DnsName}' on DC '{seeded.DcVmName}' and joining member '{seeded.MemberVmName}' " +
                     $"(image '{RealBaseImageId}', switch '{resources.SwitchName}')."
        });

        // Wait for BOTH VMs to reach Running before probing either guest, so a slow member (it starts
        // in a later wave, after the DC) is not misreported just because the DC settled first.
        var vmNames = new[] { seeded.DcVmName, seeded.MemberVmName };
        if (!WaitForAllVmsRunning(probe, vmNames, TimeSpan.FromMinutes(10)))
        {
            var notRunning = vmNames.Where(n =>
            {
                var t = probe.GetVm(n);
                return t is null || !string.Equals(t.State, "Running", StringComparison.OrdinalIgnoreCase);
            }).ToList();

            recorder.RecordFailure(
                context.Host, Name, "provision", FindingSeverity.Error,
                $"{notRunning.Count} of 2 DC-member VM(s) did not reach Running after Start Deploy",
                $"Not-Running VMs: [{string.Join(", ", notRunning)}]. Either provisioning failed for those VMs or the app " +
                "rolled one back before promotion/join completed. Check the app's deploy logs.");
            return;
        }

        recorder.Capture(context.Host, "dc-member-deploy-vms-running");
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "provision",
            Severity = FindingSeverity.Info,
            Title = "Both DC and member VMs are Running; waiting for AD promotion, then the member join",
            Detail = "Both VMs provisioned and started. DC promotion (and reboots) plus the member's join (with its own " +
                     "reboot) can take several minutes; the guest probes poll PowerShell Direct until each answers."
        });

        // 7a) PRECONDITION: the DC must actually promote the forest before the member can join it.
        //     Read it from the guest (Get-ADForest / Get-ADDomain), not the UI, and fail fast if the
        //     app rolls the DC back.
        var directory = new GuestDirectoryProbe();
        GuestForestInfo? forest = directory.QueryForest(
            seeded.DcVmName,
            seeded.NetBiosName,
            TimeSpan.FromMinutes(12),
            abortIf: () => VmIsGone(probe, seeded.DcVmName));

        if (forest is null)
        {
            bool rolledBack = VmIsGone(probe, seeded.DcVmName);
            recorder.RecordFailure(
                context.Host, Name, "validate-dc", FindingSeverity.Error,
                rolledBack
                    ? $"Deploy failed and the app rolled back DC '{seeded.DcVmName}' before promotion completed"
                    : $"Active Directory never answered on DC '{seeded.DcVmName}'",
                rolledBack
                    ? "The DC VM was Running but then disappeared, so a deploy step failed and the app tore it down " +
                      "(cleanup worked - no orphan). The member could not have joined. Check the app diagnostics log."
                    : $"Could not read Get-ADForest/Get-ADDomain over PowerShell Direct as '{seeded.NetBiosName}\\Administrator' " +
                      "within the timeout, so the forest the member needs to join is not up. See the console output for the " +
                      "last PowerShell Direct error.");
            return;
        }

        bool domainOk = string.Equals(forest.DomainDnsName, seeded.DnsName, StringComparison.OrdinalIgnoreCase);
        if (!domainOk)
        {
            recorder.RecordFailure(
                context.Host, Name, "validate-dc", FindingSeverity.Error,
                "DC promoted a different domain than the template asked for",
                $"Expected domain DNS '{seeded.DnsName}', but the DC reported '{forest.DomainDnsName}'. The member's join " +
                "target would not match, so the run is aborted before the member check.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "validate-dc",
            Severity = FindingSeverity.Info,
            Title = $"DC '{seeded.DcVmName}' promoted forest '{forest.ForestRootDomain}', domain '{forest.DomainDnsName}'",
            Detail = "Read live from the guest over PowerShell Direct; the join target exists. Now validating the member."
        });

        // 7b) THE PRIZE: authenticate into the MEMBER as its local ".\Administrator" and assert it
        //     reports PartOfDomain=true for the templated domain. Abort the poll the moment the app
        //     rolls the member back, so a failed join fails fast instead of polling a deleted VM.
        var membership = new GuestMembershipProbe();
        GuestDomainMembership? member = membership.QueryDomainMembership(
            seeded.MemberVmName,
            seeded.DnsName,
            TimeSpan.FromMinutes(12),
            abortIf: () => VmIsGone(probe, seeded.MemberVmName));

        if (member is null)
        {
            bool rolledBack = VmIsGone(probe, seeded.MemberVmName);
            recorder.RecordFailure(
                context.Host, Name, "validate-member", FindingSeverity.Error,
                rolledBack
                    ? $"Deploy failed and the app rolled back member '{seeded.MemberVmName}' before the join completed"
                    : $"Member '{seeded.MemberVmName}' never answered over PowerShell Direct",
                rolledBack
                    ? "The member VM was Running but then disappeared, so a deploy step failed and the app tore it down " +
                      "(cleanup worked - no orphan). Check the app diagnostics log for the failing deploy.step."
                    : $"Could not read Win32_ComputerSystem over PowerShell Direct as '.\\Administrator' on " +
                      $"'{seeded.MemberVmName}' within the timeout. The join may still be in progress, or the admin password " +
                      "does not match the base image. See the console output for the last PowerShell Direct error.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "validate-member",
            Severity = FindingSeverity.Info,
            Title = $"Member '{seeded.MemberVmName}' reports PartOfDomain={member.PartOfDomain}, Domain '{member.Domain}'",
            Detail = "Read live from the guest over PowerShell Direct via Get-CimInstance Win32_ComputerSystem."
        });

        bool joinedOk = member.PartOfDomain &&
                        string.Equals(member.Domain, seeded.DnsName, StringComparison.OrdinalIgnoreCase);
        if (joinedOk)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "validate-member",
                Severity = FindingSeverity.Info,
                Title = $"GUEST VALIDATION PASSED: member '{seeded.MemberVmName}' joined domain '{seeded.DnsName}'",
                Detail = $"The member reports PartOfDomain=true and Domain '{member.Domain}' == '{seeded.DnsName}', so the " +
                         "deploy promoted the DC and joined the member exactly as templated, validated against the running " +
                         "guests - not the UI."
            });
        }
        else
        {
            recorder.RecordFailure(
                context.Host, Name, "validate-member", FindingSeverity.Error,
                $"Member '{seeded.MemberVmName}' did not join domain '{seeded.DnsName}'",
                $"Expected PartOfDomain=true and Domain '{seeded.DnsName}', but the guest reported PartOfDomain=" +
                $"{member.PartOfDomain}, Domain '{member.Domain}'. The member never joined (DNS could not find the DC, the " +
                "join credentials were wrong, or the join step failed), even though the DC promoted successfully.");
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
    /// false, so a momentary Get-VM hiccup during a guest poll never aborts it or gets misread as a
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
