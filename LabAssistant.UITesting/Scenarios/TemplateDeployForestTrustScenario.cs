using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.ResourceProvider;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// The forest-trust proof and the top complexity rung: a two-forest lab on one shared Internal switch -
/// forest-alpha (alpha.lab / ALPHA) and forest-beta (beta.lab / BETA), each with its own
/// FirstDomainController VM on a static NIC that serves its own DNS - linked by a single bidirectional
/// Forest trust. Both DCs reference the REAL prepared Windows Server base image, so the V2 plan requires
/// guest work (two DC promotions plus the trust steps: prepare cross-forest DNS forwarders, create the
/// trust, validate it) and therefore a bootstrap-capable base image plus a resolved local bootstrap
/// credential slot. It seeds a tagged template, drives Deploy &gt; From Template to plan it, resolves the
/// credential slot, and asserts the plan becomes startable. That planning path runs with or without a
/// real image password.
///
/// When the base image's local Administrator password is supplied (env var
/// LABASSISTANT_SMOKE_ADMIN_PASSWORD), the scenario goes all the way: Start Deploy, wait for BOTH DCs to
/// provision and settle, confirm over PowerShell Direct that EACH DC promoted its OWN forest
/// (Get-ADForest / Get-ADDomain as its NETBIOS\Administrator), then read the trust live from BOTH sides
/// (Get-ADTrust from alpha to beta.lab AND from beta to alpha.lab) and assert each is a
/// forest+bidirectional trust to the peer - the actual prize, validated against the running guests,
/// never the UI's success text. A trust that silently failed to establish (DNS could not resolve the
/// peer, the cross-forest credential was wrong, the create step threw) surfaces here as an absent trust
/// or the wrong type/direction even when the deploy reported success. Both DCs and the template file
/// carry the run tag so the harness gate tears them down and proves no orphans; deleting the run-tagged
/// DC VMs removes the in-guest trust objects and conditional DNS forwarders with them, so nothing
/// host-side leaks. The real base image is only annotated with a bootstrap profile (never deleted).
///
/// The runtime's cleanupForestTrust step (DeleteLocalSideOfTrustRelationship on each anchor) runs only
/// on deploy failure/cancellation as the no-orphans mechanism; a successful deploy never invokes it. So
/// this happy-path verb gives live coverage to the prepare/create/validate trust steps, and the gate's
/// tag-sweep is what guarantees zero orphans on both the success and the failed/partial paths (the
/// trust and forwarders die with the guests either way).
/// </summary>
public sealed class TemplateDeployForestTrustScenario : IScenario
{
    // The real prepared Windows Server base image already in the user's catalog. Overridable so the
    // scenario is not pinned to one machine, but defaulted to the known WS2022 image id here.
    private static string RealBaseImageId =>
        Environment.GetEnvironmentVariable("LABASSISTANT_SMOKE_BASE_IMAGE_ID") ?? "b0a5e0222022400080000000000000a1";

    // AD-readiness budget for a single DC, measured from the moment BOTH VMs are Running (where the guest
    // probes start polling). It must absorb the WHOLE per-DC guest pipeline for the slowest of the two
    // concurrently-promoting DCs, NOT just the promotion itself. Derived from the observed 2-DC pipeline
    // plus headroom, worst-case accounting (finding 82):
    //   - transport / PowerShell-Direct login ......... ~6 min, PLUS up to ~5 min of TIME-BASED auth grace
    //     (a correct-password guest may legitimately tolerate transient auth-rejection during transport
    //     before promotion even starts - the finding-81 grace fix), so transport-complete is worst-case
    //     ~11 min. This is why a window anchored at VMs-Running is MORE fragile after the grace fix, and
    //     why the old 12 min (which barely covered transport+grace alone) expired ~1-3 min before
    //     domainReady finished even though both forests had actually promoted.
    //   - installAdDomainServices ...................... ~2.5 min
    //   - promoteFirstDomainController ................. ~1.5 min
    //   - domainReady (post-promotion AD services + reboot) ~4-5 min
    // ~11 + 2.5 + 1.5 + 5 = ~20 min worst case; round up to 25 min for headroom. This only BOUNDS the
    // silent-hang failure case: the probe returns the instant Get-ADForest answers, and an app rollback is
    // caught immediately by the VmIsGone abort, so a wider window adds no time to a healthy run.
    private static readonly TimeSpan ForestReadinessBudget = TimeSpan.FromMinutes(25);

    // Trust-readiness budget, measured from AFTER both forests are confirmed promoted. The trust steps
    // (prepare cross-forest DNS -> create trust -> validate) run once both anchors are domainReady, so this
    // window does NOT carry the transport/promotion cost the forest budget above absorbs; it only needs to
    // cover the trust wraps plus their #916 transient-drop retries. Kept at the original generous 12 min.
    private static readonly TimeSpan TrustReadinessBudget = TimeSpan.FromMinutes(12);

    public string Name => "template-deploy-forest-trust";

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
            notes: "Harness-authored so the V2 planner accepts guest work (two DC promotions + a forest trust) for this image.");

        if (!profiled)
        {
            recorder.RecordFailure(
                context.Host, Name, "seed-bootstrap", FindingSeverity.Error,
                $"Real base image '{RealBaseImageId}' is not registered in the catalog",
                "The forest-trust scenario needs a prepared, bootable Windows Server base image registered in the app " +
                "catalog to promote two domain controllers and establish a trust. Register the image (Assets > Base disks) " +
                "or set LABASSISTANT_SMOKE_BASE_IMAGE_ID to an existing catalog id, then re-run.");
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

        // 2) Seed the tagged two-forest + trust template referencing the real image + the gate's Internal switch.
        var seeder = new TemplateSeeder(new AppDataLocations());
        SeededForestTrustTemplate seeded = seeder.SeedForestTrustTemplate(hyperV.Tagger, RealBaseImageId, resources.SwitchName);

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "seed-template",
            Severity = FindingSeverity.Info,
            Title = $"Seeded two-forest + trust V2 template '{seeded.TemplateName}'",
            Detail = $"File '{seeded.FilePath}', source DC '{seeded.SourceDcVmName}' (forest '{seeded.SourceDnsName}' / " +
                     $"'{seeded.SourceNetBiosName}'), target DC '{seeded.TargetDcVmName}' (forest '{seeded.TargetDnsName}' / " +
                     $"'{seeded.TargetNetBiosName}'), image '{RealBaseImageId}', switch '{resources.SwitchName}'. Mode: " +
                     $"{(live ? "LIVE (deploy + guest AD + both-sides trust validation)" : "PLANNING-ONLY (no admin password supplied)")}."
        });

        // 2b) Clear this scenario's credential slot so the deploy uses the password we enter now, not a
        //     value cached from an earlier run, and restore the prior store verbatim in the finally.
        //     Both DCs share the same localBootstrap slot ref, so a single slot covers the pair.
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
        SeededForestTrustTemplate seeded,
        ProvisionedResources resources,
        bool live)
    {
        // 3) Drive Deploy > From Template: select the template (auto-evaluates the plan).
        var page = new DeployFromTemplatePage(context.Host);
        page.Open();
        recorder.Capture(context.Host, "forest-trust-deploy-opened");

        page.ReloadLibrary();
        if (!page.SelectTemplateByName(seeded.TemplateName, TimeSpan.FromSeconds(30)))
        {
            recorder.RecordFailure(
                context.Host, Name, "select-template", FindingSeverity.Error,
                $"Seeded forest-trust template '{seeded.TemplateName}' never appeared in the deploy library",
                $"The From Template selector did not list the seeded template even after reloading. Expected file: '{seeded.FilePath}'.");
            return;
        }

        recorder.Capture(context.Host, "forest-trust-deploy-selected");
        page.EvaluatePlan();

        // 4) Resolve the local bootstrap credential slot. In live mode this is the REAL image password
        //    (from the env var) so the guest steps can authenticate; in planning-only mode a placeholder
        //    proves the plan is startable (the value is never used without a deploy). Both DCs share the
        //    localBootstrap slot, and the planner reuses it for each domain-admin/DSRM and the trust.
        string password = live
            ? Environment.GetEnvironmentVariable(GuestDirectoryProbe.PasswordEnvVar)!
            : "P@ssw0rd!HarnessPlaceholder";

        int resolved = page.ResolveCredentialSlots("Administrator", password, TimeSpan.FromSeconds(30));
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "credentials",
            Severity = FindingSeverity.Info,
            Title = $"Resolved {resolved} credential slot(s) for the forest-trust template",
            Detail = live
                ? "Filled the local bootstrap slot with the real image admin password (from the env var); the planner " +
                  "reuses it for each domain's domain-admin, DSRM and the cross-forest trust credential."
                : "Filled the local bootstrap slot with a PLACEHOLDER to prove startability. A live run overwrites it " +
                  "with the real password. The planner reuses this slot for each domain-admin, DSRM and the trust."
        });
        page.EvaluatePlan();

        // 5) CORE ASSERTION (both modes): a two-forest + trust template must reach a startable plan.
        if (!page.WaitForStartEnabled(TimeSpan.FromSeconds(60)))
        {
            recorder.RecordFailure(
                context.Host, Name, "readiness", FindingSeverity.Error,
                "Start Deploy never became enabled for the forest-trust template",
                $"The V2 plan did not reach a startable state. Status: '{page.ActionStatusText()}'. A two-forest + trust " +
                "template requires a bootstrap-capable base image (seeded above) and a resolved local bootstrap credential " +
                "slot; one of those requirements is still unmet.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "readiness",
            Severity = FindingSeverity.Info,
            Title = "Two-forest + trust plan is startable",
            Detail = "The UI accepted a two-VM two-forest + trust guest-work template end to end: import, plan evaluation, " +
                     "credential-slot resolution, and a startable plan (two DC promotions + forest trust planned)."
        });
        recorder.Capture(context.Host, "forest-trust-deploy-startable");

        if (!live)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "gated",
                Severity = FindingSeverity.Warning,
                Title = "Live forest-trust deploy + validation skipped: no admin password supplied",
                Detail = $"Set the '{GuestDirectoryProbe.PasswordEnvVar}' environment variable to the real base-image " +
                         "local Administrator password before running, and the scenario will Start Deploy, wait for both DCs " +
                         "to settle, confirm each promoted its own forest, and validate over PowerShell Direct that a " +
                         $"forest+bidirectional trust exists BOTH ways between '{seeded.SourceDnsName}' and " +
                         $"'{seeded.TargetDnsName}'. The plan was proven startable, but no VMs were created (no orphan risk)."
            });
            return;
        }

        // 6) LIVE: start the deploy, wait for BOTH DCs to provision, then validate the guests.
        page.StartDeploy();
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "start-deploy",
            Severity = FindingSeverity.Info,
            Title = $"Start Deploy clicked for forest-trust template '{seeded.TemplateName}'",
            Detail = $"Promoting forests '{seeded.SourceDnsName}' on '{seeded.SourceDcVmName}' and '{seeded.TargetDnsName}' " +
                     $"on '{seeded.TargetDcVmName}', then establishing a bidirectional forest trust between them " +
                     $"(image '{RealBaseImageId}', switch '{resources.SwitchName}')."
        });

        // Wait for BOTH DCs to reach Running before probing either guest, so a slower peer (they promote
        // in waves) is not misreported just because the first settled sooner.
        var vmNames = new[] { seeded.SourceDcVmName, seeded.TargetDcVmName };
        if (!WaitForAllVmsRunning(probe, vmNames, TimeSpan.FromMinutes(10)))
        {
            var notRunning = vmNames.Where(n =>
            {
                var t = probe.GetVm(n);
                return t is null || !string.Equals(t.State, "Running", StringComparison.OrdinalIgnoreCase);
            }).ToList();

            recorder.RecordFailure(
                context.Host, Name, "provision", FindingSeverity.Error,
                $"{notRunning.Count} of 2 forest-trust DC VM(s) did not reach Running after Start Deploy",
                $"Not-Running VMs: [{string.Join(", ", notRunning)}]. Either provisioning failed for those VMs or the app " +
                "rolled one back before promotion/trust completed. Check the app's deploy logs.");
            return;
        }

        recorder.Capture(context.Host, "forest-trust-deploy-vms-running");
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "provision",
            Severity = FindingSeverity.Info,
            Title = "Both DCs are Running; waiting for AD promotion on each, then the forest trust",
            Detail = "Both VMs provisioned and started. Two DC promotions (with reboots) plus the DNS-forwarder / trust-create " +
                     "steps can take several minutes; the guest probes poll PowerShell Direct until each answers."
        });

        // 7) PRECONDITION: each DC must actually promote its OWN forest before the trust can span them.
        //    Read it from each guest (Get-ADForest / Get-ADDomain), not the UI, and fail fast if the app
        //    rolls either DC back.
        if (!ConfirmForestPromoted(context, recorder, probe, seeded.SourceDcVmName, seeded.SourceNetBiosName, seeded.SourceDnsName, "source") ||
            !ConfirmForestPromoted(context, recorder, probe, seeded.TargetDcVmName, seeded.TargetNetBiosName, seeded.TargetDnsName, "target"))
        {
            return;
        }

        // 8) THE PRIZE: read the trust live from BOTH sides. The runtime creates the trust from the
        //    source anchor but builds both sides in one call, so each DC must see a forest+bidirectional
        //    trust to its peer. Probing both proves the trust actually spans the forests, not just that
        //    the source anchor recorded its half. Abort each poll the moment the app rolls that DC back.
        if (!ConfirmTrust(context, recorder, probe, seeded.SourceDcVmName, seeded.SourceNetBiosName, seeded.TargetDnsName, "source->target") ||
            !ConfirmTrust(context, recorder, probe, seeded.TargetDcVmName, seeded.TargetNetBiosName, seeded.SourceDnsName, "target->source"))
        {
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "validate-trust",
            Severity = FindingSeverity.Info,
            Title = $"GUEST VALIDATION PASSED: bidirectional forest trust established between '{seeded.SourceDnsName}' and '{seeded.TargetDnsName}'",
            Detail = "Both DCs promoted their own forest and each reports a forest+bidirectional trust to the peer, read " +
                     "live over PowerShell Direct (Get-ADTrust) - so the deploy prepared cross-forest DNS, created the trust, " +
                     "and validated it exactly as templated, proven against the running guests, not the UI."
        });
    }

    /// <summary>
    /// Confirms one DC promoted the expected forest, reading Get-ADForest/Get-ADDomain live from the
    /// guest. Records a failure and returns false when AD never answered, the app rolled the DC back, or
    /// the DC promoted a different domain than templated.
    /// </summary>
    private bool ConfirmForestPromoted(
        ScenarioContext context,
        FindingRecorder recorder,
        HyperVProbe probe,
        string vmName,
        string netBiosName,
        string expectedDnsName,
        string role)
    {
        var directory = new GuestDirectoryProbe();
        GuestForestInfo? forest = directory.QueryForest(
            vmName,
            netBiosName,
            ForestReadinessBudget,
            abortIf: () => VmIsGone(probe, vmName));

        if (forest is null)
        {
            bool rolledBack = VmIsGone(probe, vmName);
            recorder.RecordFailure(
                context.Host, Name, "validate-dc", FindingSeverity.Error,
                rolledBack
                    ? $"Deploy failed and the app rolled back {role} DC '{vmName}' before promotion completed"
                    : $"Active Directory never answered on {role} DC '{vmName}'",
                rolledBack
                    ? "The DC VM was Running but then disappeared, so a deploy step failed and the app tore it down " +
                      "(cleanup worked - no orphan). The forest trust could not have been established. Check the app diagnostics log."
                    : $"Could not read Get-ADForest/Get-ADDomain over PowerShell Direct as '{netBiosName}\\Administrator' " +
                      "within the timeout, so this forest is not up. See the console output for the last PowerShell Direct error.");
            return false;
        }

        if (!string.Equals(forest.DomainDnsName, expectedDnsName, StringComparison.OrdinalIgnoreCase))
        {
            recorder.RecordFailure(
                context.Host, Name, "validate-dc", FindingSeverity.Error,
                $"{role} DC promoted a different domain than the template asked for",
                $"Expected domain DNS '{expectedDnsName}', but the DC reported '{forest.DomainDnsName}'. The trust target " +
                "would not match, so the run is aborted before the trust check.");
            return false;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "validate-dc",
            Severity = FindingSeverity.Info,
            Title = $"{role} DC '{vmName}' promoted forest '{forest.ForestRootDomain}', domain '{forest.DomainDnsName}'",
            Detail = "Read live from the guest over PowerShell Direct; this forest is up and ready to be a trust anchor."
        });
        return true;
    }

    /// <summary>
    /// Confirms one DC reports a forest+bidirectional trust to the expected peer domain, reading
    /// Get-ADTrust live from the guest. Records a failure and returns false when no such trust appeared,
    /// the app rolled the DC back, or the trust is the wrong type/direction.
    /// </summary>
    private bool ConfirmTrust(
        ScenarioContext context,
        FindingRecorder recorder,
        HyperVProbe probe,
        string vmName,
        string netBiosName,
        string expectedTrustedDomainDns,
        string direction)
    {
        var trustProbe = new GuestTrustProbe();
        GuestTrustInfo? trust = trustProbe.QueryTrust(
            vmName,
            netBiosName,
            expectedTrustedDomainDns,
            TrustReadinessBudget,
            abortIf: () => VmIsGone(probe, vmName));

        if (trust is null)
        {
            bool rolledBack = VmIsGone(probe, vmName);
            recorder.RecordFailure(
                context.Host, Name, "validate-trust", FindingSeverity.Error,
                rolledBack
                    ? $"Deploy failed and the app rolled back DC '{vmName}' before the {direction} trust was established"
                    : $"No forest trust to '{expectedTrustedDomainDns}' ever appeared on DC '{vmName}' ({direction})",
                rolledBack
                    ? "The DC VM was Running but then disappeared, so a deploy step failed and the app tore it down " +
                      "(cleanup worked - no orphan). Check the app diagnostics log for the failing deploy.step."
                    : $"Could not read a trust to '{expectedTrustedDomainDns}' over PowerShell Direct as " +
                      $"'{netBiosName}\\Administrator' on '{vmName}' within the timeout. The trust may have failed to create " +
                      "(cross-forest DNS did not resolve, wrong credential, or the create step threw). See the console output.");
            return false;
        }

        bool trustOk = string.Equals(trust.Target.TrimEnd('.'), expectedTrustedDomainDns.TrimEnd('.'), StringComparison.OrdinalIgnoreCase) &&
                       trust.IsForestTrust &&
                       trust.IsBidirectional;
        if (!trustOk)
        {
            recorder.RecordFailure(
                context.Host, Name, "validate-trust", FindingSeverity.Error,
                $"DC '{vmName}' does not hold the expected {direction} forest trust",
                $"Expected a forest+bidirectional trust to '{expectedTrustedDomainDns}', but the guest reported " +
                $"Target='{trust.Target}', TrustType='{trust.TrustType}', Direction='{trust.Direction}', " +
                $"ForestTransitive={trust.ForestTransitive}. The trust either landed on the wrong target, is not a forest " +
                "trust, or is not bidirectional.");
            return false;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "validate-trust",
            Severity = FindingSeverity.Info,
            Title = $"{direction}: DC '{vmName}' holds a forest+bidirectional trust to '{trust.Target}'",
            Detail = $"Read live from the guest over PowerShell Direct via Get-ADTrust: TrustType='{trust.TrustType}', " +
                     $"Direction='{trust.Direction}', ForestTransitive={trust.ForestTransitive}."
        });
        return true;
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
