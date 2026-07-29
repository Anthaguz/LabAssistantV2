using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.ResourceProvider;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// The forest-trust ROLLBACK proof (finding 79): the destructive companion to
/// <see cref="TemplateDeployForestTrustScenario"/>. Where the happy-path verb only ever exercises
/// prepare/create/validate, the runtime's cleanupForestTrust wrap
/// (<c>V2ForestTrustRuntimeStage.CleanupFailedOrCancelledAsync</c> - DeleteLocalSideOfTrustRelationship on
/// each anchor) fires ONLY on deploy cancellation/failure, so it had zero live coverage. This scenario
/// induces a cancel while a real trust is in place and proves the system rolls back cleanly with no orphans.
///
/// It reuses the exact #918 two-forest topology (same seeder, fixture, credential slot, and guest probes):
/// forest-alpha (alpha.lab / ALPHA) and forest-beta (beta.lab / BETA), each a FirstDomainController on one
/// shared Internal switch, linked by one bidirectional Forest trust. The only difference is the LIVE path:
/// instead of validating the finished trust, it Starts the deploy, waits for the app's own structured log to
/// report that the create-trust step STARTED (<c>deploy.forest-trust.create.start</c>, result=started - which
/// the runtime emits just before MarkTrustObjectsCreated and the atomic guest create call, so both forests
/// have necessarily promoted for the dependency-gated create to begin), then CANCELS by navigating the shell
/// away from Deploy. That fires <c>DeployPage.OnNavigatedFrom</c>, which requests user cancellation so the
/// runtime tears down everything it created.
///
/// TIMING - why the cancel targets the VALIDATE stage, not "mid-create": the guest create call goes through
/// <c>GuestStepTransportRetry.RunAsync</c>, which checks cancellation ONLY before the first attempt, and the
/// first New-ADTrust attempt is atomic - once it starts it runs to completion and emits create.end=success.
/// MarkTrustObjectsCreated (cleanup's precondition) runs just before that attempt. So a navigate-away cancel
/// cannot interrupt the create itself; it is instead observed at the following validate stage's cancellation
/// checkpoints, where the trust is fully created but not yet marked READY. Cleanup then removes a COMPLETE
/// real trust - a stronger no-orphans proof than interrupting a partial create.
///
/// The verdict (a single pure classifier, <see cref="ClassifyRollback"/>), reframed for the findings-86/87
/// moot-skip product behaviour: on cancel the runtime SKIPS the in-guest DeleteLocalSideOfTrust for any anchor
/// whose OWN VM is being torn down in the same cancel (run-created), because deleting a trust object on a disk
/// about to be wiped is MOOT - the trust dies with the disk. In THIS scenario BOTH DCs are run-created, so both
/// anchor deletes are skipped-as-moot and the cleanup stage emits an HONEST <c>cleanup.end</c>=skipped terminal
/// (not success). The authoritative proof therefore shifts from "cleanup.end=success" to the RUNTIME tearing
/// everything down promptly with zero orphans - a STRONGER proof (it pins that the runtime itself cleans up,
/// not the harness backstop). Strongest first:
///   PASS (authoritative): <c>create.end</c>=success (a real, fully-created trust existed) AND the cleanup wrap
///       reached an HONEST terminal - <c>cleanup.end</c>=skipped-as-moot (expected here, both anchors run-created)
///       OR =success (only if a surviving pre-existing anchor path ran) - AND ZERO ORPHANS before the gate
///       backstop: the runtime tore down every run-created VM + differencing disk within TeardownBudget. The
///       zero-orphans-before-backstop check is the CENTERPIECE - it directly proves findings 86 (mandatory
///       teardown no longer gated behind best-effort in-guest cleanup) and 87 (no ~30-min orphan-and-Running
///       window on a cancelled dual-DC deploy).
///   GATING FAIL: cleanup ran but reported residual (<c>cleanup.end</c>=failed - the finding-85 hard-fail this
///       verb's live PASS is coupled to the #924 cleanup-retry fix eliminating), or cleanup started but never
///       reached ANY terminal within budget (a hang - the finding-85 hang the fix must not reintroduce), or the
///       runtime left orphans / exceeded TeardownBudget (the finding-86/87 regression the centerpiece pins).
///   INCONCLUSIVE (never a false pass/fail, re-run): the deploy reached a READY trust before the cancel took
///       effect (validate.end=success, no cleanup), or no fully-created trust was observed (cancel too early),
///       or cleanup ran to an honest terminal but create.end=success was not confirmed, or the cleanup stage was
///       silent (no start + no terminal) yet the runtime still left zero orphans - no leak, but the honest stage
///       could not be confirmed, so re-run. The fix thread ALWAYS emits a terminal per cleanup.start, so a truly
///       silent case should never legitimately occur post-fix; this branch is defence-in-depth on the emit path.
///   MONEY: zero orphans host-side, read directly BEFORE the gate's backstop sweep - both DC VMs gone within
///       TeardownBudget, no run-tagged VMs / differencing disks survive, and the shared real base image is
///       intact. (The shared Internal switch is gate-owned - the app references but never created it - so it is
///       intentionally left to the gate backstop and not asserted gone here; this verb has no run-created switch.)
///   In-guest GuestTrustProbe is best-effort and NON-gating: once the app tears the DCs down the trust objects
///       are gone with them, so an in-guest read is typically not applicable and never fails the run.
///
/// The planning-only path (no admin password) mirrors #918: it proves the template seeds and reaches a
/// startable plan, then stops - there is nothing to cancel without a live deploy. Both DC VMs and the
/// template file carry the run tag so the gate's tag-sweep is the unconditional no-orphans backstop on every
/// path, including a mistimed/partial cancel.
/// </summary>
public sealed class TemplateDeployForestTrustRollbackScenario : IScenario
{
    // The real prepared Windows Server base image already in the user's catalog. Overridable so the
    // scenario is not pinned to one machine, defaulted to the known WS2022 image id (matches #918).
    private static string RealBaseImageId =>
        Environment.GetEnvironmentVariable("LABASSISTANT_SMOKE_BASE_IMAGE_ID") ?? "b0a5e0222022400080000000000000a1";

    // Budget from Start Deploy until the create-trust step reports started. It must absorb the WHOLE 2-DC
    // pipeline for BOTH forests plus DNS-prep before create can even begin, because the plan gates the trust
    // create behind both anchors being domainReady. Same worst-case accounting as #918's finding-82 window
    // (measured there from VMs-Running; here from Start Deploy, which adds provisioning), so it is set a few
    // minutes above the 25 min ForestReadinessBudget:
    //   - provision both VMs to Running .............. ~2-3 min
    //   - transport / PowerShell-Direct login ........ ~6 min, PLUS up to ~5 min TIME-BASED auth grace
    //   - installAdDomainServices (x2, concurrent) ... ~2.5 min
    //   - promoteFirstDomainController + domainReady .. ~1.5 + ~5 min
    //   - prepareForestTrustDns then create begins .... ~1 min
    // ~28 min worst case; rounded to 30 min. This only BOUNDS a silent hang: the wait returns the instant
    // create.start appears, and an app rollback trips the VmIsGone abort immediately, so a healthy run pays
    // nothing for the headroom.
    private static readonly TimeSpan CreateStartBudget = TimeSpan.FromMinutes(30);

    // Budget from the cancel until the cleanup wrap reports its terminal end. Cleanup deletes the local side
    // of the trust on each anchor over PowerShell Direct with the #916 transient-drop retries, so it needs
    // more than a couple of RPCs of headroom but runs before the VMs are removed. 8 min is generous.
    private static readonly TimeSpan CleanupObservationBudget = TimeSpan.FromMinutes(8);

    // Budget from cleanup.start until a TERMINAL cleanup.end lands. The terminal is exactly one of
    // success | failed | skipped (skipped-as-moot is the EXPECTED terminal here: both DC anchors are
    // run-created, so both in-guest deletes are correctly skipped). Skipped is near-instant (no in-guest work);
    // success/failed over PowerShell Direct with the #916 transient-drop retries can take longer, so this is
    // generous. If no terminal appears the wrap is treated as hung (a real finding), never as a clean completion.
    private static readonly TimeSpan CleanupEndBudget = TimeSpan.FromMinutes(6);

    // Budget from the cancel until both DC VMs are gone - the LOAD-BEARING finding-87 assertion, not merely a
    // safety bound. A correct prompt teardown of two VMs + differencing disks is host-side Remove-VM + disk
    // delete (~1-3 min, not compute-sensitive), so 6 min is a 2-3x margin on a shared box. Critically it also
    // catches a HALF regression: a single-side in-guest-cleanup block is ~15 min on its own, so any regression
    // to the old behaviour (mandatory teardown gated behind best-effort in-guest trust cleanup) blows 6 min and
    // fails. 10 min would let a single-side regression that happened to resolve fast slip; 6 min is the tripwire.
    private static readonly TimeSpan TeardownBudget = TimeSpan.FromMinutes(6);

    public string Name => "template-deploy-forest-trust-rollback";

    public string Capability => "Deploy";

    public ScenarioRequirements Requirements => ScenarioRequirements.HyperV;

    public void Run(ScenarioContext context)
    {
        var recorder = context.Recorder;
        var hyperV = context.RequireHyperV();
        var resources = hyperV.Provisioned;
        var probe = hyperV.Probe;
        bool live = GuestDirectoryProbe.HasAdminPassword;

        // 1) Make the REAL base image guest-configurable (never touches its id/path or VHDX), same as #918.
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
                "The forest-trust rollback scenario needs a prepared, bootable Windows Server base image registered in " +
                "the app catalog to promote two domain controllers and begin a trust. Register the image (Assets > Base " +
                "disks) or set LABASSISTANT_SMOKE_BASE_IMAGE_ID to an existing catalog id, then re-run.");
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

        // 2) Seed the SAME tagged two-forest + trust template #918 uses (reused verbatim via the seeder).
        var seeder = new TemplateSeeder(new AppDataLocations());
        SeededForestTrustTemplate seeded = seeder.SeedForestTrustTemplate(hyperV.Tagger, RealBaseImageId, resources.SwitchName);

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "seed-template",
            Severity = FindingSeverity.Info,
            Title = $"Seeded two-forest + trust V2 template '{seeded.TemplateName}' for the rollback proof",
            Detail = $"File '{seeded.FilePath}', source DC '{seeded.SourceDcVmName}' (forest '{seeded.SourceDnsName}'), " +
                     $"target DC '{seeded.TargetDcVmName}' (forest '{seeded.TargetDnsName}'), image '{RealBaseImageId}', " +
                     $"switch '{resources.SwitchName}'. Mode: {(live ? "LIVE (deploy, cancel during validate, prove clean rollback)" : "PLANNING-ONLY (no admin password supplied)")}."
        });

        // 2b) Clear this scenario's credential slot so the deploy uses the password we enter now; restore the
        //     prior store verbatim in the finally. Both DCs share the one localBootstrap slot.
        var credSeeder = new CredentialSlotSeeder(new AppDataLocations());
        var priorSlot = credSeeder.Capture(seeded.LocalBootstrapSlotKey);
        credSeeder.Remove(seeded.LocalBootstrapSlotKey);

        try
        {
            RunRollback(context, recorder, hyperV, probe, seeded, resources, live);
        }
        finally
        {
            credSeeder.Restore(priorSlot);
        }
    }

    private void RunRollback(
        ScenarioContext context,
        FindingRecorder recorder,
        HyperVScenarioResources hyperV,
        HyperVProbe probe,
        SeededForestTrustTemplate seeded,
        ProvisionedResources resources,
        bool live)
    {
        // 3) Drive Deploy > From Template to a startable plan (identical to #918 up to Start Deploy).
        var page = new DeployFromTemplatePage(context.Host);
        page.Open();
        recorder.Capture(context.Host, "forest-trust-rollback-deploy-opened");

        page.ReloadLibrary();
        if (!page.SelectTemplateByName(seeded.TemplateName, TimeSpan.FromSeconds(30)))
        {
            recorder.RecordFailure(
                context.Host, Name, "select-template", FindingSeverity.Error,
                $"Seeded forest-trust template '{seeded.TemplateName}' never appeared in the deploy library",
                $"The From Template selector did not list the seeded template even after reloading. Expected file: '{seeded.FilePath}'.");
            return;
        }

        recorder.Capture(context.Host, "forest-trust-rollback-deploy-selected");
        page.EvaluatePlan();

        string password = live
            ? Environment.GetEnvironmentVariable(GuestDirectoryProbe.PasswordEnvVar)!
            : "P@ssw0rd!HarnessPlaceholder";

        int resolved = page.ResolveCredentialSlots("Administrator", password, TimeSpan.FromSeconds(30));
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "credentials",
            Severity = FindingSeverity.Info,
            Title = $"Resolved {resolved} credential slot(s) for the forest-trust rollback template",
            Detail = live
                ? "Filled the local bootstrap slot with the real image admin password; the planner reuses it for each " +
                  "domain-admin, DSRM and the cross-forest trust credential."
                : "Filled the local bootstrap slot with a PLACEHOLDER to prove startability (never used without a deploy)."
        });
        page.EvaluatePlan();

        // 4) CORE ASSERTION (both modes): the two-forest + trust template must reach a startable plan.
        if (!page.WaitForStartEnabled(TimeSpan.FromSeconds(60)))
        {
            recorder.RecordFailure(
                context.Host, Name, "readiness", FindingSeverity.Error,
                "Start Deploy never became enabled for the forest-trust rollback template",
                $"The V2 plan did not reach a startable state. Status: '{page.ActionStatusText()}'.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "readiness",
            Severity = FindingSeverity.Info,
            Title = "Two-forest + trust plan is startable",
            Detail = "Import, plan evaluation, credential-slot resolution and a startable plan all succeeded."
        });
        recorder.Capture(context.Host, "forest-trust-rollback-deploy-startable");

        if (!live)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "gated",
                Severity = FindingSeverity.Warning,
                Title = "Live rollback proof skipped: no admin password supplied",
                Detail = $"Set the '{GuestDirectoryProbe.PasswordEnvVar}' environment variable to the real base-image " +
                         "local Administrator password before running, and the scenario will Start Deploy, wait for the " +
                         "create-trust step to start, cancel by navigating away, and prove the cleanup wrap ran with zero " +
                         "orphans. The plan was proven startable, but no VMs were created (no orphan risk)."
            });
            return;
        }

        // 5) LIVE: open the step-log window BEFORE Start Deploy so every trust marker this run emits falls
        //    inside it, then Start.
        var stepLog = new DeployStepLogProbe(new AppDataLocations());
        var logWindow = stepLog.OpenWindow();
        page.StartDeploy();
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "start-deploy",
            Severity = FindingSeverity.Info,
            Title = $"Start Deploy clicked for forest-trust rollback template '{seeded.TemplateName}'",
            Detail = $"Promoting forests '{seeded.SourceDnsName}' and '{seeded.TargetDnsName}', then beginning a " +
                     "bidirectional forest trust between them - which the scenario cancels once the trust has been " +
                     "created, so the cancel lands during validation."
        });

        // 6) Wait for BOTH DCs to reach Running before arming the VmIsGone abort (which reads null-as-gone,
        //    so it must not run before the VMs exist).
        var vmNames = new[] { seeded.SourceDcVmName, seeded.TargetDcVmName };
        if (!WaitForAllVmsRunning(probe, vmNames, TimeSpan.FromMinutes(10)))
        {
            recorder.RecordFailure(
                context.Host, Name, "provision", FindingSeverity.Error,
                "Both forest-trust DC VMs did not reach Running after Start Deploy",
                "Provisioning did not bring both DCs up, so the create-trust step could never start and there is nothing " +
                "to cancel. Check the app's deploy logs.");
            return;
        }
        recorder.Capture(context.Host, "forest-trust-rollback-vms-running");

        // 7) PRECONDITION + INJECTION TIMING: wait until the create-trust step STARTS. The runtime emits this
        //    only after both forests promoted (the create is dependency-gated) and just before
        //    MarkTrustObjectsCreated + the atomic guest create call, so when it appears there are real trust
        //    artifacts to roll back. Because the create attempt cannot be interrupted mid-flight (see the class
        //    doc), navigating now lets the OnNavigatedFrom cancellation propagate and be observed at the
        //    following validate stage - where the trust is fully created but not yet READY. Abort fast if the
        //    app rolls a DC back.
        bool createStarted = stepLog.WaitForTrustEvent(
            DeployStepLogProbe.TrustCreateStartEvent,
            "started",
            logWindow,
            CreateStartBudget,
            abortIf: () => VmIsGone(probe, seeded.SourceDcVmName) || VmIsGone(probe, seeded.TargetDcVmName));

        if (!createStarted)
        {
            bool rolledBack = VmIsGone(probe, seeded.SourceDcVmName) || VmIsGone(probe, seeded.TargetDcVmName);
            recorder.RecordFailure(
                context.Host, Name, "await-create", FindingSeverity.Error,
                rolledBack
                    ? "A DC was rolled back before the create-trust step started"
                    : "The create-trust step never started within the budget",
                rolledBack
                    ? "The deploy failed and tore a DC down before the trust create began, so this run could not inject a " +
                      "cancel against a real trust. The gate still verifies no orphans; re-run to exercise the rollback path."
                    : $"No '{DeployStepLogProbe.TrustCreateStartEvent}' (result=started) appeared within {CreateStartBudget.TotalMinutes:N0} " +
                      "min. Both forests must promote before the trust create begins; a promotion likely hung. Check the deploy logs.");
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "await-create",
            Severity = FindingSeverity.Info,
            Title = "Create-trust step started; trust objects are in place - injecting cancel now",
            Detail = "The app logged deploy.forest-trust.create.start (result=started). Both forests promoted (the create is " +
                     "gated behind both anchors) and MarkTrustObjectsCreated has run, so cleanup has real artifacts to remove. " +
                     "Navigating the shell away from Deploy to request cancellation; the atomic create attempt completes and the " +
                     "cancel is observed at the following validate stage, before the trust is marked ready."
        });

        // 8) INJECT THE CANCEL: navigate the shell away from Deploy. DeployPage.OnNavigatedFrom requests user
        //    cancellation synchronously, so the runtime rolls back the resources it created.
        var nav = new ShellNav(context.Host);
        nav.NavigateTo("Machines");
        recorder.Capture(context.Host, "forest-trust-rollback-cancelled");
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "cancel",
            Severity = FindingSeverity.Info,
            Title = "Navigated away from Deploy to cancel the in-flight forest-trust deploy",
            Detail = "The shell left the Deploy page while a real trust was in place, which fires the page's OnNavigatedFrom " +
                     "cancellation so the app tears down everything it created (the cleanupForestTrust wrap plus the VMs/disks)."
        });

        AssertCleanRollback(context, recorder, hyperV, probe, stepLog, logWindow, seeded, resources);
    }

    /// <summary>
    /// Reads the app's own account of the rollback and proves it was clean via a single pure verdict
    /// (<see cref="ClassifyRollback"/>). The authoritative PASS requires a real, fully-created trust
    /// (create.end=success), an HONEST cleanup terminal (skipped-as-moot - expected here since both DCs are
    /// run-created - or success), AND zero orphans before the gate backstop (the runtime tore down every
    /// run-created VM + differencing disk within TeardownBudget). Zero-orphans-before-backstop is the gating
    /// centerpiece (findings 86/87). A best-effort non-gating in-guest note follows on the pass path.
    /// </summary>
    private void AssertCleanRollback(
        ScenarioContext context,
        FindingRecorder recorder,
        HyperVScenarioResources hyperV,
        HyperVProbe probe,
        DeployStepLogProbe stepLog,
        AppLogWindow logWindow,
        SeededForestTrustTemplate seeded,
        ProvisionedResources resources)
    {
        // Give the app time to run cleanupForestTrust and report its terminal end. The wrap always emits one
        // cleanup.start then one cleanup.end per trust, so waiting for the start first is the natural order.
        bool cleanupStarted = stepLog.WaitForTrustEvent(
            DeployStepLogProbe.TrustCleanupStartEvent, "started", logWindow, CleanupObservationBudget);

        // After the start, wait for the SINGLE terminal cleanup.end - success | skipped | failed - over one
        // combined budget, polling all three. skipped-as-moot is the EXPECTED terminal here (both anchor VMs
        // are being torn down, so the in-guest deletes are correctly skipped); success only if a surviving
        // pre-existing anchor path ran; failed is a residual (finding-85). Each finding below is gated on
        // actually observing its terminal, so a cleanup that starts then hangs/throws between start and end can
        // never be mistaken for a clean completion.
        bool cleanupSucceeded = false;
        bool cleanupSkipped = false;
        bool cleanupResidual = false;
        if (cleanupStarted)
        {
            var endDeadline = DateTime.UtcNow + CleanupEndBudget;
            while (true)
            {
                // Read ALL three terminals each poll and let ClassifyRollback's precedence decide, rather than
                // breaking on the first-seen marker. This keeps the read layer from masking a residual behind a
                // co-occurring skipped/success marker (defence-in-depth over the "exactly one terminal per
                // cleanup.start" contract) so the classifier's residual-is-gating guarantee holds end to end.
                cleanupSucceeded = stepLog.ReadTrustEvent(DeployStepLogProbe.TrustCleanupEndEvent, "success", logWindow);
                cleanupSkipped = stepLog.ReadTrustEvent(DeployStepLogProbe.TrustCleanupEndEvent, "skipped", logWindow);
                cleanupResidual = stepLog.ReadTrustEvent(DeployStepLogProbe.TrustCleanupEndEvent, "failed", logWindow);

                if (cleanupSucceeded || cleanupSkipped || cleanupResidual)
                {
                    break;
                }

                if (DateTime.UtcNow >= endDeadline)
                {
                    break;
                }

                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
        }

        // The two qualifying log facts. create.end=success proves a REAL, fully-created trust existed (the atomic
        // guest New-ADTrust attempt completed) and is the positive precondition of the authoritative PASS.
        // validate.end=success proves the trust reached READY, which only happens if the cancel landed after
        // the whole trust was already established.
        bool createEnded = stepLog.ReadTrustEvent(DeployStepLogProbe.TrustCreateEndEvent, "success", logWindow);
        bool trustValidated = stepLog.ReadTrustEvent(DeployStepLogProbe.TrustValidateEndEvent, "success", logWindow);

        // The CENTERPIECE: probe the host directly, BEFORE the gate's backstop sweep, for what the RUNTIME's own
        // cancel teardown removed. This is the finding-86/87 proof and it gates the PASS.
        var orphans = ProbeOrphans(context, recorder, hyperV, probe, seeded, resources);
        if (orphans.EnumerationFailed)
        {
            // The no-orphans invariant cannot be verified here (Hyper-V/disk enumeration threw); ProbeOrphans
            // already recorded the Error and the gate backstop still runs. Without the centerpiece we cannot
            // assert PASS, so stop.
            return;
        }

        var verdict = ClassifyRollback(
            cleanupStarted, cleanupSucceeded, cleanupSkipped, cleanupResidual, createEnded, trustValidated,
            orphans.NoTaggedLeftovers, orphans.TeardownWithinBudget);

        switch (verdict)
        {
            case RollbackVerdict.Pass:
                // AUTHORITATIVE: a real, fully-created trust existed, the cleanup wrap reached an honest terminal,
                // and the RUNTIME tore everything down with zero orphans. The heart of the reframed finding 79.
                recorder.Record(new Finding
                {
                    Scenario = Name,
                    Step = "cleanup-ran",
                    Severity = FindingSeverity.Info,
                    Title = cleanupSkipped
                        ? "AUTHORITATIVE: the cleanup wrap honestly SKIPPED both moot in-guest deletes; the runtime tore everything down"
                        : "AUTHORITATIVE: the cleanupForestTrust wrap executed to success on a fully-created trust",
                    Detail = cleanupSkipped
                        ? "The app's structured log shows deploy.forest-trust.create.end (result=success) - a real, complete " +
                          "bidirectional trust existed - followed by deploy.forest-trust.cleanup.start (result=started) and " +
                          "deploy.forest-trust.cleanup.end (result=skipped): both trust anchors' own VMs were being torn down in " +
                          "the same cancel, so the runtime correctly skipped the moot in-guest DeleteLocalSideOfTrust (the trust " +
                          "object dies with the disk) and instead relied on the mandatory VM/disk teardown. This is the honest " +
                          "moot-skip terminal (findings 86/87), and the zero-orphans check below proves the teardown happened."
                        : "The app's structured log shows deploy.forest-trust.create.end (result=success) - a real, complete " +
                          "bidirectional trust existed - followed by deploy.forest-trust.cleanup.start (result=started) and " +
                          "deploy.forest-trust.cleanup.end (result=success): a surviving pre-existing anchor path ran the in-guest " +
                          "DeleteLocalSideOfTrustRelationship as its no-orphans mechanism. First live coverage of the cleanup wrap (#916)."
                });
                recorder.Record(new Finding
                {
                    Scenario = Name,
                    Step = "cancel-during-validate",
                    Severity = FindingSeverity.Info,
                    Title = "The cancel landed after the trust was created but before it was READY; the runtime rolled back cleanly",
                    Detail = "create.end=success with no validate.end=success in the window: the guest create attempt is atomic and " +
                             "completes once started, so the cancel was designed to land during the following validate stage, where the " +
                             "trust is fully created but not yet marked ready. The runtime then removed a complete real trust's artifacts " +
                             "and tore down all run-created resources - a stronger no-orphans proof than interrupting a partial create."
                });
                RecordOrphanFinding(recorder, orphans, seeded, gating: true);
                RecordBestEffortInGuest(recorder, probe, seeded);
                return;

            case RollbackVerdict.FailCleanupResidual:
                recorder.RecordFailure(
                    context.Host, Name, "cleanup-ran", FindingSeverity.Error,
                    "cleanupForestTrust ran but reported residual (did not fully remove the trust)",
                    "deploy.forest-trust.cleanup.start ran and the wrap executed, but cleanup.end reported result=failed - one or both " +
                    "local sides of the trust may remain. NOTE: cleanup's in-guest trust-deletion hard-failing on the PSDirect " +
                    "credential-invalid transient is finding 85; this verb's live PASS is coupled to the #924 cleanup-retry fix landing, " +
                    "after which a residual end is a genuine regression. The zero-orphans check still follows to show host-side leakage.");
                RecordOrphanFinding(recorder, orphans, seeded, gating: true);
                return;

            case RollbackVerdict.FailCleanupHung:
                recorder.RecordFailure(
                    context.Host, Name, "cleanup-ran", FindingSeverity.Error,
                    "cleanupForestTrust started but never reached a terminal end",
                    $"'{DeployStepLogProbe.TrustCleanupStartEvent}' (result=started) appeared, but no terminal " +
                    $"'{DeployStepLogProbe.TrustCleanupEndEvent}' (success, skipped or failed) followed within {CleanupEndBudget.TotalMinutes:N0} min. " +
                    "The cleanup wrap began but did not complete - it likely hung between anchors (the finding-85 hang the #924 fix must " +
                    "NOT reintroduce), or the process was torn down mid-cleanup. Treated as a real finding. The zero-orphans check still " +
                    "follows to show whether resources leaked.");
                RecordOrphanFinding(recorder, orphans, seeded, gating: true);
                return;

            case RollbackVerdict.FailOrphansLeaked:
                recorder.RecordFailure(
                    context.Host, Name, "rollback-no-orphans", FindingSeverity.Error,
                    "the runtime's cancel teardown left orphans or exceeded the teardown budget (findings 86/87)",
                    "A real, fully-created trust was cancelled and the cleanup stage reached its honest terminal (or was silent), but " +
                    $"the runtime did NOT tear down every run-created resource within TeardownBudget ({TeardownBudget.TotalMinutes:N0} min): " +
                    "one or more run-tagged VMs / differencing disks survive, or both DCs were still present past the budget. This is the " +
                    "exact finding-86/87 regression the reframe pins - mandatory teardown must not be gated behind best-effort in-guest " +
                    "cleanup, and a cancelled dual-DC deploy must not leave a lingering orphan-and-Running window. The gate's backstop " +
                    "will still sweep tagged leftovers so the box is left clean, but the runtime owning prompt teardown is the product invariant.");
                RecordOrphanFinding(recorder, orphans, seeded, gating: true);
                return;

            case RollbackVerdict.InconclusiveReadyTrust:
                recorder.Record(new Finding
                {
                    Scenario = Name,
                    Step = "rollback-inconclusive",
                    Severity = FindingSeverity.Warning,
                    Title = "INCONCLUSIVE: the deploy reached a READY trust before the cancel took effect",
                    Detail = "The log shows deploy.forest-trust.validate.end=success and no cleanup start within the window: the trust " +
                             "was fully established and the deploy effectively completed before the navigate-away cancel landed, so there " +
                             "was nothing for cleanupForestTrust to roll back and the VMs remaining is a successful deploy, not a leak. " +
                             "This is NOT a false pass and NOT a failure; re-run to land the cancel during the validate stage (before the " +
                             "trust is marked ready). Host-side orphan verification still follows as non-gating evidence."
                });
                RecordOrphanFinding(recorder, orphans, seeded, gating: false);
                return;

            case RollbackVerdict.InconclusiveNoCreate:
                recorder.Record(new Finding
                {
                    Scenario = Name,
                    Step = "rollback-inconclusive",
                    Severity = FindingSeverity.Warning,
                    Title = "INCONCLUSIVE: no fully-created trust was observed before teardown",
                    Detail = "Neither deploy.forest-trust.create.end=success nor a cleanup start appeared in the window: the cancel " +
                             "likely landed before MarkTrustObjectsCreated, so there were no trust artifacts for cleanupForestTrust to " +
                             "remove (its precondition, TrustObjectsCreated, was never set). This is NOT a false pass and NOT a failure; " +
                             "re-run to land the cancel after the create attempt commits. Host-side orphan verification still follows."
                });
                RecordOrphanFinding(recorder, orphans, seeded, gating: false);
                return;

            case RollbackVerdict.InconclusiveCleanupWithoutCreateEnd:
                recorder.Record(new Finding
                {
                    Scenario = Name,
                    Step = "rollback-inconclusive",
                    Severity = FindingSeverity.Warning,
                    Title = "INCONCLUSIVE: cleanup reached an honest terminal but a fully-created trust was not confirmed",
                    Detail = "deploy.forest-trust.cleanup.start -> cleanup.end (success or skipped) appeared, but no deploy.forest-trust." +
                             "create.end=success was observed in the window, so the authoritative PASS precondition (a real, fully-created " +
                             "trust) cannot be asserted. The cleanup wrap did run, but this run cannot claim it acted on a complete trust. " +
                             "This is NOT a false pass and NOT a failure; re-run. Host-side orphan verification still follows."
                });
                RecordOrphanFinding(recorder, orphans, seeded, gating: false);
                return;

            case RollbackVerdict.InconclusiveSilentButClean:
                recorder.Record(new Finding
                {
                    Scenario = Name,
                    Step = "rollback-inconclusive",
                    Severity = FindingSeverity.Warning,
                    Title = "INCONCLUSIVE: the runtime left zero orphans but the cleanup stage was silent (no honest terminal)",
                    Detail = "deploy.forest-trust.create.end=success shows a real trust existed and the runtime tore down every " +
                             "run-created resource with zero orphans, but NO cleanup.start and NO cleanup.end terminal appeared in the " +
                             "window. There is no leak, but the honest cleanup stage could not be confirmed. The fix thread always emits a " +
                             "terminal for every cleanup.start, so a truly silent case should not legitimately occur post-fix; if this " +
                             "branch fires it signals a broken emit path worth surfacing. This is NOT a false pass and NOT a failure; re-run."
                });
                RecordOrphanFinding(recorder, orphans, seeded, gating: false);
                return;
        }
    }

    /// <summary>
    /// The pure rollback verdict over the log-derived facts PLUS the host-side zero-orphans facts, extracted so
    /// it is unit-testable without a live deploy. Reframed for the findings-86/87 moot-skip behaviour: the
    /// authoritative PASS requires a fully-created trust (create.end=success), an HONEST cleanup terminal
    /// (success OR skipped-as-moot), AND zero orphans before the backstop (no tagged leftovers AND teardown
    /// within budget). A cleanup that reported residual or hung is a gating failure; a runtime that left orphans
    /// or blew the teardown budget on the cancel path is the gating finding-86/87 failure; any remaining
    /// ambiguity is a non-gating INCONCLUSIVE (never a false pass, never a false fail).
    /// </summary>
    internal static RollbackVerdict ClassifyRollback(
        bool cleanupStarted,
        bool cleanupSucceeded,
        bool cleanupSkipped,
        bool cleanupResidual,
        bool createEnded,
        bool trustValidated,
        bool noTaggedLeftovers,
        bool teardownWithinBudget)
    {
        bool zeroOrphans = noTaggedLeftovers && teardownWithinBudget;

        if (cleanupStarted)
        {
            // A residual terminal (cleanup.end=failed) is gating and must never be masked by a co-occurring
            // skipped flag; a genuine success terminal still wins over a stale residual (defensive - the live
            // path sets exactly one terminal).
            if (cleanupResidual && !cleanupSucceeded)
            {
                return RollbackVerdict.FailCleanupResidual;
            }

            // Started but no terminal of any kind within budget - a hang (the finding-85 hang), gating.
            if (!cleanupSucceeded && !cleanupSkipped && !cleanupResidual)
            {
                return RollbackVerdict.FailCleanupHung;
            }

            // Honest terminal (success or skipped-as-moot). The authoritative PASS additionally requires a
            // confirmed fully-created trust and zero orphans before the backstop.
            if (!createEnded)
            {
                return RollbackVerdict.InconclusiveCleanupWithoutCreateEnd;
            }

            return zeroOrphans ? RollbackVerdict.Pass : RollbackVerdict.FailOrphansLeaked;
        }

        // The cleanup stage never started. Distinguish "the deploy already finished" from "cancel too early"
        // from the defence-in-depth silent case.
        if (trustValidated)
        {
            return RollbackVerdict.InconclusiveReadyTrust;
        }

        if (!createEnded)
        {
            return RollbackVerdict.InconclusiveNoCreate;
        }

        // A real trust existed and the deploy was cancelled, but no cleanup terminal was observed. Orphans
        // decide: a leak is the gating finding-86/87 failure; zero orphans with a silent stage is a non-gating
        // re-run (no leak, but the honest cleanup stage could not be confirmed).
        return zeroOrphans ? RollbackVerdict.InconclusiveSilentButClean : RollbackVerdict.FailOrphansLeaked;
    }

    /// <summary>
    /// The possible outcomes of the rollback proof. PASS is the only clean success; the three Fail* outcomes are
    /// gating findings (residual cleanup, a hung cleanup, or the runtime leaking orphans / blowing the teardown
    /// budget on the cancel path); the four Inconclusive* outcomes are non-gating "re-run" verdicts that never
    /// report a false pass or a false fail.
    /// </summary>
    internal enum RollbackVerdict
    {
        Pass,
        FailCleanupResidual,
        FailCleanupHung,
        FailOrphansLeaked,
        InconclusiveReadyTrust,
        InconclusiveNoCreate,
        InconclusiveCleanupWithoutCreateEnd,
        InconclusiveSilentButClean
    }

    /// <summary>
    /// Probes the host directly for what the runtime's own cancel teardown removed, read BEFORE the gate's
    /// unconditional backstop sweep. Waits up to <see cref="TeardownBudget"/> for both DC VMs to disappear
    /// (the finding-87 timing assertion), then enumerates surviving run-tagged VMs / differencing disks and
    /// confirms the shared real base image is intact. Records NOTHING on the success path - the caller records
    /// the appropriate finding via <see cref="RecordOrphanFinding"/> so the gating severity follows the verdict.
    /// On a Hyper-V/disk enumeration failure it records an Error and returns <c>EnumerationFailed = true</c>.
    /// The shared Internal switch is gate-owned (the app never created it) so it is deliberately not asserted
    /// gone here; this verb has no run-created switch.
    /// </summary>
    private OrphanProbe ProbeOrphans(
        ScenarioContext context,
        FindingRecorder recorder,
        HyperVScenarioResources hyperV,
        HyperVProbe probe,
        SeededForestTrustTemplate seeded,
        ProvisionedResources resources)
    {
        var vmNames = new[] { seeded.SourceDcVmName, seeded.TargetDcVmName };
        bool bothGone = WaitForAllVmsGone(probe, vmNames, TeardownBudget);

        var appData = new AppDataLocations();
        string globalPrefix = hyperV.Tagger.GlobalPrefix + "-";

        List<string> survivingVms;
        List<string> orphanDisks = new();
        List<string> orphanDirs = new();
        try
        {
            survivingVms = probe.ListVmNames(globalPrefix)
                .Where(v => vmNames.Contains(v, StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var root in new[] { appData.DifferencingDiskBasePath, appData.VmBasePath })
            {
                if (Directory.Exists(root))
                {
                    orphanDisks.AddRange(Directory.EnumerateFiles(root, globalPrefix + "*", SearchOption.AllDirectories));
                    orphanDirs.AddRange(Directory.EnumerateDirectories(root, globalPrefix + "*", SearchOption.AllDirectories));
                }
            }
        }
        catch (Exception ex)
        {
            recorder.RecordFailure(
                context.Host, Name, "rollback-no-orphans", FindingSeverity.Error,
                "Could not verify the app's rollback left no orphans: Hyper-V/disk enumeration failed",
                $"Enumerating tagged VMs/disks threw '{ex.Message}', so the no-orphans invariant cannot be confirmed here. " +
                "The gate's backstop still runs; inspect Hyper-V manually for surviving '" + globalPrefix + "' resources.");
            return new OrphanProbe(EnumerationFailed: true, TeardownWithinBudget: bothGone, NoTaggedLeftovers: false,
                BaseImageIntact: true, SurvivingVms: Array.Empty<string>(), OrphanDisks: Array.Empty<string>(),
                OrphanDirs: Array.Empty<string>(), BothGone: bothGone, GlobalPrefix: globalPrefix);
        }

        // The shared base image must survive: the app's cancel cleanup must never delete resources it did not create.
        bool baseImageIntact = string.IsNullOrEmpty(resources.BaseDiskPath) || File.Exists(resources.BaseDiskPath);

        // "No tagged leftovers" folds every artifact the runtime should have removed plus the base-image-intact
        // invariant; TeardownBudget (bothGone) is kept distinct so the finding-87 timing regression is nameable.
        bool noTaggedLeftovers = survivingVms.Count == 0 && orphanDisks.Count == 0 && orphanDirs.Count == 0 && baseImageIntact;

        return new OrphanProbe(EnumerationFailed: false, TeardownWithinBudget: bothGone, NoTaggedLeftovers: noTaggedLeftovers,
            BaseImageIntact: baseImageIntact, SurvivingVms: survivingVms, OrphanDisks: orphanDisks, OrphanDirs: orphanDirs,
            BothGone: bothGone, GlobalPrefix: globalPrefix);
    }

    /// <summary>
    /// Records the zero-orphans MONEY finding (Info) when the runtime's cancel teardown left the box clean, or a
    /// leak finding otherwise (Error when <paramref name="gating"/>, else a non-gating Warning for the
    /// inconclusive lanes where surviving VMs are expected rather than a leak).
    /// </summary>
    private void RecordOrphanFinding(FindingRecorder recorder, OrphanProbe orphans, SeededForestTrustTemplate seeded, bool gating)
    {
        bool clean = orphans.NoTaggedLeftovers && orphans.TeardownWithinBudget;
        if (clean)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "rollback-no-orphans",
                Severity = FindingSeverity.Info,
                Title = "MONEY: the runtime's cancel path left zero orphans (verified before the gate backstop)",
                Detail = $"Both DC VMs ('{seeded.SourceDcVmName}', '{seeded.TargetDcVmName}') are gone within the " +
                         $"{TeardownBudget.TotalMinutes:N0}-min teardown budget, no '{orphans.GlobalPrefix}' VMs, differencing disks or " +
                         "folders survive, and the shared real base image is intact - so the runtime's own cancellation teardown (not the " +
                         "harness gate) promptly removed everything it created. This is the finding-86/87 proof: mandatory teardown is not " +
                         "gated behind best-effort in-guest cleanup, and there is no lingering orphan-and-Running window."
            });
            return;
        }

        int leaks = orphans.SurvivingVms.Count + orphans.OrphanDisks.Count + orphans.OrphanDirs.Count
            + (orphans.TeardownWithinBudget ? 0 : 1) + (orphans.BaseImageIntact ? 0 : 1);
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "rollback-no-orphans",
            Severity = gating ? FindingSeverity.Error : FindingSeverity.Warning,
            Title = $"The runtime's cancel path left {leaks} orphan(s) before the gate backstop",
            Detail = $"Surviving DC VMs: [{string.Join(", ", orphans.SurvivingVms)}] (both-gone-in-budget={orphans.TeardownWithinBudget}); " +
                     $"disk files: [{string.Join(", ", orphans.OrphanDisks)}]; folders: [{string.Join(", ", orphans.OrphanDirs)}]; base image " +
                     $"intact={orphans.BaseImageIntact}. On the cancel path this is a REAL product finding (findings 86/87 - the runtime's " +
                     "rollback did not promptly clean up), distinct from a harness issue: the harness only navigated away; the runtime owns " +
                     "the teardown. The gate's sweep will still remove tagged leftovers so the box is left clean."
        });
    }

    /// <summary>
    /// Host-side orphan facts from <see cref="ProbeOrphans"/>. <see cref="NoTaggedLeftovers"/> folds no surviving
    /// VMs/disks/folders AND the base-image-intact invariant; <see cref="TeardownWithinBudget"/> (both DCs gone
    /// within <see cref="TeardownBudget"/>) is kept distinct so the finding-87 timing regression is nameable.
    /// </summary>
    private readonly record struct OrphanProbe(
        bool EnumerationFailed,
        bool TeardownWithinBudget,
        bool NoTaggedLeftovers,
        bool BaseImageIntact,
        IReadOnlyList<string> SurvivingVms,
        IReadOnlyList<string> OrphanDisks,
        IReadOnlyList<string> OrphanDirs,
        bool BothGone,
        string GlobalPrefix);

    /// <summary>
    /// Best-effort, NON-gating in-guest read. Once the app tears the DCs down the trust objects are gone with
    /// them, so this is typically not applicable; it never fails the run and only adds colour when a DC still
    /// happens to be present at read time.
    /// </summary>
    private void RecordBestEffortInGuest(FindingRecorder recorder, HyperVProbe probe, SeededForestTrustTemplate seeded)
    {
        bool anyVmPresent = !VmIsGone(probe, seeded.SourceDcVmName) || !VmIsGone(probe, seeded.TargetDcVmName);
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "in-guest-best-effort",
            Severity = FindingSeverity.Info,
            Title = anyVmPresent
                ? "In-guest trust check skipped: a DC is mid-teardown (non-gating)"
                : "In-guest trust check not applicable: the DCs were torn down (non-gating)",
            Detail = "The authoritative rollback proof is the app's cleanup-wrap log plus the host-side zero-orphans check. An " +
                     "in-guest Get-ADTrust is best-effort only: the app removes the local side of the trust and then destroys the " +
                     "DC VMs, so the trust objects do not outlive the guests. This note never gates the run."
        });
    }

    private static bool WaitForAllVmsRunning(HyperVProbe probe, IReadOnlyList<string> vmNames, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (vmNames.All(name =>
            {
                var truth = probe.GetVm(name);
                return truth is not null && string.Equals(truth.State, "Running", StringComparison.OrdinalIgnoreCase);
            }))
            {
                return true;
            }

            Thread.Sleep(3000);
        }

        return false;
    }

    private static bool WaitForAllVmsGone(HyperVProbe probe, IReadOnlyList<string> vmNames, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (vmNames.All(name => VmIsGone(probe, name)))
            {
                return true;
            }

            Thread.Sleep(3000);
        }

        return vmNames.All(name => VmIsGone(probe, name));
    }

    /// <summary>
    /// True when the VM no longer exists. Swallows a transient inventory-probe failure by returning false so a
    /// momentary Get-VM hiccup is never misread as a rollback; a genuine removal reports true.
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
