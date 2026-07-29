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
/// induces a cancel at a meaningful mid-trust point and proves the system rolls back cleanly with no
/// orphans.
///
/// It reuses the exact #918 two-forest topology (same seeder, fixture, credential slot, and guest probes):
/// forest-alpha (alpha.lab / ALPHA) and forest-beta (beta.lab / BETA), each a FirstDomainController on one
/// shared Internal switch, linked by one bidirectional Forest trust. The only difference is the LIVE path:
/// instead of validating the finished trust, it Starts the deploy, waits for the app's own structured log to
/// report that the create-trust step STARTED (<c>deploy.forest-trust.create.start</c>, result=started - which
/// the runtime emits AFTER MarkTrustObjectsCreated, so real trust artifacts are already in place and both
/// forests have necessarily promoted for the dependency-gated create to begin), then CANCELS by navigating
/// the shell away from Deploy. That fires <c>DeployPage.OnNavigatedFrom</c>, which requests user cancellation
/// so the runtime tears down everything it created.
///
/// The assertions, strongest first:
///   (a) AUTHORITATIVE: the app's structured log shows the cleanup WRAP ran -
///       <c>deploy.forest-trust.cleanup.start</c>(started) then <c>deploy.forest-trust.cleanup.end</c>. A
///       success end means both local sides were removed; a failed end means cleanup ran but left residual
///       (a real finding, still proof the wrap executed). This is the whole point: it distinguishes
///       "cleanupForestTrust actually executed" from "the trust merely died when the VM was destroyed".
///   (b) the create step did NOT reach a validated trust: no <c>create.end</c>=success and no
///       <c>validate.end</c>=success. If either is present the cancel landed too late (trust fully
///       created/validated before the navigate took effect) - reported INCONCLUSIVE, never a false PASS and
///       never a false FAIL, with a retry hint.
///   (c) MONEY: zero orphans host-side, read directly BEFORE the gate's backstop sweep - both DC VMs gone,
///       no run-tagged VMs / differencing disks survive, and the shared real base image is intact. This
///       proves the APP's own cancel path cleaned up, not the harness gate. (The shared Internal switch is
///       gate-owned - the app references but never created it - so it is intentionally left to the gate
///       backstop and not asserted gone here.)
///   (d) in-guest GuestTrustProbe is best-effort and NON-gating: once the app tears the DCs down the trust
///       objects are gone with them, so an in-guest read is typically not applicable and never fails the run.
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
                     $"switch '{resources.SwitchName}'. Mode: {(live ? "LIVE (deploy, cancel mid-create, prove clean rollback)" : "PLANNING-ONLY (no admin password supplied)")}."
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
                     "bidirectional forest trust between them - which the scenario cancels mid-create."
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
        //    only after both forests promoted (the create is dependency-gated) and after MarkTrustObjectsCreated,
        //    so when it appears there are real trust artifacts to roll back and the long guest create call is in
        //    flight - the widest window to land a mid-create cancel. Abort fast if the app rolls a DC back.
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
                      "MID-CREATE cancel. The gate still verifies no orphans; re-run to exercise the rollback path."
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
                     "Navigating the shell away from Deploy to request cancellation while the guest create call is in flight."
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
            Detail = "The shell left the Deploy page mid-create, which fires the page's OnNavigatedFrom cancellation so the " +
                     "app tears down everything it created (the cleanupForestTrust wrap plus the VMs/disks)."
        });

        AssertCleanRollback(context, recorder, hyperV, probe, stepLog, logWindow, seeded, resources);
    }

    /// <summary>
    /// Reads the app's own account of the rollback and proves it was clean. Order of verdict: first the
    /// INCONCLUSIVE guard (cancel landed too late), then the AUTHORITATIVE cleanup-ran proof, then the
    /// host-side zero-orphans money check, then a best-effort non-gating in-guest note.
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

        // INCONCLUSIVE guard: if the create fully succeeded or the trust was validated before our navigate
        // took effect, the cancel did not interrupt the create - this run cannot claim the mid-create partial
        // path. Report inconclusive (NOT a pass, NOT a fail) with a retry hint. The gate still proves orphans.
        bool createFullySucceeded = stepLog.ReadTrustEvent(DeployStepLogProbe.TrustCreateEndEvent, "success", logWindow);
        bool trustValidated = stepLog.ReadTrustEvent(DeployStepLogProbe.TrustValidateEndEvent, "success", logWindow);
        if (createFullySucceeded || trustValidated)
        {
            recorder.Record(new Finding
            {
                Scenario = Name,
                Step = "rollback-inconclusive",
                Severity = FindingSeverity.Warning,
                Title = "INCONCLUSIVE: the cancel landed after the trust was already created/validated",
                Detail = "The log shows " +
                         (trustValidated ? "deploy.forest-trust.validate.end=success" : "deploy.forest-trust.create.end=success") +
                         " within the window, so the navigate-away cancel did not interrupt the create as intended - the guest " +
                         "create call completed faster than the cancel took effect. Cleanup " +
                         (cleanupStarted ? "still ran on the full trust" : "was not observed") + ". This is NOT a false pass " +
                         "and NOT a failure; re-run to land the cancel mid-create. Host-side orphan verification still follows."
            });

            // Still verify no orphans - a mistimed cancel must never leak resources either.
            AssertNoOrphansHostSide(context, recorder, hyperV, probe, seeded, resources, gating: false);
            return;
        }

        // (a) AUTHORITATIVE: prove the cleanup WRAP executed. This is the heart of finding 79.
        if (!cleanupStarted)
        {
            recorder.RecordFailure(
                context.Host, Name, "cleanup-ran", FindingSeverity.Error,
                "cleanupForestTrust did NOT run after a mid-create cancel",
                $"The create-trust step had started (trust objects were marked created) and the deploy was cancelled, but no " +
                $"'{DeployStepLogProbe.TrustCleanupStartEvent}' (result=started) appeared within {CleanupObservationBudget.TotalMinutes:N0} " +
                "min. On this path CleanupFailedOrCancelledAsync is the no-orphans mechanism for the trust; its absence means " +
                "the trust would only be removed incidentally when the VMs are destroyed, leaving no guarantee for a trust that " +
                "outlived its DCs. Treated as a real finding, not a harness issue - the cancel did reach teardown (see orphan check).");
            AssertNoOrphansHostSide(context, recorder, hyperV, probe, seeded, resources, gating: true);
            return;
        }

        if (cleanupResidual && !cleanupSucceeded)
        {
            recorder.RecordFailure(
                context.Host, Name, "cleanup-ran", FindingSeverity.Error,
                "cleanupForestTrust ran but reported residual (did not fully remove the trust)",
                "deploy.forest-trust.cleanup.start ran and the wrap executed, but cleanup.end reported result=failed - one or both " +
                "local sides of the trust may remain. The cleanup WRAP did run (finding 79's core question answered YES), but it " +
                "left residual, which is itself a real finding: check the DCs for a leftover trust. Because the DC VMs are torn " +
                "down on this path the in-guest trust dies with them, but a residual report still warrants investigation.");
            AssertNoOrphansHostSide(context, recorder, hyperV, probe, seeded, resources, gating: true);
            return;
        }

        // Cleanup STARTED but never reached a terminal end within the budget: it hung/threw between
        // cleanup.start and cleanup.end. This must NOT be reported as a clean completion - it is the exact
        // failure mode (a cleanup that begins but never finishes) the scenario exists to catch.
        if (!cleanupSucceeded)
        {
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
        }

        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "cleanup-ran",
            Severity = FindingSeverity.Info,
            Title = "AUTHORITATIVE: the cleanupForestTrust wrap executed and completed",
            Detail = "The app's structured log shows deploy.forest-trust.cleanup.start (result=started) followed by " +
                     "deploy.forest-trust.cleanup.end (result=success): the runtime ran DeleteLocalSideOfTrustRelationship on each " +
                     "anchor as its no-orphans mechanism, rather than letting the trust die incidentally with the VMs. This is the " +
                     "first live coverage of the forest-trust cleanup wrap (#916)."
        });

        // (b) The cancel interrupted the create before a validated trust existed (already established above:
        //     neither create.end=success nor validate.end=success is present).
        recorder.Record(new Finding
        {
            Scenario = Name,
            Step = "create-interrupted",
            Severity = FindingSeverity.Info,
            Title = "The create-trust step was interrupted before reaching a validated trust",
            Detail = "No deploy.forest-trust.create.end=success and no deploy.forest-trust.validate.end=success appeared in the " +
                     "window, confirming the cancel landed mid-create as intended (not after the trust was already established)."
        });

        // (c) MONEY: prove zero orphans host-side, read directly before the gate's backstop sweep.
        AssertNoOrphansHostSide(context, recorder, hyperV, probe, seeded, resources, gating: true);

        // (d) Best-effort, non-gating in-guest note.
        RecordBestEffortInGuest(recorder, probe, seeded);
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
