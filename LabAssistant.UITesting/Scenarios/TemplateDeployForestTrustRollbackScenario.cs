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
/// The verdict (a single pure classifier, <see cref="ClassifyRollback"/>), strongest first:
///   PASS (authoritative): BOTH <c>create.end</c>=success (a real, fully-created trust existed) AND the
///       cleanup WRAP ran to a success terminal (<c>cleanup.start</c>(started) then <c>cleanup.end</c>
///       =success). This distinguishes "cleanupForestTrust actually executed and removed a real trust" from
///       "the trust merely died when the VM was destroyed", and is the first live coverage of the wrap (#916).
///   GATING FAIL: cleanup ran but reported residual (<c>cleanup.end</c>=failed - EXPECTED until the finding-85
///       cleanup-retry fix lands, so this verb's live PASS is coupled to finding 85), or cleanup started but
///       never reached a terminal end (hung), or a full trust was created and cancelled yet cleanup never ran.
///   INCONCLUSIVE (never a false pass/fail, re-run): the deploy reached a READY trust before the cancel took
///       effect (validate.end=success, no cleanup), or no fully-created trust was observed (cancel too early),
///       or cleanup succeeded but create.end=success was not confirmed.
///   MONEY: zero orphans host-side, read directly BEFORE the gate's backstop sweep - both DC VMs gone, no
///       run-tagged VMs / differencing disks survive, and the shared real base image is intact. (The shared
///       Internal switch is gate-owned - the app references but never created it - so it is intentionally left
///       to the gate backstop and not asserted gone here.)
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

    // Budget from cleanup.start until a TERMINAL cleanup.end (success or failed) lands. Deleting the local
    // side on each anchor over PowerShell Direct with the #916 transient-drop retries can take longer than a
    // couple of RPCs, so this is generous; if no terminal appears the wrap is treated as hung (a real
    // finding), never as a clean completion.
    private static readonly TimeSpan CleanupEndBudget = TimeSpan.FromMinutes(6);

    // Budget from the cancel until both DC VMs are gone (the app's teardown stops + removes each VM and
    // deletes its differencing disk). Two VMs plus disk deletes settle well within this; 10 min bounds a
    // wedged teardown so the money orphan-check is not read prematurely.
    private static readonly TimeSpan TeardownBudget = TimeSpan.FromMinutes(10);

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
    /// (<see cref="ClassifyRollback"/>). The authoritative PASS requires BOTH a real, fully-created trust
    /// (create.end=success) AND a cleanup wrap that ran to a success terminal; the host-side zero-orphans money
    /// check then runs (gating on the fail/pass paths, non-gating on inconclusive), followed by a best-effort
    /// non-gating in-guest note on the pass path.
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
        // Give the app time to run cleanupForestTrust and report its terminal end. Cleanup runs before the
        // VMs are removed, so waiting for it first is the natural order.
        bool cleanupStarted = stepLog.WaitForTrustEvent(
            DeployStepLogProbe.TrustCleanupStartEvent, "started", logWindow, CleanupObservationBudget);

        // After the start, wait for a TERMINAL cleanup.end - success OR failed - over ONE combined budget,
        // polling both. The success finding below is gated on actually observing a success terminal, so a
        // cleanup that starts and then hangs/throws between cleanup.start and cleanup.end (or a residual end
        // that lands later than a fixed success-only grace) can never be mistaken for a clean completion.
        bool cleanupSucceeded = false;
        bool cleanupResidual = false;
        if (cleanupStarted)
        {
            var endDeadline = DateTime.UtcNow + CleanupEndBudget;
            while (true)
            {
                if (stepLog.ReadTrustEvent(DeployStepLogProbe.TrustCleanupEndEvent, "success", logWindow))
                {
                    cleanupSucceeded = true;
                    break;
                }

                if (stepLog.ReadTrustEvent(DeployStepLogProbe.TrustCleanupEndEvent, "failed", logWindow))
                {
                    cleanupResidual = true;
                    break;
                }

                if (DateTime.UtcNow >= endDeadline)
                {
                    break;
                }

                Thread.Sleep(TimeSpan.FromSeconds(3));
            }
        }

        // The two qualifying facts. create.end=success proves a REAL, fully-created trust existed (the atomic
        // guest New-ADTrust attempt completed) and is the positive precondition of the authoritative PASS.
        // validate.end=success proves the trust reached READY, which only happens if the cancel landed after
        // the whole trust was already established.
        bool createEnded = stepLog.ReadTrustEvent(DeployStepLogProbe.TrustCreateEndEvent, "success", logWindow);
        bool trustValidated = stepLog.ReadTrustEvent(DeployStepLogProbe.TrustValidateEndEvent, "success", logWindow);

        var verdict = ClassifyRollback(cleanupStarted, cleanupSucceeded, cleanupResidual, createEnded, trustValidated);
        switch (verdict)
        {
            case RollbackVerdict.Pass:
                // AUTHORITATIVE: the cleanup wrap ran to success ON A REAL, FULLY-CREATED TRUST. This is the
                // heart of finding 79 and its precondition is create.end=success, not merely "cleanup ran".
                recorder.Record(new Finding
                {
                    Scenario = Name,
                    Step = "cleanup-ran",
                    Severity = FindingSeverity.Info,
                    Title = "AUTHORITATIVE: the cleanupForestTrust wrap executed and completed on a fully-created trust",
                    Detail = "The app's structured log shows deploy.forest-trust.create.end (result=success) - a real, complete " +
                             "bidirectional trust existed - followed by deploy.forest-trust.cleanup.start (result=started) and " +
                             "deploy.forest-trust.cleanup.end (result=success): on the cancel the runtime ran " +
                             "DeleteLocalSideOfTrustRelationship on each anchor as its no-orphans mechanism, rather than letting the " +
                             "trust die incidentally with the VMs. This is the first live coverage of the forest-trust cleanup wrap (#916)."
                });
                recorder.Record(new Finding
                {
                    Scenario = Name,
                    Step = "cancel-during-validate",
                    Severity = FindingSeverity.Info,
                    Title = "The cancel landed after the trust was created but before it was READY; cleanup removed the real trust",
                    Detail = "create.end=success with no validate.end=success in the window: the guest create attempt is atomic and " +
                             "completes once started, so the cancel was designed to land during the following validate stage, where the " +
                             "trust is fully created but not yet marked ready. Cleanup then removed a complete real trust - a stronger " +
                             "no-orphans proof than interrupting a partial create."
                });
                AssertNoOrphansHostSide(context, recorder, hyperV, probe, seeded, resources, gating: true);
                RecordBestEffortInGuest(recorder, probe, seeded);
                return;

            case RollbackVerdict.FailCleanupResidual:
                recorder.RecordFailure(
                    context.Host, Name, "cleanup-ran", FindingSeverity.Error,
                    "cleanupForestTrust ran but reported residual (did not fully remove the trust)",
                    "deploy.forest-trust.cleanup.start ran and the wrap executed, but cleanup.end reported result=failed - one or both " +
                    "local sides of the trust may remain. The cleanup WRAP did run (finding 79's core question answered YES), but it left " +
                    "residual. NOTE: until the finding-85 fix lands (cleanup's in-guest trust-deletion is not yet wrapped in the " +
                    "transport-retry helper and hard-fails on the PSDirect credential-invalid transient), a residual end is EXPECTED on " +
                    "live runs - this verb's live PASS is coupled to finding 85. Because the DC VMs are torn down on this path the " +
                    "in-guest trust dies with them, but a residual report still warrants investigation.");
                AssertNoOrphansHostSide(context, recorder, hyperV, probe, seeded, resources, gating: true);
                return;

            case RollbackVerdict.FailCleanupHung:
                recorder.RecordFailure(
                    context.Host, Name, "cleanup-ran", FindingSeverity.Error,
                    "cleanupForestTrust started but never reached a terminal end",
                    $"'{DeployStepLogProbe.TrustCleanupStartEvent}' (result=started) appeared, but no terminal " +
                    $"'{DeployStepLogProbe.TrustCleanupEndEvent}' (success or failed) followed within {CleanupEndBudget.TotalMinutes:N0} min. " +
                    "The cleanup wrap began but did not complete - it likely hung or threw between deleting the local side on each " +
                    "anchor, or the process was torn down mid-cleanup. Treated as a real finding: the no-orphans wrap did not finish. " +
                    "Host-side orphan verification still follows to show whether resources leaked.");
                AssertNoOrphansHostSide(context, recorder, hyperV, probe, seeded, resources, gating: true);
                return;

            case RollbackVerdict.FailCleanupDidNotRun:
                recorder.RecordFailure(
                    context.Host, Name, "cleanup-ran", FindingSeverity.Error,
                    "cleanupForestTrust did NOT run after a full-create cancel",
                    "deploy.forest-trust.create.end=success shows a real, fully-created trust existed and the deploy was cancelled " +
                    $"(no validate.end=success), but no '{DeployStepLogProbe.TrustCleanupStartEvent}' (result=started) appeared within " +
                    $"{CleanupObservationBudget.TotalMinutes:N0} min. On this path CleanupFailedOrCancelledAsync is the no-orphans " +
                    "mechanism for the trust; its absence means the trust would only be removed incidentally when the VMs are destroyed, " +
                    "leaving no guarantee for a trust that outlived its DCs. Treated as a real finding, not a harness issue - the cancel " +
                    "did reach teardown (see orphan check).");
                AssertNoOrphansHostSide(context, recorder, hyperV, probe, seeded, resources, gating: true);
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
                             "was nothing for cleanupForestTrust to roll back. This is NOT a false pass and NOT a failure; re-run to land " +
                             "the cancel during the validate stage (before the trust is marked ready). Host-side orphan verification still follows."
                });
                AssertNoOrphansHostSide(context, recorder, hyperV, probe, seeded, resources, gating: false);
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
                AssertNoOrphansHostSide(context, recorder, hyperV, probe, seeded, resources, gating: false);
                return;

            case RollbackVerdict.InconclusiveCleanupWithoutCreateEnd:
                recorder.Record(new Finding
                {
                    Scenario = Name,
                    Step = "rollback-inconclusive",
                    Severity = FindingSeverity.Warning,
                    Title = "INCONCLUSIVE: cleanup ran to success but a fully-created trust was not confirmed",
                    Detail = "deploy.forest-trust.cleanup.start -> cleanup.end=success appeared, but no deploy.forest-trust.create.end" +
                             "=success was observed in the window, so the authoritative PASS precondition (a real, fully-created trust) " +
                             "cannot be asserted. The cleanup wrap did run, but this run cannot claim it removed a complete trust. This is " +
                             "NOT a false pass and NOT a failure; re-run. Host-side orphan verification still follows."
                });
                AssertNoOrphansHostSide(context, recorder, hyperV, probe, seeded, resources, gating: false);
                return;
        }
    }

    /// <summary>
    /// The pure rollback verdict over five log-derived facts, extracted so it is unit-testable without a live
    /// deploy. The authoritative PASS requires BOTH a fully-created trust (create.end=success) AND a cleanup
    /// wrap that ran to a success terminal; any ambiguity resolves to INCONCLUSIVE (never a false pass), and a
    /// cleanup that ran but left residual, hung, or failed to run at all on a real trust is a gating failure.
    /// </summary>
    internal static RollbackVerdict ClassifyRollback(
        bool cleanupStarted,
        bool cleanupSucceeded,
        bool cleanupResidual,
        bool createEnded,
        bool trustValidated)
    {
        if (cleanupStarted)
        {
            // The cleanup WRAP ran - finding 79's core question is answered YES. Qualify the terminal and the
            // fully-created precondition.
            if (cleanupResidual && !cleanupSucceeded)
            {
                return RollbackVerdict.FailCleanupResidual;
            }

            if (!cleanupSucceeded)
            {
                return RollbackVerdict.FailCleanupHung;
            }

            return createEnded ? RollbackVerdict.Pass : RollbackVerdict.InconclusiveCleanupWithoutCreateEnd;
        }

        // The cleanup wrap did NOT run. Distinguish "the deploy already finished" from "cleanup should have
        // run on a real trust but didn't".
        if (trustValidated)
        {
            return RollbackVerdict.InconclusiveReadyTrust;
        }

        if (!createEnded)
        {
            return RollbackVerdict.InconclusiveNoCreate;
        }

        return RollbackVerdict.FailCleanupDidNotRun;
    }

    /// <summary>
    /// The possible outcomes of the rollback proof. PASS is the only clean success; the three Fail* outcomes are
    /// gating findings; the three Inconclusive* outcomes are non-gating "re-run" verdicts that never report a
    /// false pass or a false fail.
    /// </summary>
    internal enum RollbackVerdict
    {
        Pass,
        FailCleanupResidual,
        FailCleanupHung,
        FailCleanupDidNotRun,
        InconclusiveReadyTrust,
        InconclusiveNoCreate,
        InconclusiveCleanupWithoutCreateEnd
    }

    /// <summary>
    /// Proves the APP's own cancel path removed everything it created, read directly from Hyper-V/disk BEFORE
    /// the gate's unconditional backstop sweep. Waits for both DC VMs to disappear, then asserts no run-tagged
    /// VMs / differencing disks survive and the shared real base image is intact. The shared Internal switch is
    /// gate-owned (the app never created it) so it is deliberately not asserted gone here.
    /// When <paramref name="gating"/> is false the result is recorded as evidence but a leak is still an Error.
    /// </summary>
    private void AssertNoOrphansHostSide(
        ScenarioContext context,
        FindingRecorder recorder,
        HyperVScenarioResources hyperV,
        HyperVProbe probe,
        SeededForestTrustTemplate seeded,
        ProvisionedResources resources,
        bool gating)
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
            return;
        }

        // The shared base image must survive: the app's cancel cleanup must never delete resources it did not create.
        bool baseImageIntact = string.IsNullOrEmpty(resources.BaseDiskPath) || File.Exists(resources.BaseDiskPath);

        int leaks = survivingVms.Count + orphanDisks.Count + orphanDirs.Count + (bothGone ? 0 : 1) + (baseImageIntact ? 0 : 1);
        if (leaks == 0)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "rollback-no-orphans",
                Severity = FindingSeverity.Info,
                Title = "MONEY: the app's cancel path left zero orphans (verified before the gate backstop)",
                Detail = $"Both DC VMs ('{seeded.SourceDcVmName}', '{seeded.TargetDcVmName}') are gone, no '{globalPrefix}' VMs, " +
                         "differencing disks or folders survive, and the shared real base image is intact - so the runtime's own " +
                         "cancellation teardown (not the harness gate) removed everything it created."
            });
            return;
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "rollback-no-orphans",
            Severity = gating ? FindingSeverity.Error : FindingSeverity.Warning,
            Title = $"The app's cancel path left {leaks} orphan(s) before the gate backstop",
            Detail = $"Surviving DC VMs: [{string.Join(", ", survivingVms)}] (both-gone={bothGone}); disk files: " +
                     $"[{string.Join(", ", orphanDisks)}]; folders: [{string.Join(", ", orphanDirs)}]; base image intact={baseImageIntact}. " +
                     "A mistimed cancel that leaves orphans would be a REAL product finding (the runtime's rollback did not clean up), " +
                     "distinct from a harness issue: the harness only navigated away; the runtime owns the teardown. The gate's sweep " +
                     "will still remove tagged leftovers so the box is left clean."
        });
    }

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
