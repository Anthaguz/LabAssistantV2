using System.Collections.Specialized;
using System.Diagnostics;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.ViewModels.Deploy;
using LabAssistant.WinUI.ViewModels.Templates;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Runtime-independent unit tests for <see cref="DeployQuickDeployViewModel"/>, the full-MVVM Quick
/// Deploy lane that replaced the imperative workspace/owner/composition trio. These exercise VM entry
/// management, editor draft validation gating, command CanExecute wiring, the nonce-debounced
/// auto-evaluate, readiness state transitions, and navigate-away cancellation through injected seams
/// (a synchronous UI marshaller and stubbed deployment services) with no WinUI dispatcher present.
/// </summary>
public sealed class DeployQuickDeployViewModelTests
{
    private const string SwitchName = "Lab-A";
    private const string CatalogId = "base-1";
    private const string CatalogPath = @"C:\Base\base-1.vhdx";
    private const string SecondCatalogId = "base-2";
    private const string SecondCatalogPath = @"C:\Base\base-2.vhdx";

    // A catalog with two base disks so the F26 "auto-select the single base disk" convenience
    // (TryAutoSelectSingleBaseDisk, which only fires when exactly one disk exists) does not engage. A
    // newly added VM then stays genuinely diskless, which is the bare-entry state these tests intend to
    // exercise. With a single-disk catalog the added VM would auto-inherit that disk and only lack a
    // switch, settling on a Warning rather than a Blocked readiness state.
    private static IReadOnlyList<VhdxCatalogItem> TwoDiskCatalog() =>
    [
        new VhdxCatalogItem { Id = CatalogId, Path = CatalogPath, OsName = "Windows Server", OsVersion = "2022", Generation = 2 },
        new VhdxCatalogItem { Id = SecondCatalogId, Path = SecondCatalogPath, OsName = "Windows Server", OsVersion = "2022", Generation = 2 },
    ];

    /// <summary>
    /// Holds the constructed view model together with the fakes and interaction counters a test needs
    /// to assert against.
    /// </summary>
    private sealed class Harness
    {
        public required DeployQuickDeployViewModel Vm { get; init; }

        public required FakePreflightService Preflight { get; init; }

        public required FakeV2PlanningCapabilityService Planning { get; init; }

        public required FakeV2RuntimeCapabilityService Runtime { get; init; }

        public List<string> RemovePrompts { get; } = [];

        public int ToggleCount { get; set; }

        public int TemplateEditorCount { get; set; }

        public bool ConfirmRemoveResult { get; set; } = true;
    }

    private static Harness CreateHarness(
        IReadOnlyList<string>? switches = null,
        IReadOnlyList<VhdxCatalogItem>? catalog = null,
        int autoEvaluateDelayMs = 60000)
    {
        switches ??= [SwitchName];
        catalog ??= [new VhdxCatalogItem { Id = CatalogId, Path = CatalogPath, OsName = "Windows Server", OsVersion = "2022", Generation = 2 }];

        var settings = new AppSettings { VmBasePath = @"C:\Labs", CatalogPath = @"C:\catalog.json" };
        var referenceData = new DeployReferenceDataService(
            new FakeAppSettingsStore(settings),
            new FakeVhdxCatalogStore(catalog),
            new FakeMachinesCapabilityService(switches),
            new FakeTemplatesCapabilityService(catalog
                .Select(item => new TemplatesVhdxCatalogItem
                {
                    Id = item.Id,
                    Path = item.Path,
                    OsName = item.OsName,
                    OsVersion = item.OsVersion,
                    Generation = item.Generation,
                    Signature = item.Signature
                })
                .ToList()),
            new FakeHyperVMachineAdminService(switches));

        var preflight = new FakePreflightService();
        var planning = new FakeV2PlanningCapabilityService();
        var runtime = new FakeV2RuntimeCapabilityService();

        Harness harness = null!;
        var vm = new DeployQuickDeployViewModel(
            referenceData,
            (_, _) =>
            {
                harness.TemplateEditorCount++;
                return Task.CompletedTask;
            },
            preflight,
            planning,
            runtime,
            action => action(),
            vmName =>
            {
                harness.RemovePrompts.Add(vmName);
                return Task.FromResult(harness.ConfirmRemoveResult);
            },
            () => harness.ToggleCount++,
            autoEvaluateDelayMs);

        harness = new Harness
        {
            Vm = vm,
            Preflight = preflight,
            Planning = planning,
            Runtime = runtime
        };

        return harness;
    }

    /// <summary>
    /// Activates the lane (seeding the default entry and loading reference data synchronously) and then
    /// promotes the seeded VM to a fully deploy-ready draft: a catalog-backed base disk plus a valid host
    /// switch, so readiness has no blocking compatibility issues.
    /// </summary>
    private static async Task ActivateReadyVmAsync(Harness harness)
    {
        var vm = harness.Vm;
        vm.ApplyShellState(isActive: true);

        var catalogOption = vm.VhdxCatalogItems.OfType<TemplateVhdxCatalogOption>().First();
        vm.SelectedVhdxCatalogItem = catalogOption;

        vm.AddSwitchRowCommand.Execute(null);
        vm.SwitchRows[0].SelectedSwitch = SwitchName;

        await vm.EvaluateCommand.ExecuteAsync(null);
    }

    // Debounced readiness passes run on Task.Delay continuations, which the shared thread pool
    // can starve for hundreds of ms when the xUnit suite runs test classes in parallel. A 2s
    // budget was too tight under that load and produced intermittent CI failures, so the wait
    // gets generous headroom - a passing condition still returns immediately, only a genuinely
    // failing wait pays the full timeout.
    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(15);
        }
    }

    /// <summary>
    /// Waits until the readiness pipeline is fully idle: no evaluation is in flight and the debounced
    /// preflight call count has held steady across a quiet window, meaning every scheduled pass has drained.
    /// </summary>
    /// <remarks>
    /// Follow-up mutations (AddVm/RemoveVm) should start from a settled state so the baseline evaluation
    /// count captured before the mutation is stable. In the real UI the AddVm/Remove commands are gated by
    /// <c>!IsEvaluatingReadiness</c>, so a user never triggers them mid-pass; tests invoke the commands
    /// directly (bypassing CanExecute), so this drain reproduces that settled starting point. A fixed delay
    /// could not close the window reliably because the debounce continuation is starved for a variable time
    /// under the parallel suite's thread-pool pressure.
    /// </remarks>
    private static async Task WaitForReadinessIdleAsync(Harness harness, int quietMs = 150, int timeoutMs = 5000)
    {
        var overall = Stopwatch.StartNew();
        var quiet = Stopwatch.StartNew();
        int lastCount = -1;

        while (overall.ElapsedMilliseconds < timeoutMs)
        {
            int count = harness.Preflight.CallCount;
            if (harness.Vm.IsEvaluatingReadiness || count != lastCount)
            {
                // Still churning (evaluating or a pass just landed); restart the quiet window.
                lastCount = count;
                quiet.Restart();
            }
            else if (quiet.ElapsedMilliseconds >= quietMs)
            {
                return;
            }

            await Task.Delay(15);
        }
    }

    [Fact]
    public void ApplyShellState_Activate_SeedsSingleDefaultEntry()
    {
        var harness = CreateHarness();

        harness.Vm.ApplyShellState(isActive: true);

        Assert.Single(harness.Vm.VmEntries);
        Assert.Single(harness.Vm.VmEntryRows);
        Assert.Equal(1, harness.Vm.DraftCount);
        Assert.NotNull(harness.Vm.SelectedVmEntry);
    }

    [Fact]
    public void AddVm_AppendsEntryAndRaisesSharedUiState()
    {
        var harness = CreateHarness();
        var sharedUiRaised = 0;
        harness.Vm.SharedUiStateChanged += (_, _) => sharedUiRaised++;

        harness.Vm.AddVmCommand.Execute(null);
        harness.Vm.AddVmCommand.Execute(null);

        Assert.Equal(2, harness.Vm.VmEntries.Count);
        Assert.Equal(2, harness.Vm.VmEntryRows.Count);
        Assert.Equal(2, harness.Vm.DraftCount);
        Assert.Equal(2, sharedUiRaised);
    }

    [Fact]
    public async Task RemoveVmRow_WhenConfirmed_RemovesEntry()
    {
        var harness = CreateHarness();
        harness.Vm.AddVmCommand.Execute(null);
        harness.Vm.AddVmCommand.Execute(null);
        var rowToRemove = harness.Vm.VmEntryRows[1];

        await harness.Vm.RemoveVmRowCommand.ExecuteAsync(rowToRemove);

        Assert.Single(harness.Vm.VmEntries);
        Assert.DoesNotContain(rowToRemove.VmEntry, harness.Vm.VmEntries);
        Assert.Single(harness.RemovePrompts);
    }

    [Fact]
    public async Task RemoveVmRow_WhenDeclined_KeepsEntry()
    {
        var harness = CreateHarness();
        harness.ConfirmRemoveResult = false;
        harness.Vm.AddVmCommand.Execute(null);
        harness.Vm.AddVmCommand.Execute(null);

        await harness.Vm.RemoveVmRowCommand.ExecuteAsync(harness.Vm.VmEntryRows[1]);

        Assert.Equal(2, harness.Vm.VmEntries.Count);
        Assert.Single(harness.RemovePrompts);
    }

    [Fact]
    public async Task ApplyVmChanges_ValidDraft_UpdatesSelectedEntry()
    {
        var harness = CreateHarness();
        harness.Vm.AddVmCommand.Execute(null);

        harness.Vm.EditorVmNameDraft = "Renamed-VM";
        harness.Vm.EditorVmMemoryDraft = "4096";
        harness.Vm.EditorVmCpuDraft = "3";
        await harness.Vm.ApplyVmChangesCommand.ExecuteAsync(null);

        var entry = harness.Vm.SelectedVmEntry;
        Assert.NotNull(entry);
        Assert.Equal("Renamed-VM", entry!.Name);
        Assert.Equal(4096, entry.MemoryMb);
        Assert.Equal(3, entry.CpuCount);
    }

    [Fact]
    public async Task ApplyVmChanges_InvalidMemory_IsGatedWithStatusMessage()
    {
        var harness = CreateHarness();
        harness.Vm.AddVmCommand.Execute(null);
        harness.Vm.EditorVmMemoryDraft = "4096";
        await harness.Vm.ApplyVmChangesCommand.ExecuteAsync(null);
        Assert.Equal(4096, harness.Vm.SelectedVmEntry!.MemoryMb);

        harness.Vm.EditorVmMemoryDraft = "not-a-number";
        await harness.Vm.ApplyVmChangesCommand.ExecuteAsync(null);

        Assert.Equal("Memory must be a positive integer.", harness.Vm.StatusText);
        Assert.Equal(4096, harness.Vm.SelectedVmEntry!.MemoryMb);
    }

    [Fact]
    public async Task RowsRebuild_WhenSelectionChangesDuringCollectionChanged_DoesNotThrow()
    {
        // F41 regression: a rows-rebuild that removes the selected row lets the ListView push a new
        // SelectedVmEntryRow synchronously, mid-CollectionChanged. Before the fix that re-entered
        // RefreshVmEntryRows and mutated VmEntryRows during the dispatch, throwing
        // "Cannot change ObservableCollection during a CollectionChanged event". Here we emulate that
        // framework selection push and additionally grow the entry set during the same event so a nested
        // rebuild would need to Insert a row, which is exactly the throwing case.
        var harness = CreateHarness();
        var vm = harness.Vm;
        vm.ApplyShellState(isActive: true);

        vm.AddVmCommand.Execute(null);
        vm.AddSwitchRowCommand.Execute(null);
        vm.SwitchRows[0].SelectedSwitch = SwitchName;

        var rowToRemove = vm.VmEntryRows[1];
        vm.SelectedVmEntryRow = rowToRemove;

        var spareEntry = new VmTemplate { Name = "Spare", MemoryMb = 2048, CpuCount = 2 };
        var handledOnce = false;

        void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (handledOnce || e.Action != NotifyCollectionChangedAction.Remove || e.OldItems is null ||
                !e.OldItems.Contains(rowToRemove))
            {
                return;
            }

            handledOnce = true;

            // Emulate the ListView reconciling SelectedItem while the removed row is still being
            // dispatched, and a concurrent entry-set growth so a nested rebuild would Insert mid-event.
            vm.VmEntries.Add(spareEntry);
            vm.SelectedVmEntryRow = vm.VmEntryRows.FirstOrDefault();
        }

        vm.VmEntryRows.CollectionChanged += OnRowsChanged;
        Exception? exception;
        try
        {
            exception = await Record.ExceptionAsync(() => vm.RemoveVmRowCommand.ExecuteAsync(rowToRemove));
        }
        finally
        {
            vm.VmEntryRows.CollectionChanged -= OnRowsChanged;
        }

        Assert.Null(exception);
        Assert.Equal(vm.VmEntries.Count, vm.VmEntryRows.Count);
        Assert.True(vm.VmEntryRows.Select(row => row.VmEntry).SequenceEqual(vm.VmEntries));
    }

    [Fact]
    public async Task Evaluate_BlankSwitchRow_DoesNotBlockDeploy()
    {
        var harness = CreateHarness();
        var vm = harness.Vm;
        vm.ApplyShellState(isActive: true);

        var catalogOption = vm.VhdxCatalogItems.OfType<TemplateVhdxCatalogOption>().First();
        vm.SelectedVhdxCatalogItem = catalogOption;

        // Add a switch row but leave it unselected. Switches are optional, so a blank row is treated as
        // "no switch" - a soft warning at most, never a blocker that gates deploy.
        vm.AddSwitchRowCommand.Execute(null);

        await vm.EvaluateCommand.ExecuteAsync(null);

        Assert.False(vm.HasBlockingFailures);
        Assert.True(vm.CanStartDeploy);
        Assert.DoesNotContain(vm.IssueRows, row => row.Message.Contains("switch row"));
        Assert.DoesNotContain(vm.IssueRows, row => row.Severity == "Block");
    }

    [Fact]
    public async Task Evaluate_BlockingFailure_PopulatesReadinessBadge()
    {
        var harness = CreateHarness();
        harness.Preflight.Results.Add(new DeploymentReadinessCheckResult
        {
            Status = DeploymentReadinessStatus.Fail,
            Category = DeploymentReadinessCategory.Environment,
            Message = "Hyper-V is not available.",
            ActionableGuidance = "Enable the Hyper-V role."
        });

        await ActivateReadyVmAsync(harness);

        Assert.True(harness.Vm.HasReadinessIssues);
        Assert.True(harness.Vm.HasBlockingIssues);
        Assert.True(harness.Vm.BlockingIssueCount >= 1);
        Assert.Equal(harness.Vm.WarningIssueCount > 0, harness.Vm.HasWarningIssues);
        Assert.Contains("Hyper-V is not available.", harness.Vm.BlockingBadgeTooltip);
    }

    [Fact]
    public async Task Evaluate_ReadyVm_HidesReadinessBadge()
    {
        var harness = CreateHarness();

        await ActivateReadyVmAsync(harness);

        Assert.False(harness.Vm.HasReadinessIssues);
        Assert.False(harness.Vm.HasBlockingIssues);
        Assert.False(harness.Vm.HasWarningIssues);
        Assert.Equal(0, harness.Vm.BlockingIssueCount);
        Assert.Equal(0, harness.Vm.WarningIssueCount);
        Assert.Equal(string.Empty, harness.Vm.BlockingBadgeTooltip);
        Assert.Equal(string.Empty, harness.Vm.WarningBadgeTooltip);
    }

    [Fact]
    public async Task Evaluate_WarningOnly_CountsWarningInReadinessBadge()
    {
        var harness = CreateHarness();
        harness.Preflight.Results.Add(new DeploymentReadinessCheckResult
        {
            Status = DeploymentReadinessStatus.Warn,
            Category = DeploymentReadinessCategory.DestinationPathStorage,
            Message = "Destination volume is low on space.",
            ActionableGuidance = "Free up disk space."
        });

        await ActivateReadyVmAsync(harness);

        Assert.True(harness.Vm.HasReadinessIssues);
        Assert.False(harness.Vm.HasBlockingIssues);
        Assert.Equal(0, harness.Vm.BlockingIssueCount);
        Assert.True(harness.Vm.HasWarningIssues);
        Assert.True(harness.Vm.WarningIssueCount >= 1);
        Assert.Equal(string.Empty, harness.Vm.BlockingBadgeTooltip);
        Assert.Contains("Destination volume is low on space.", harness.Vm.WarningBadgeTooltip);
    }

    [Fact]
    public void CommandCanExecute_ReflectsEntryPresence()
    {
        var harness = CreateHarness();

        Assert.True(harness.Vm.CanAddVm);
        Assert.False(harness.Vm.CanRemoveVm);
        Assert.False(harness.Vm.CanOpenTemplateEditor);
        Assert.False(harness.Vm.CanStartDeploy);

        harness.Vm.AddVmCommand.Execute(null);

        Assert.True(harness.Vm.CanRemoveVm);
        Assert.True(harness.Vm.CanOpenTemplateEditor);
        Assert.True(harness.Vm.CanStartDeploy);
    }

    [Fact]
    public async Task Evaluate_ValidVm_ReachesReadyState()
    {
        var harness = CreateHarness();

        await ActivateReadyVmAsync(harness);

        Assert.Equal("Ready", harness.Vm.LifecycleState);
        Assert.False(harness.Vm.HasBlockingFailures);
        Assert.True(harness.Vm.CanStartDeploy);
        Assert.Equal(100, harness.Vm.ProgressPercent);
    }

    [Fact]
    public async Task Evaluate_PreflightFailure_ReachesBlockedState()
    {
        var harness = CreateHarness();
        harness.Preflight.Results.Add(new DeploymentReadinessCheckResult
        {
            Status = DeploymentReadinessStatus.Fail,
            Category = DeploymentReadinessCategory.Environment,
            Message = "Hyper-V is not available.",
            ActionableGuidance = "Enable the Hyper-V role."
        });

        await ActivateReadyVmAsync(harness);

        Assert.Equal("Blocked", harness.Vm.LifecycleState);
        Assert.True(harness.Vm.HasBlockingFailures);
        Assert.False(harness.Vm.CanStartDeploy);
        Assert.Contains(harness.Vm.IssueRows, row => row.Severity == "Block");
    }

    [Fact]
    public async Task Evaluate_PreflightWarning_ReachesWarningStateButStaysDeployable()
    {
        var harness = CreateHarness();
        harness.Preflight.Results.Add(new DeploymentReadinessCheckResult
        {
            Status = DeploymentReadinessStatus.Warn,
            Category = DeploymentReadinessCategory.DestinationPathStorage,
            Message = "Destination volume is low on space.",
            ActionableGuidance = "Free up disk space."
        });

        await ActivateReadyVmAsync(harness);

        Assert.Equal("Warning", harness.Vm.LifecycleState);
        Assert.False(harness.Vm.HasBlockingFailures);
        Assert.True(harness.Vm.CanStartDeploy);
        Assert.Equal(100, harness.Vm.ProgressPercent);
    }

    [Fact]
    public async Task DraftChange_SchedulesDebouncedAutoEvaluate_RunsOnceForRapidEdits()
    {
        var harness = CreateHarness(autoEvaluateDelayMs: 30);
        harness.Vm.ApplyShellState(isActive: true);

        // Promote to a ready draft with several rapid synchronous edits. The nonce debounce should
        // collapse them (plus the activation schedule) into a single readiness pass.
        var catalogOption = harness.Vm.VhdxCatalogItems.OfType<TemplateVhdxCatalogOption>().First();
        harness.Vm.SelectedVhdxCatalogItem = catalogOption;
        harness.Vm.AddSwitchRowCommand.Execute(null);
        harness.Vm.SwitchRows[0].SelectedSwitch = SwitchName;
        harness.Vm.EditorVmCpuDraft = "4";

        await WaitUntilAsync(() => harness.Vm.ReadinessReport is not null);

        Assert.NotNull(harness.Vm.ReadinessReport);
        Assert.Equal(1, harness.Preflight.CallCount);
        Assert.Equal("Ready", harness.Vm.LifecycleState);
    }

    [Fact]
    public async Task AddVm_AfterEvaluation_ReschedulesReadinessForNewVmSet()
    {
        // F49 regression: adding a VM used to clear the readiness report without re-scheduling an
        // evaluation, so the badges and right panel stayed stuck on the idle state permanently. Adding a
        // bare VM must now re-evaluate. A two-disk catalog is used so the added entry is genuinely
        // diskless (F26 auto-select does not fire), which correctly blocks readiness.
        var harness = CreateHarness(autoEvaluateDelayMs: 30, catalog: TwoDiskCatalog());
        await ActivateReadyVmAsync(harness);
        await WaitUntilAsync(() => harness.Vm.LifecycleState == "Ready");

        // Drain every setup-scheduled pass so the add starts from a fully idle pipeline, mirroring the
        // real UI's !IsEvaluatingReadiness gate before capturing the baseline evaluation count.
        await WaitForReadinessIdleAsync(harness);
        var baselineCallCount = harness.Preflight.CallCount;

        harness.Vm.AddVmCommand.Execute(null);

        // Wait for the re-evaluation to fully settle: the new bare entry has no base disk, so the set
        // must resolve to Blocked with a Block-severity issue row.
        await WaitUntilAsync(() =>
            harness.Preflight.CallCount > baselineCallCount
            && harness.Vm.LifecycleState == "Blocked"
            && harness.Vm.IssueRows.Any(row => row.Severity == "Block"));

        Assert.True(harness.Preflight.CallCount > baselineCallCount);
        Assert.NotNull(harness.Vm.ReadinessReport);
        Assert.Equal("Blocked", harness.Vm.LifecycleState);
        Assert.Contains(harness.Vm.IssueRows, row => row.Severity == "Block");
    }

    [Fact]
    public async Task RemoveVm_LeavingEntries_ReschedulesReadinessForRemainingSet()
    {
        // F49 regression: removing a VM must re-evaluate the remaining set. Start blocked (two VMs, one
        // bare) then remove the bare VM so readiness recovers to Ready for the lone configured VM. The
        // two-disk catalog keeps the added VM genuinely diskless so it blocks deterministically.
        var harness = CreateHarness(autoEvaluateDelayMs: 30, catalog: TwoDiskCatalog());
        await ActivateReadyVmAsync(harness);

        // Drain the activation pass before adding, then wait for the two-VM set to settle on Blocked
        // (the added bare entry has no base disk).
        await WaitForReadinessIdleAsync(harness);
        harness.Vm.AddVmCommand.Execute(null);
        await WaitUntilAsync(() =>
            harness.Vm.LifecycleState == "Blocked"
            && harness.Vm.IssueRows.Any(row => row.Severity == "Block"));

        // Settle the add's pass before removing so the baseline count is stable.
        await WaitForReadinessIdleAsync(harness);
        var baselineCallCount = harness.Preflight.CallCount;
        var bareRow = harness.Vm.VmEntryRows[1];

        await harness.Vm.RemoveVmRowCommand.ExecuteAsync(bareRow);

        await WaitUntilAsync(() =>
            harness.Preflight.CallCount > baselineCallCount && harness.Vm.LifecycleState == "Ready");

        Assert.True(harness.Preflight.CallCount > baselineCallCount);
        Assert.Single(harness.Vm.VmEntries);
        Assert.NotNull(harness.Vm.ReadinessReport);
        Assert.Equal("Ready", harness.Vm.LifecycleState);
    }

    [Fact]
    public async Task RemoveVm_RemovingLastEntry_DoesNotRescheduleAndStaysIdle()
    {
        // F49 boundary: removing the final VM leaves nothing to evaluate, so no readiness pass is scheduled
        // and the lane rests on the "add at least one VM" idle prompt.
        var harness = CreateHarness(autoEvaluateDelayMs: 30);
        harness.Vm.ApplyShellState(isActive: true);

        // The activation schedule evaluates the seeded default entry once; drain it before the baseline.
        await WaitUntilAsync(() => harness.Vm.ReadinessReport is not null);
        await WaitForReadinessIdleAsync(harness);
        var baselineCallCount = harness.Preflight.CallCount;

        await harness.Vm.RemoveVmRowCommand.ExecuteAsync(harness.Vm.VmEntryRows[0]);

        // Wait past the debounce window to confirm no evaluation is scheduled for the now-empty set.
        await Task.Delay(120);

        Assert.Equal(baselineCallCount, harness.Preflight.CallCount);
        Assert.Empty(harness.Vm.VmEntries);
        Assert.Null(harness.Vm.ReadinessReport);
        Assert.Equal("Add at least one VM entry to evaluate readiness.", harness.Vm.ReadinessSummaryText);
    }

    [Fact]
    public async Task StartDeploy_ThenNavigateAwayCleanup_CancelsInFlightRun()
    {
        var harness = CreateHarness();
        await ActivateReadyVmAsync(harness);

        var release = new TaskCompletionSource();
        harness.Runtime.OnExecute = _ => release.Task;

        var deployTask = harness.Vm.StartDeployCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => harness.Runtime.LastContext is not null);
        Assert.NotNull(harness.Runtime.LastContext);
        Assert.False(harness.Runtime.LastContext!.IsCancellationRequested);

        await harness.Vm.CleanupAsync();

        Assert.True(harness.Runtime.LastContext!.UserCancellationRequested);
        Assert.True(harness.Runtime.LastContext!.IsCancellationRequested);

        release.SetResult();
        await deployTask;
    }

    [Fact]
    public async Task StartDeploy_BlockedReadiness_DoesNotInvokeRuntime()
    {
        var harness = CreateHarness();
        harness.Preflight.Results.Add(new DeploymentReadinessCheckResult
        {
            Status = DeploymentReadinessStatus.Fail,
            Category = DeploymentReadinessCategory.Environment,
            Message = "Hyper-V is not available.",
            ActionableGuidance = "Enable the Hyper-V role."
        });
        await ActivateReadyVmAsync(harness);

        await harness.Vm.StartDeployCommand.ExecuteAsync(null);

        Assert.Equal(0, harness.Planning.CallCount);
        Assert.Equal(0, harness.Runtime.CallCount);
        Assert.Equal("Blocked", harness.Vm.LifecycleState);
    }

    [Fact]
    public async Task StartDeploy_BuildsStandaloneV2Plan()
    {
        var harness = CreateHarness();
        harness.Planning.Result = FakeV2PlanningCapabilityService.StandalonePlan("Quick VM 1");
        await ActivateReadyVmAsync(harness);

        await harness.Vm.StartDeployCommand.ExecuteAsync(null);

        var request = Assert.IsType<V2PlanBuildRequest>(harness.Planning.LastRequest);
        Assert.Equal(TemplateExecutionEngine.V2UnifiedPlanning, request.Template.ExecutionEngine);
        Assert.Equal("2.0.0", request.Template.SchemaVersion);
        Assert.All(
            request.Template.VmTemplates,
            vm => Assert.Equal(V2MembershipModeCatalog.Standalone, vm.MembershipMode));
    }

    [Fact]
    public async Task StartDeploy_PlanBlocked_SurfacesBlockersAndDoesNotExecute()
    {
        var harness = CreateHarness();
        harness.Planning.Result = new V2PlanBuildResult
        {
            Success = false,
            Issues =
            [
                new V2PlanIssue
                {
                    Severity = V2PlanIssueSeverity.Blocking,
                    VmName = "Quick VM 1",
                    Message = "Base disk is missing a bootstrap profile.",
                    SuggestedAction = "Register a bootstrap profile."
                }
            ]
        };
        await ActivateReadyVmAsync(harness);

        await harness.Vm.StartDeployCommand.ExecuteAsync(null);

        Assert.Equal(1, harness.Planning.CallCount);
        Assert.Equal(0, harness.Runtime.CallCount);
        Assert.Equal("Blocked", harness.Vm.LifecycleState);
        var issue = Assert.Single(harness.Vm.IssueRows);
        Assert.Equal("Block", issue.Severity);
        Assert.Contains("bootstrap profile", issue.Message);
    }

    [Fact]
    public async Task StartDeploy_Succeeds_ProjectsLiveRows()
    {
        var harness = CreateHarness();
        harness.Planning.Result = FakeV2PlanningCapabilityService.StandalonePlan("Quick VM 1");
        harness.Runtime.Result = new V2RuntimeExecutionResult { Success = true };
        await ActivateReadyVmAsync(harness);

        await harness.Vm.StartDeployCommand.ExecuteAsync(null);

        Assert.Equal(1, harness.Runtime.CallCount);
        Assert.Equal("Completed", harness.Vm.LifecycleState);
        Assert.Equal(100, harness.Vm.ProgressPercent);
        var row = Assert.Single(harness.Vm.ResultRows);
        Assert.Equal("Quick VM 1", row.VmName);
    }

    /// <summary>
    /// F32 regression: the overall/aggregate deployment progress must reflect real per-VM state - 0 when
    /// nothing has run, moving upward as VMs complete, and pinned to 100 on terminal completion - instead
    /// of resting on a fixed stage constant while the run is in flight.
    /// </summary>
    [Fact]
    public void OverallProgress_AggregatesPerVmProgress_AndCompletes()
    {
        var harness = CreateHarness();
        var vm = harness.Vm;
        vm.ApplyShellState(isActive: true);

        vm.InitializeProgressRows(FakeV2PlanningCapabilityService.StandalonePlan("vm-a", "vm-b"));
        vm.SetWorkflowState("Running", 0, "Deploying...");
        Assert.Equal(0, vm.ProgressPercent);

        CompleteVmSteps(vm, "vm-a");
        var afterFirst = vm.ProgressPercent;
        Assert.InRange(afterFirst, 1, 99);

        CompleteVmSteps(vm, "vm-b");
        Assert.True(vm.ProgressPercent > afterFirst, "Aggregate progress must advance as more VMs complete.");

        vm.SetWorkflowState("Completed", 100, "Done.");
        Assert.Equal(100, vm.ProgressPercent);
    }

    /// <summary>
    /// F33 regression: a single underlying problem (a missing base disk) is reported by both the
    /// compatibility list and the readiness report. The merged projection must count it once and surface
    /// the actual reason text rather than a bare, doubled "Blocking: 2" number.
    /// </summary>
    [Fact]
    public async Task Readiness_DuplicateBaseDiskProblem_CountsOnceAndSurfacesReason()
    {
        // Two catalog disks so the F26 single-disk auto-select does not fire: the VM stays disk-less,
        // so the compatibility builder raises a blocking base-disk issue that overlaps the readiness one.
        var harness = CreateHarness(catalog:
        [
            new VhdxCatalogItem { Id = "base-1", Path = @"C:\Base\base-1.vhdx", OsName = "Windows Server", OsVersion = "2022", Generation = 2 },
            new VhdxCatalogItem { Id = "base-2", Path = @"C:\Base\base-2.vhdx", OsName = "Windows Server", OsVersion = "2025", Generation = 2 }
        ]);
        harness.Preflight.Results.Add(new DeploymentReadinessCheckResult
        {
            Status = DeploymentReadinessStatus.Fail,
            Category = DeploymentReadinessCategory.VhdxBaseDisk,
            Code = "VHDX.MISSING_REFERENCE",
            Message = "Base VHDX reference is missing for VM 'Quick VM 1'.",
            ActionableGuidance = "Select a valid base VHDX for this VM before deploying.",
            AffectedVmNames = ["Quick VM 1"]
        });

        // Activate without selecting a base disk so the compatibility builder also raises a blocking
        // base-disk issue for the same VM - the exact overlap that used to be double-counted.
        harness.Vm.ApplyShellState(isActive: true);
        await harness.Vm.EvaluateCommand.ExecuteAsync(null);

        var blockingRows = harness.Vm.IssueRows.Where(row => row.Severity == "Block").ToList();
        Assert.Single(blockingRows);

        var vmRow = harness.Vm.ResultRows.Single(row => row.VmName == "Quick VM 1");
        Assert.Equal("Blocked", vmRow.Status);
        Assert.StartsWith("Blocked:", vmRow.Summary);
        Assert.Contains("Base VHDX reference is missing", vmRow.Summary);
        Assert.DoesNotContain("Blocking: 2", vmRow.Summary);
    }

    /// <summary>
    /// F33 guard: the cross-source de-duplication must not collapse two genuinely distinct readiness
    /// failures that happen to share a (scope, category, severity) tuple. A single VM legitimately raises
    /// several guest-configuration gaps in the same category, and each carries its own reason the user
    /// must see, so both rows must survive and both must be counted.
    /// </summary>
    [Fact]
    public async Task Readiness_DistinctSameCategoryFailures_AreAllPreserved()
    {
        var harness = CreateHarness();
        harness.Preflight.Results.Add(new DeploymentReadinessCheckResult
        {
            Status = DeploymentReadinessStatus.Fail,
            Category = DeploymentReadinessCategory.TemplateConfig,
            Code = "GST.TIMEZONE.MISSING_CONFIG",
            Message = "Time zone is not configured for VM 'Quick VM 1'.",
            ActionableGuidance = "Pick a time zone for this VM before deploying.",
            AffectedVmNames = ["Quick VM 1"]
        });
        harness.Preflight.Results.Add(new DeploymentReadinessCheckResult
        {
            Status = DeploymentReadinessStatus.Fail,
            Category = DeploymentReadinessCategory.TemplateConfig,
            Code = "GST.ROLE.MISSING_SELECTION",
            Message = "No role is selected for VM 'Quick VM 1'.",
            ActionableGuidance = "Choose at least one role for this VM before deploying.",
            AffectedVmNames = ["Quick VM 1"]
        });

        harness.Vm.ApplyShellState(isActive: true);
        await harness.Vm.EvaluateCommand.ExecuteAsync(null);

        var blockingMessages = harness.Vm.IssueRows
            .Where(row => row.Severity == "Block")
            .Select(row => row.Message)
            .ToList();

        Assert.Contains(blockingMessages, message => message.Contains("Time zone is not configured"));
        Assert.Contains(blockingMessages, message => message.Contains("No role is selected"));
    }

    /// <summary>
    /// F26: when the catalog holds exactly one base disk, the default disk-less VM auto-selects it so the
    /// user is not blocked by a missing base disk on first run, and the base-disk hint stays hidden.
    /// </summary>
    [Fact]
    public void Activate_SingleCatalogDisk_AutoSelectsItAndHidesHint()
    {
        var harness = CreateHarness();

        harness.Vm.ApplyShellState(isActive: true);

        Assert.IsType<TemplateVhdxCatalogOption>(harness.Vm.SelectedVhdxCatalogItem);
        Assert.False(harness.Vm.ShowBaseDiskHint);
    }

    /// <summary>
    /// F26: when the catalog holds more than one base disk there is no safe auto-selection, so the VM stays
    /// disk-less and the hint points the user at the selector.
    /// </summary>
    [Fact]
    public void Activate_MultipleCatalogDisks_ShowsBaseDiskHint()
    {
        var harness = CreateHarness(catalog:
        [
            new VhdxCatalogItem { Id = "base-1", Path = @"C:\Base\base-1.vhdx", OsName = "Windows Server", OsVersion = "2022", Generation = 2 },
            new VhdxCatalogItem { Id = "base-2", Path = @"C:\Base\base-2.vhdx", OsName = "Windows Server", OsVersion = "2025", Generation = 2 }
        ]);

        harness.Vm.ApplyShellState(isActive: true);

        Assert.IsNotType<TemplateVhdxCatalogOption>(harness.Vm.SelectedVhdxCatalogItem);
        Assert.True(harness.Vm.ShowBaseDiskHint);
        Assert.Equal("Select a base disk to continue.", harness.Vm.BaseDiskHintText);
    }

    /// <summary>
    /// F27: a destination-folder collision reported by readiness for the selected VM surfaces as inline
    /// validation on the Name field instead of only appearing in the readiness list.
    /// </summary>
    [Fact]
    public async Task NameValidation_FolderCollision_SurfacesInlineOnNameField()
    {
        var harness = CreateHarness();
        harness.Preflight.Results.Add(new DeploymentReadinessCheckResult
        {
            Status = DeploymentReadinessStatus.Fail,
            Category = DeploymentReadinessCategory.DestinationPathStorage,
            Code = "DST.VM_PATH.CONFLICT_DIR_EXISTS",
            Message = "VM destination folder 'C:\\Labs\\Quick VM 1' already exists.",
            ActionableGuidance = "Use a different VM destination folder or remove the existing folder if it is safe to do so.",
            AffectedVmNames = ["Quick VM 1"]
        });

        harness.Vm.ApplyShellState(isActive: true);
        await harness.Vm.EvaluateCommand.ExecuteAsync(null);

        Assert.True(harness.Vm.HasEditorNameError);
        Assert.Contains("already exists", harness.Vm.EditorNameValidationText);
    }

    /// <summary>
    /// F27: a VM name that collides with another entry is flagged inline on the Name field.
    /// </summary>
    [Fact]
    public void NameValidation_DuplicateName_FlagsNameField()
    {
        var harness = CreateHarness();
        harness.Vm.ApplyShellState(isActive: true);

        harness.Vm.AddVmCommand.Execute(null);
        harness.Vm.EditorVmNameDraft = "Quick VM 1";

        Assert.True(harness.Vm.HasEditorNameError);
        Assert.Contains("already uses this name", harness.Vm.EditorNameValidationText);
    }

    /// <summary>
    /// F34: a fully ready draft yields human-friendly readiness copy rather than the opaque "Readiness
    /// passed." label.
    /// </summary>
    [Fact]
    public async Task Readiness_Ready_UsesHumanFriendlyCopy()
    {
        var harness = CreateHarness();

        await ActivateReadyVmAsync(harness);

        Assert.Equal("Machines are ready to deploy.", harness.Vm.ReadinessSummaryText);
    }

    /// <summary>
    /// F34: when readiness is blocked the summary names the first blocking reason instead of a bare
    /// "Blocked" label.
    /// </summary>
    [Fact]
    public async Task Readiness_Blocked_NamesFirstReason()
    {
        var harness = CreateHarness();
        harness.Preflight.Results.Add(new DeploymentReadinessCheckResult
        {
            Status = DeploymentReadinessStatus.Fail,
            Category = DeploymentReadinessCategory.Environment,
            Message = "Hyper-V is not available.",
            ActionableGuidance = "Enable the Hyper-V role."
        });

        await ActivateReadyVmAsync(harness);

        Assert.StartsWith("Not ready to deploy:", harness.Vm.ReadinessSummaryText);
        Assert.Contains("Hyper-V is not available.", harness.Vm.ReadinessSummaryText);
    }

    private static void CompleteVmSteps(DeployQuickDeployViewModel vm, string vmName)
    {
        foreach (var stepKey in new[] { DeploymentStepKeys.V2ProvisionVm, DeploymentStepKeys.V2StartVm })
        {
            vm.ApplyProgressUpdate(vmName, new DeployStepStateUpdate(
                OperationId: "op",
                VmId: Guid.NewGuid(),
                VmName: vmName,
                StepKey: stepKey,
                StepLabel: stepKey,
                State: DeployStepState.Succeeded,
                Message: null,
                TimestampUtc: DateTimeOffset.UtcNow,
                Sequence: 0));
        }
    }

    [Fact]
    public void ToggleResultsPanel_InvokesInjectedToggleSeam()
    {
        var harness = CreateHarness();

        harness.Vm.ToggleResultsPanelCommand.Execute(null);

        Assert.Equal(1, harness.ToggleCount);
    }

    [Fact]
    public void ApplyResultsPanelState_PanelUnavailable_DisablesToggleAndExplains()
    {
        var harness = CreateHarness();

        harness.Vm.ApplyResultsPanelState(isActive: true, showPanel: false, panelUnavailable: true);

        Assert.False(harness.Vm.CanToggleResultsPanel);
        Assert.Equal("Expand the window to review the progress and results panel.", harness.Vm.ResultsPanelSummaryText);
    }

    /// <summary>
    /// Regression guard for the combined-smoke-test blocker: Quick Deploy spun in a constant
    /// re-evaluate loop. The base-disk ComboBox is bound TwoWay to
    /// <see cref="DeployQuickDeployViewModel.SelectedVhdxCatalogItem"/> with its ItemsSource bound to
    /// <see cref="DeployQuickDeployViewModel.VhdxCatalogItems"/>. Every readiness pass reloaded
    /// reference data and rebuilt that collection, which reset the control's selection to null and
    /// wrote it back through the binding, re-arming auto-evaluate and closing any open dropdown. This
    /// test models that control feedback (null the selection whenever the collection is reset) and
    /// asserts the workflow does NOT spin: readiness runs a bounded number of times.
    /// </summary>
    [Fact]
    public async Task CatalogRebuildFeedback_DoesNotSpinAutoEvaluate()
    {
        var harness = CreateHarness(autoEvaluateDelayMs: 25);
        var vm = harness.Vm;

        // Mimic the WinUI ComboBox: when its ItemsSource is cleared, it drops its selection and
        // writes null back through the TwoWay binding.
        vm.VhdxCatalogItems.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                vm.SelectedVhdxCatalogItem = null;
            }
        };

        vm.ApplyShellState(isActive: true);

        await WaitUntilAsync(() => harness.Preflight.CallCount >= 1);
        var runsAfterFirstPass = harness.Preflight.CallCount;

        // Let several more debounce windows elapse; a spinning loop would keep incrementing.
        await Task.Delay(200);

        Assert.True(
            harness.Preflight.CallCount <= runsAfterFirstPass,
            $"Auto-evaluate spun: {harness.Preflight.CallCount} readiness runs (was {runsAfterFirstPass} after the first pass).");
        Assert.True(harness.Preflight.CallCount <= 2, $"Unexpected readiness run count: {harness.Preflight.CallCount}.");
    }

    /// <summary>
    /// Directly asserts the structural fix behind <see cref="CatalogRebuildFeedback_DoesNotSpinAutoEvaluate"/>:
    /// a repeat reference-data ensure whose option set is unchanged must not rebuild the bound catalog
    /// collection (no Reset raised) and must preserve the current selection, so the base-disk dropdown
    /// stays open and keeps its value across every readiness pass.
    /// </summary>
    [Fact]
    public async Task EnsureReferenceData_WhenOptionsUnchanged_DoesNotRebuildCatalogOrLoseSelection()
    {
        var harness = CreateHarness();
        var vm = harness.Vm;
        await ActivateReadyVmAsync(harness);

        var selectedOption = vm.SelectedVhdxCatalogItem as TemplateVhdxCatalogOption;
        Assert.NotNull(selectedOption);
        var itemsSnapshot = vm.VhdxCatalogItems.ToList();

        var resetRaised = 0;
        vm.VhdxCatalogItems.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                resetRaised++;
            }
        };

        await ((IDeployQuickDeployWorkspaceControllerHost)vm).EnsureReferenceDataAsync(forceRefresh: false);

        Assert.Equal(0, resetRaised);
        Assert.Equal(itemsSnapshot, vm.VhdxCatalogItems);
        Assert.Same(selectedOption, vm.SelectedVhdxCatalogItem);
    }

    /// <summary>
    /// Companion guard to the catalog stabilization: the switch-row collection backs the per-row
    /// switch ComboBoxes, so it must survive a readiness pass untouched. Rebuilding it would tear down
    /// the bound ItemsControl and close an open switch dropdown mid-selection. This asserts the row
    /// instances are preserved across a repeat reference-data ensure when the selection is unchanged.
    /// </summary>
    [Fact]
    public async Task EnsureReferenceData_WhenSwitchSelectionUnchanged_KeepsSwitchRowInstances()
    {
        var harness = CreateHarness();
        var vm = harness.Vm;
        await ActivateReadyVmAsync(harness);

        var rowsSnapshot = vm.SwitchRows.ToList();
        Assert.NotEmpty(rowsSnapshot);

        await ((IDeployQuickDeployWorkspaceControllerHost)vm).EnsureReferenceDataAsync(forceRefresh: false);

        Assert.Equal(rowsSnapshot.Count, vm.SwitchRows.Count);
        for (var index = 0; index < rowsSnapshot.Count; index++)
        {
            Assert.Same(rowsSnapshot[index], vm.SwitchRows[index]);
        }
        Assert.Equal(SwitchName, vm.SwitchRows[0].SelectedSwitch);
    }
}
