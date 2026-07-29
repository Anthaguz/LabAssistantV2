using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.ResourceProvider;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// The routed cross-forest capstone rung: two root forests on SEPARATE gatewayed Internal switches -
/// forest-alpha (alpha.lab / ALPHA) on alpha-net and forest-beta (beta.lab / BETA) on beta-net - bridged
/// by a standalone 3-NIC router (a LAN leg on each subnet plus a Default Switch egress). Each DC's default
/// gateway is the router's LAN leg on its own subnet, so the two forests can only reach each other THROUGH
/// the router; there is no shared L2 segment. A single bidirectional Forest trust links the roots, and the
/// runtime prepares a conditional DNS forwarder on each DC pointing at the peer across the routed boundary.
///
/// This is the live proof of the finding-83 planner fix (PR #922): because the two anchor DCs live on
/// disjoint switches and the router carries a leg on each side, the planner must order the forest-trust /
/// DNS-prep stage AFTER the router is routing (the RouterReady -&gt; prepareForestTrustDns edge #922 adds via
/// the structural TrustSpansRouterBridgedBoundary trigger). Without that edge the trust would race the
/// router and only lucky-pass behind the #916 transport-retry wraps; this rung asserts the ordering
/// directly from the app's own step log, so a missing edge cannot hide behind a retry.
///
/// Proof set (strongest first), all read from the running guests / the app's structured log, never the UI:
///   (b) ORDERING: the router's enableRouterRouting result=success timestamp PRECEDES the first
///       prepareForestTrustDns start on either DC - the live corroboration of #922's edge.
///   (c) validateCrossSwitchRouting is terminally SKIPPED. This is the CORRECT outcome here, not a gap:
///       cross-switch validation runs only for a router-dependent guest (a replica DC or domain-join
///       member on a switch behind the router that must reach a DC across it), keyed on
///       RequiresRouterDependency, which a FirstDomainController never sets. Neither forest root is such a
///       guest, so the step correctly skips; the DC-to-DC trust routing is proven by the trust steps plus
///       the route-hop check (e), not by this step. (Driving it to success needs a dedicated
///       domain-join-across-router topology and is deliberately out of scope for this rung.)
///   (d) TRUST: each DC reports a forest+bidirectional trust to its peer (Get-ADTrust both sides).
///   (e) ROUTE-HOP: from each DC, the next hop selected to reach the PEER DC (on the other subnet) is this
///       DC's gateway - the router's LAN leg - and the peer is reachable. This is the evidence the trust
///       traffic crossed the router rather than taking a same-subnet shortcut.
///   (f) FOOTPRINT + NO ORPHANS: exactly the three tagged VMs stood up and both referenced-but-absent lab
///       switches were auto-created as Internal (the disjoint two-switch topology, no L2 collapse); the
///       harness gate then tears everything down and its fail-closed no-orphans check proves nothing
///       tagged (3 VMs + 2 Internal switches + disks + template) survives.
///
/// The planning-only path (no LABASSISTANT_SMOKE_ADMIN_PASSWORD) drives the template to a startable plan
/// and stops - no VMs are created, no orphan risk. All three VMs reference the REAL prepared Windows Server
/// base image so the plan requires guest work (router RRAS/NAT + two DC promotions + the trust steps) and a
/// resolved local bootstrap slot; the real image is only annotated with a bootstrap profile (never deleted).
/// </summary>
public sealed class TemplateDeployForestTrustRoutedScenario : IScenario
{
    // The real prepared Windows Server base image already in the user's catalog. Overridable so the
    // scenario is not pinned to one machine, but defaulted to the known WS2022 image id here.
    private static string RealBaseImageId =>
        Environment.GetEnvironmentVariable("LABASSISTANT_SMOKE_BASE_IMAGE_ID") ?? "b0a5e0222022400080000000000000a1";

    // Step keys are pinned here as a log contract: the harness references no app project (it drives the
    // built exe), so these mirror LabAssistant.Models.Deployment.DeploymentStepKeys by value.
    private const string StepEnableRouterRouting = "v2.enableRouterRouting";
    private const string StepValidateCrossSwitchRouting = "v2.validateCrossSwitchRouting";
    private const string StepRouterReady = "v2.routerReady";
    private const string StepPrepareForestTrustDns = "v2.prepareForestTrustDns";

    // AD-readiness budget for a single DC, measured from the moment ALL VMs are Running. Reused verbatim
    // from the finding-82 accounting: each DC promotes its OWN forest locally and does NOT wait on the
    // router, so this window covers the same worst-case per-DC guest pipeline as the same-L2 forest-trust
    // scenario (transport ~11 min incl. up to ~5 min grace + installADDS ~2.5 + promote ~1.5 + domainReady
    // reboot ~5 = ~20 min, rounded to 25 for headroom). It only bounds the silent-hang case: the probe
    // returns the instant Get-ADForest answers and a rollback is caught immediately by the VmIsGone abort.
    private static readonly TimeSpan ForestReadinessBudget = TimeSpan.FromMinutes(25);

    // Trust-readiness budget, measured from AFTER both forests are confirmed promoted. Unlike the same-L2
    // case, the trust here cannot start until the router is routing (the #922 edge) AND cross-subnet DNS
    // resolution settles across the router, so this widens the same-L2 12 min:
    //   - base trust wraps (prepareDns -> create -> validate) + #916 transient-drop retries ... ~12 min
    //   - router-config tail + cross-subnet settle before the routed conditional forwarders resolve the
    //     peer across the boundary (RRAS/NAT is already up concurrently with promotion, but the routed
    //     DNS path needs a few minutes to settle after both DCs are ready) .................. ~6 min
    // ~18 min. Like the forest budget this only bounds a silent hang: ConfirmTrust polls and aborts the
    // instant the app rolls a DC back, so a wider window costs nothing on a healthy run.
    private static readonly TimeSpan TrustReadinessBudget = TimeSpan.FromMinutes(18);

    // Ordering + route-hop are read only AFTER the trust is confirmed both ways, by which point routing is
    // provably up (cross-forest resolution already succeeded), so these are short bounds on already-settled
    // state rather than waits for it to appear.
    private static readonly TimeSpan RouterReadyBudget = TimeSpan.FromMinutes(12);
    private static readonly TimeSpan RouteHopBudget = TimeSpan.FromMinutes(5);

    public string Name => "template-deploy-forest-trust-routed";

    public string Capability => "Deploy";

    public ScenarioRequirements Requirements => ScenarioRequirements.HyperV;

    public void Run(ScenarioContext context)
    {
        var recorder = context.Recorder;
        var hyperV = context.RequireHyperV();
        var probe = hyperV.Probe;
        bool live = GuestDirectoryProbe.HasAdminPassword;

        // 1) Make the REAL base image guest-configurable (bootstrap profile on its catalog entry). This
        //    never touches the id/path or deletes the VHDX, so the gate's tag sweep leaves it intact.
        var catalog = new CatalogSeeder(new AppDataLocations());
        bool profiled = catalog.EnsureBaseDiskBootstrapProfile(
            RealBaseImageId,
            expectedLocalUser: "Administrator",
            localCredentialSlotRef: TemplateSeeder.DcLocalBootstrapSlotKey,
            guestOsFamily: "WindowsServer",
            guestTransport: "powershell-direct",
            notes: "Harness-authored so the V2 planner accepts guest work (router RRAS/NAT + two DC promotions + a routed forest trust) for this image.");

        if (!profiled)
        {
            recorder.RecordFailure(
                context.Host, Name, "seed-bootstrap", FindingSeverity.Error,
                $"Real base image '{RealBaseImageId}' is not registered in the catalog",
                "The routed forest-trust scenario needs a prepared, bootable Windows Server base image registered in the app " +
                "catalog to configure a router and promote two domain controllers. Register the image (Assets > Base disks) " +
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

        // 2) Two run-tagged Internal switch names the harness never provisions: each names one forest's
        //    subnet and is deliberately absent, so the deploy must auto-create them (the #909 fix) as
        //    Internal. Because they carry the run prefix, the gate's switch sweep removes both.
        string switchA = hyperV.Tagger.Name("ftr-alpha");
        string switchB = hyperV.Tagger.Name("ftr-beta");

        // Guard against pre-existing switches of those names (freshly tagged, so this should be impossible);
        // a leftover would make the Internal-auto-create assertion meaningless. GetSwitchType returns null
        // (never throws) for an absent switch, so this reads clean for the expected names.
        foreach (var (label, name) in new[] { ("alpha", switchA), ("beta", switchB) })
        {
            if (probe.GetSwitchType(name) is not null)
            {
                recorder.RecordFailure(
                    context.Host, Name, "seed-template", FindingSeverity.Error,
                    $"Lab switch '{name}' ({label}) already exists before the deploy",
                    "The run-tagged switch name was expected absent so the deploy would auto-create it as Internal, but a " +
                    "switch with that name is already present. A prior run may have leaked it; run the sweep verb and retry.");
                return;
            }
        }

        // 3) Seed the tagged routed two-forest + trust template referencing the real image + the two ghost switches.
        var seeder = new TemplateSeeder(new AppDataLocations());
        SeededForestTrustRoutedTemplate seeded = seeder.SeedForestTrustRoutedTemplate(hyperV.Tagger, RealBaseImageId, switchA, switchB);

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "seed-template",
            Severity = FindingSeverity.Info,
            Title = $"Seeded routed two-forest + trust V2 template '{seeded.TemplateName}'",
            Detail = $"File '{seeded.FilePath}', router '{seeded.RouterVmName}', source DC '{seeded.SourceDcVmName}' " +
                     $"(forest '{seeded.SourceDnsName}' on {seeded.SourceSubnet}, gw {seeded.SourceGatewayIpAddress}), " +
                     $"target DC '{seeded.TargetDcVmName}' (forest '{seeded.TargetDnsName}' on {seeded.TargetSubnet}, " +
                     $"gw {seeded.TargetGatewayIpAddress}), switches '{switchA}'/'{switchB}' (both absent, to be auto-created " +
                     $"Internal), image '{RealBaseImageId}'. Mode: " +
                     $"{(live ? "LIVE (deploy + guest AD + both-sides trust + routed-boundary evidence)" : "PLANNING-ONLY (no admin password supplied)")}."
        });

        // 3b) Clear this scenario's credential slot so the deploy uses the password we enter now, not a
        //     value cached from an earlier run, and restore the prior store verbatim in the finally. All
        //     three VMs share the same localBootstrap slot ref, so a single slot covers them.
        var credSeeder = new CredentialSlotSeeder(new AppDataLocations());
        var priorSlot = credSeeder.Capture(seeded.LocalBootstrapSlotKey);
        credSeeder.Remove(seeded.LocalBootstrapSlotKey);

        try
        {
            RunDeploy(context, recorder, probe, seeded, live);
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
        SeededForestTrustRoutedTemplate seeded,
        bool live)
    {
        // 4) Drive Deploy > From Template: select the template (auto-evaluates the plan).
        var page = new DeployFromTemplatePage(context.Host);
        page.Open();
        recorder.Capture(context.Host, "forest-trust-routed-deploy-opened");

        page.ReloadLibrary();
        if (!page.SelectTemplateByName(seeded.TemplateName, TimeSpan.FromSeconds(30)))
        {
            recorder.RecordFailure(
                context.Host, Name, "select-template", FindingSeverity.Error,
                $"Seeded routed forest-trust template '{seeded.TemplateName}' never appeared in the deploy library",
                $"The From Template selector did not list the seeded template even after reloading. Expected file: '{seeded.FilePath}'.");
            return;
        }

        recorder.Capture(context.Host, "forest-trust-routed-deploy-selected");
        page.EvaluatePlan();

        // 5) Resolve the local bootstrap credential slot. In live mode this is the REAL image password (from
        //    the env var) so guest steps can authenticate; in planning-only mode a placeholder proves the
        //    plan is startable (never used without a deploy). All three VMs share the slot; the planner
        //    reuses it for each domain-admin/DSRM, the router local admin, and the cross-forest trust.
        string password = live
            ? Environment.GetEnvironmentVariable(GuestDirectoryProbe.PasswordEnvVar)!
            : "P@ssw0rd!HarnessPlaceholder";

        int resolved = page.ResolveCredentialSlots("Administrator", password, TimeSpan.FromSeconds(30));
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "credentials",
            Severity = FindingSeverity.Info,
            Title = $"Resolved {resolved} credential slot(s) for the routed forest-trust template",
            Detail = live
                ? "Filled the local bootstrap slot with the real image admin password (from the env var); the planner reuses " +
                  "it for the router, each domain's domain-admin/DSRM, and the cross-forest trust credential."
                : "Filled the local bootstrap slot with a PLACEHOLDER to prove startability. A live run overwrites it with the " +
                  "real password. The planner reuses this slot for the router, each domain-admin/DSRM, and the trust."
        });
        page.EvaluatePlan();

        // 6) CORE ASSERTION (both modes): a routed two-forest + trust template must reach a startable plan.
        if (!page.WaitForStartEnabled(TimeSpan.FromSeconds(60)))
        {
            recorder.RecordFailure(
                context.Host, Name, "readiness", FindingSeverity.Error,
                "Start Deploy never became enabled for the routed forest-trust template",
                $"The V2 plan did not reach a startable state. Status: '{page.ActionStatusText()}'. This template requires a " +
                "bootstrap-capable base image (seeded above), a resolved local bootstrap slot, and the switch-auto-create " +
                "planning half for its two absent lab switches; one of those requirements is still unmet.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "readiness",
            Severity = FindingSeverity.Info,
            Title = "Routed two-forest + trust plan is startable",
            Detail = "The UI accepted a three-VM routed two-forest + trust guest-work template end to end: import, plan " +
                     "evaluation, credential-slot resolution, and a startable plan (router + two DC promotions + forest trust)."
        });
        recorder.Capture(context.Host, "forest-trust-routed-deploy-startable");

        if (!live)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "gated",
                Severity = FindingSeverity.Warning,
                Title = "Live routed forest-trust deploy + validation skipped: no admin password supplied",
                Detail = $"Set the '{GuestDirectoryProbe.PasswordEnvVar}' environment variable to the real base-image local " +
                         "Administrator password before running, and the scenario will Start Deploy, wait for the router + both " +
                         "DCs, confirm each forest, validate the bidirectional trust BOTH ways, assert the router-routing/trust " +
                         "ordering and the route-hop through the router, and prove no orphans. The plan was proven startable, " +
                         "but no VMs were created (no orphan risk)."
            });
            return;
        }

        // 7) LIVE: open the app-log window BEFORE Start Deploy so the ordering assertion sees every step's
        //    start/end event, then start the deploy.
        var stepLog = new DeployStepLogProbe(new AppDataLocations());
        var logWindow = stepLog.OpenWindow();

        page.StartDeploy();
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "start-deploy",
            Severity = FindingSeverity.Info,
            Title = $"Start Deploy clicked for routed forest-trust template '{seeded.TemplateName}'",
            Detail = $"Bringing up router '{seeded.RouterVmName}' bridging '{seeded.SourceSubnet}' and '{seeded.TargetSubnet}', " +
                     $"promoting forests '{seeded.SourceDnsName}' and '{seeded.TargetDnsName}', then establishing a bidirectional " +
                     "forest trust across the routed boundary."
        });

        // Wait for ALL THREE VMs (router + both DCs) to reach Running before probing any guest.
        var vmNames = new[] { seeded.RouterVmName, seeded.SourceDcVmName, seeded.TargetDcVmName };
        if (!WaitForAllVmsRunning(probe, vmNames, TimeSpan.FromMinutes(12)))
        {
            var notRunning = vmNames.Where(n =>
            {
                var t = probe.GetVm(n);
                return t is null || !string.Equals(t.State, "Running", StringComparison.OrdinalIgnoreCase);
            }).ToList();

            recorder.RecordFailure(
                context.Host, Name, "provision", FindingSeverity.Error,
                $"{notRunning.Count} of 3 routed forest-trust VM(s) did not reach Running after Start Deploy",
                $"Not-Running VMs: [{string.Join(", ", notRunning)}]. Either provisioning failed for those VMs or the app rolled " +
                "one back before promotion/trust completed. Check the app's deploy logs.");
            return;
        }

        recorder.Capture(context.Host, "forest-trust-routed-vms-running");

        // 8) STRUCTURAL: both referenced-but-absent lab switches must have been auto-created as Internal.
        //    This proves the disjoint two-switch topology actually stood up (no L2 collapse onto one
        //    switch) and corroborates the #909 switch-auto-create fix for a two-switch case.
        if (!AssertLabSwitchInternal(context, recorder, probe, seeded.SwitchAName, "alpha") ||
            !AssertLabSwitchInternal(context, recorder, probe, seeded.SwitchBName, "beta"))
        {
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "provision",
            Severity = FindingSeverity.Info,
            Title = "Router + both DCs are Running on two auto-created Internal switches; waiting for AD + the routed trust",
            Detail = "All three VMs provisioned and started, and each forest sits on its own Internal switch bridged by the " +
                     "router. Two DC promotions (with reboots) plus the routed DNS-forwarder / trust steps take several minutes; " +
                     "the guest probes poll PowerShell Direct until each answers."
        });

        // 9) PRECONDITION: each DC must promote its OWN forest before the trust can span them.
        if (!ConfirmForestPromoted(context, recorder, probe, seeded.SourceDcVmName, seeded.SourceNetBiosName, seeded.SourceDnsName, "source") ||
            !ConfirmForestPromoted(context, recorder, probe, seeded.TargetDcVmName, seeded.TargetNetBiosName, seeded.TargetDnsName, "target"))
        {
            return;
        }

        // 10) THE PRIZE (d): read the trust live from BOTH sides. Success here already implies the routed
        //     DNS path worked (each DC resolved the peer forest across the router), so it is also the
        //     functional proof that routing was up when the trust was created.
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
            Title = $"GUEST VALIDATION PASSED: bidirectional forest trust established across the routed boundary between '{seeded.SourceDnsName}' and '{seeded.TargetDnsName}'",
            Detail = "Both DCs promoted their own forest on their own subnet and each reports a forest+bidirectional trust to the " +
                     "peer, read live over PowerShell Direct (Get-ADTrust) - so the deploy prepared cross-forest DNS across the " +
                     "router, created the trust, and validated it, proven against the running guests, not the UI."
        });

        // 11) ORDERING (b) + proof (c): assert #922's RouterReady->prepareDns ordering and the correct
        //     validateCrossSwitchRouting skip, read from the app's own step log.
        AssertRoutingPrecededTrustPrep(context, recorder, probe, stepLog, logWindow, seeded);

        // 12) ROUTE-HOP (e): from each DC, the next hop to the PEER DC is this DC's gateway (the router's
        //     LAN leg) and the peer is reachable - the evidence the trust crossed the router.
        if (!AssertRouteHop(context, recorder, probe, seeded.SourceDcVmName, seeded.SourceNetBiosName, seeded.TargetDcIpAddress, seeded.SourceGatewayIpAddress, "source->target") ||
            !AssertRouteHop(context, recorder, probe, seeded.TargetDcVmName, seeded.TargetNetBiosName, seeded.SourceDcIpAddress, seeded.TargetGatewayIpAddress, "target->source"))
        {
            return;
        }

        // 13) FOOTPRINT (f): record that exactly the expected three tagged VMs and two Internal lab switches
        //     stood up. The harness gate then tears everything down in its finally block and its fail-closed
        //     no-orphans check (VMs + switches + disks + template, all by run-tag prefix) is the authoritative
        //     zero-orphans assertion for this 3-VM / 2-switch topology.
        AssertExpectedFootprint(context, recorder, probe, seeded);
    }

    /// <summary>
    /// Asserts a referenced-but-absent lab switch was auto-created as Internal by the runtime. Reads the
    /// type live from Get-VMSwitch, polling briefly since the switch is created during provisioning.
    /// </summary>
    private bool AssertLabSwitchInternal(
        ScenarioContext context,
        FindingRecorder recorder,
        HyperVProbe probe,
        string switchName,
        string label)
    {
        string? switchType = WaitForSwitch(probe, switchName, TimeSpan.FromMinutes(2));
        if (switchType is null)
        {
            recorder.RecordFailure(
                context.Host, Name, "validate-switch", FindingSeverity.Error,
                $"Runtime never created the {label} lab switch '{switchName}'",
                "The plan was startable and the VMs reached Running, but the referenced-but-absent lab switch never appeared " +
                "on the host. The routed topology needs both forests' switches present as Internal; check the app's deploy logs.");
            return false;
        }

        if (!string.Equals(switchType, "Internal", StringComparison.OrdinalIgnoreCase))
        {
            recorder.RecordFailure(
                context.Host, Name, "validate-switch", FindingSeverity.Error,
                $"Auto-created {label} lab switch '{switchName}' has the wrong type '{switchType}' (expected Internal)",
                $"A lab auto-create should produce an Internal switch (host-and-guests, no physical uplink); a '{switchType}' " +
                "switch would change the segment's connectivity and could collapse the routed boundary. Check the switch-creation step.");
            return false;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "validate-switch",
            Severity = FindingSeverity.Info,
            Title = $"{label} lab switch '{switchName}' auto-created as Internal",
            Detail = "Read live from Hyper-V via Get-VMSwitch. The two forests sit on distinct Internal switches, so their only " +
                     "path to each other is through the router - there is no same-L2 shortcut."
        });
        return true;
    }

    /// <summary>
    /// Proof (b) + (c): reads the app's step log to assert the router's enableRouterRouting success PRECEDES
    /// the first prepareForestTrustDns start (the #922 RouterReady-&gt;prepareDns ordering), and that
    /// validateCrossSwitchRouting is terminally Skipped (the correct outcome for two forest roots with no
    /// router-dependent guest). Waits for routerReady first so every router-tail step has a terminal event.
    /// </summary>
    private void AssertRoutingPrecededTrustPrep(
        ScenarioContext context,
        FindingRecorder recorder,
        HyperVProbe probe,
        DeployStepLogProbe stepLog,
        AppLogWindow logWindow,
        SeededForestTrustRoutedTemplate seeded)
    {
        // routerReady is the terminal router step; waiting for it guarantees enableRouterRouting and
        // validateCrossSwitchRouting already have run.end events on disk.
        DeployStepOutcome? routerReady = stepLog.WaitForStepTerminal(
            seeded.RouterVmName,
            StepRouterReady,
            logWindow,
            RouterReadyBudget,
            abortIf: () => VmIsGone(probe, seeded.RouterVmName));

        if (routerReady != DeployStepOutcome.Success)
        {
            recorder.RecordFailure(
                context.Host, Name, "ordering", FindingSeverity.Error,
                "Router never reached routerReady=success, so the routing/trust ordering cannot be asserted",
                $"Observed routerReady='{(routerReady is null ? "no terminal event" : routerReady.ToString())}' on " +
                $"'{seeded.RouterVmName}'. The trust validated in-guest, but the router-tail step log is incomplete; check the " +
                "app's structured event log.");
            return;
        }

        // (c) validateCrossSwitchRouting must be terminally Skipped here - correct, not a gap (see class doc).
        DeployStepOutcome? crossSwitch = stepLog.ReadStepTerminal(seeded.RouterVmName, StepValidateCrossSwitchRouting, logWindow);
        if (crossSwitch == DeployStepOutcome.Skipped)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "ordering",
                Severity = FindingSeverity.Info,
                Title = "validateCrossSwitchRouting terminally Skipped (correct for two forest roots)",
                Detail = "Cross-switch routing validation runs only for a router-dependent guest (a replica DC or domain-join " +
                         "member behind the router), which a FirstDomainController never is. Neither forest root qualifies, so " +
                         "the step is correctly skipped; the DC-to-DC routing is proven by the trust and the route-hop check. " +
                         "Driving this step to success needs a dedicated domain-join-across-router topology (out of scope here)."
            });
        }
        else if (crossSwitch == DeployStepOutcome.Failed)
        {
            recorder.RecordFailure(
                context.Host, Name, "ordering", FindingSeverity.Error,
                "validateCrossSwitchRouting FAILED (expected Skipped)",
                "On a two-forest-root routed topology this step must be terminally skipped; a failure is a regression to catch.");
        }
        else
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "ordering",
                Severity = FindingSeverity.Warning,
                Title = $"validateCrossSwitchRouting outcome '{(crossSwitch is null ? "no terminal event" : crossSwitch.ToString())}' (expected Skipped)",
                Detail = "A success would mean a router-dependent guest was present (the topology changed); no terminal event " +
                         "means the step log is incomplete. Neither is expected for this rung."
            });
        }

        // (b) enableRouterRouting success timestamp must precede the FIRST prepareForestTrustDns start on
        //     either DC. This is the live corroboration that #922's RouterReady->prepareDns edge held.
        DateTimeOffset? routingSuccess = stepLog.ReadStepTerminalTimestamp(seeded.RouterVmName, StepEnableRouterRouting, logWindow, DeployStepOutcome.Success);
        DateTimeOffset? dnsStartSource = stepLog.ReadStepStartTimestamp(seeded.SourceDcVmName, StepPrepareForestTrustDns, logWindow);
        DateTimeOffset? dnsStartTarget = stepLog.ReadStepStartTimestamp(seeded.TargetDcVmName, StepPrepareForestTrustDns, logWindow);
        DateTimeOffset? firstDnsPrepStart = EarliestNonNull(dnsStartSource, dnsStartTarget);

        if (routingSuccess is null || firstDnsPrepStart is null)
        {
            recorder.RecordFailure(
                context.Host, Name, "ordering", FindingSeverity.Error,
                "Could not read the router-routing / trust-prep step timestamps to assert ordering",
                $"enableRouterRouting success ts='{routingSuccess?.ToString("o") ?? "(none)"}', first prepareForestTrustDns " +
                $"start ts='{firstDnsPrepStart?.ToString("o") ?? "(none)"}'. Both are required to assert the #922 " +
                "RouterReady->prepareDns ordering from the log.");
            return;
        }

        if (routingSuccess.Value < firstDnsPrepStart.Value)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "ordering",
                Severity = FindingSeverity.Info,
                Title = "ORDERING PROVEN: router enableRouterRouting=success precedes the first prepareForestTrustDns start",
                Detail = $"enableRouterRouting succeeded at {routingSuccess.Value:o}; the first forest-trust DNS prep started at " +
                         $"{firstDnsPrepStart.Value:o} ({(firstDnsPrepStart.Value - routingSuccess.Value).TotalSeconds:F0}s later). " +
                         "This is the live corroboration of the finding-83 planner fix (#922): the trust stage ran only after the " +
                         "router was routing between the two subnets, so the trust did not race routing behind the #916 retries."
            });
        }
        else
        {
            recorder.RecordFailure(
                context.Host, Name, "ordering", FindingSeverity.Error,
                "ORDERING VIOLATED: forest-trust DNS prep started before the router finished enabling routing",
                $"first prepareForestTrustDns start {firstDnsPrepStart.Value:o} is NOT after enableRouterRouting success " +
                $"{routingSuccess.Value:o}. The trust stage is not ordered after router-routing-ready - the #922 " +
                "RouterReady->prepareDns edge did not hold for this routed topology. Route this to the planner fix owner.");
        }
    }

    /// <summary>
    /// Proof (e): confirms the DC reaches the peer DC (on the other subnet) via THIS DC's gateway - the
    /// router's LAN leg - and that the peer is reachable, read live from the guest with Find-NetRoute +
    /// Test-NetConnection. A same-subnet shortcut or a collapsed topology shows up as the wrong next hop.
    /// </summary>
    private bool AssertRouteHop(
        ScenarioContext context,
        FindingRecorder recorder,
        HyperVProbe probe,
        string vmName,
        string netBiosName,
        string peerDcIp,
        string expectedNextHop,
        string direction)
    {
        var routeProbe = new GuestRouteProbe();
        GuestRouteInfo? route = routeProbe.QueryRoute(
            vmName,
            netBiosName,
            peerDcIp,
            RouteHopBudget,
            abortIf: () => VmIsGone(probe, vmName));

        if (route is null)
        {
            recorder.RecordFailure(
                context.Host, Name, "route-hop", FindingSeverity.Error,
                $"No off-link route to peer DC '{peerDcIp}' appeared on '{vmName}' ({direction})",
                $"Could not read a next hop to '{peerDcIp}' over PowerShell Direct on '{vmName}' within the timeout. The trust " +
                "validated, but the guest routing evidence that it crossed the router is missing. See the console output.");
            return false;
        }

        bool ok = string.Equals(route.NextHop, expectedNextHop, StringComparison.OrdinalIgnoreCase) && route.Reachable;
        if (!ok)
        {
            recorder.RecordFailure(
                context.Host, Name, "route-hop", FindingSeverity.Error,
                $"DC '{vmName}' does not reach peer '{peerDcIp}' through the router ({direction})",
                $"Expected next hop '{expectedNextHop}' (the router's LAN leg on this DC's subnet) and a reachable peer, but the " +
                $"guest reported NextHop='{route.NextHop}', Reachable={route.Reachable}. A next hop that is not the router (or an " +
                "unreachable peer) means the trust traffic did not cross the routed boundary as templated.");
            return false;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "route-hop",
            Severity = FindingSeverity.Info,
            Title = $"{direction}: DC '{vmName}' reaches peer '{peerDcIp}' via the router (next hop {route.NextHop})",
            Detail = "Read live from the guest with Find-NetRoute + Test-NetConnection: the route to the peer DC on the other " +
                     "subnet uses this DC's gateway (the router's LAN leg) as its next hop, so the trust crossed the router - " +
                     "not a same-subnet shortcut."
        });
        return true;
    }

    /// <summary>
    /// Proof (f) positive half: records that exactly the three expected tagged VMs are present and both lab
    /// switches exist. The gate's post-run teardown + fail-closed no-orphans check is the authoritative
    /// zero-orphans assertion (there is no product cleanup on a successful deploy to observe mid-run).
    /// </summary>
    private void AssertExpectedFootprint(
        ScenarioContext context,
        FindingRecorder recorder,
        HyperVProbe probe,
        SeededForestTrustRoutedTemplate seeded)
    {
        var expectedVms = new[] { seeded.RouterVmName, seeded.SourceDcVmName, seeded.TargetDcVmName };
        var missing = expectedVms.Where(n =>
        {
            try { return probe.GetVm(n) is null; }
            catch { return false; }
        }).ToList();

        if (missing.Count > 0)
        {
            recorder.RecordFailure(
                context.Host, Name, "footprint", FindingSeverity.Error,
                $"{missing.Count} of 3 expected VM(s) are no longer present after validation",
                $"Missing VMs: [{string.Join(", ", missing)}]. All three should still be Running after a successful deploy; a " +
                "vanished VM means a late rollback. The gate's no-orphans check still runs, but the footprint is not what was templated.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "footprint",
            Severity = FindingSeverity.Info,
            Title = "Expected footprint present: router + 2 DCs on 2 Internal switches",
            Detail = $"VMs [{string.Join(", ", expectedVms)}] and switches '{seeded.SwitchAName}'/'{seeded.SwitchBName}' all " +
                     "stood up as templated. The harness gate now tears them all down by run-tag and its fail-closed no-orphans " +
                     "check (VMs + switches + disks + template) proves nothing tagged survives."
        });
    }

    /// <summary>
    /// Confirms one DC promoted the expected forest, reading Get-ADForest/Get-ADDomain live from the guest.
    /// Records a failure and returns false when AD never answered, the app rolled the DC back, or the DC
    /// promoted a different domain than templated.
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
                $"Expected domain DNS '{expectedDnsName}', but the DC reported '{forest.DomainDnsName}'. The trust target would " +
                "not match, so the run is aborted before the trust check.");
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
    /// Confirms one DC reports a forest+bidirectional trust to the expected peer domain, reading Get-ADTrust
    /// live from the guest. Records a failure and returns false when no such trust appeared, the app rolled
    /// the DC back, or the trust is the wrong type/direction.
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
                      "(cross-forest DNS did not resolve across the router, wrong credential, or the create step threw). See the console output.");
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
                $"ForestTransitive={trust.ForestTransitive}. The trust either landed on the wrong target, is not a forest trust, " +
                "or is not bidirectional.");
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

    private static DateTimeOffset? EarliestNonNull(DateTimeOffset? a, DateTimeOffset? b)
    {
        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        return a.Value <= b.Value ? a : b;
    }

    private static string? WaitForSwitch(HyperVProbe probe, string switchName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var type = probe.GetSwitchType(switchName);
                if (type is not null)
                {
                    return type;
                }
            }
            catch
            {
                // transient enumeration hiccup; keep polling
            }

            Thread.Sleep(3000);
        }

        return null;
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
    /// True when the VM no longer exists. Swallows a transient inventory-probe failure by returning false,
    /// so a momentary Get-VM hiccup during a guest poll never aborts it or gets misread as a rollback; a
    /// genuine rollback (VM removed) reports true.
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
