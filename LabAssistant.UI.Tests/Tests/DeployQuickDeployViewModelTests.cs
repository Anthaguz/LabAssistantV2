using System.Diagnostics;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
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

    /// <summary>
    /// Holds the constructed view model together with the fakes and interaction counters a test needs
    /// to assert against.
    /// </summary>
    private sealed class Harness
    {
        public required DeployQuickDeployViewModel Vm { get; init; }

        public required FakePreflightService Preflight { get; init; }

        public required FakeDeploymentCoordinator Coordinator { get; init; }

        public required FakeOutcomeSummaryBuilder Outcome { get; init; }

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
        var coordinator = new FakeDeploymentCoordinator();
        var outcome = new FakeOutcomeSummaryBuilder();

        Harness harness = null!;
        var vm = new DeployQuickDeployViewModel(
            referenceData,
            new DeployResolveSuggestionsService(),
            (_, _) =>
            {
                harness.TemplateEditorCount++;
                return Task.CompletedTask;
            },
            preflight,
            coordinator,
            outcome,
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
            Coordinator = coordinator,
            Outcome = outcome
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

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.ElapsedMilliseconds < timeoutMs)
        {
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
    public void CommandCanExecute_ReflectsEntryPresence()
    {
        var harness = CreateHarness();

        Assert.True(harness.Vm.CanAddVm);
        Assert.False(harness.Vm.CanRemoveVm);
        Assert.False(harness.Vm.CanResolveSuggestions);
        Assert.False(harness.Vm.CanOpenTemplateEditor);
        Assert.False(harness.Vm.CanStartDeploy);

        harness.Vm.AddVmCommand.Execute(null);

        Assert.True(harness.Vm.CanRemoveVm);
        Assert.True(harness.Vm.CanResolveSuggestions);
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
        Assert.Equal(55, harness.Vm.ProgressPercent);
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
        Assert.Equal(45, harness.Vm.ProgressPercent);
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
    public async Task StartDeploy_ThenNavigateAwayCleanup_CancelsInFlightRun()
    {
        var harness = CreateHarness();
        await ActivateReadyVmAsync(harness);

        var release = new TaskCompletionSource();
        harness.Coordinator.OnDeploy = _ => release.Task;

        var deployTask = harness.Vm.StartDeployCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => harness.Coordinator.LastContext is not null);
        Assert.NotNull(harness.Coordinator.LastContext);
        Assert.False(harness.Coordinator.LastContext!.IsCancellationRequested);

        await harness.Vm.CleanupAsync();

        Assert.True(harness.Coordinator.LastContext!.UserCancellationRequested);
        Assert.True(harness.Coordinator.LastContext!.IsCancellationRequested);

        release.SetResult();
        await deployTask;
    }

    [Fact]
    public async Task StartDeploy_BlockedReadiness_DoesNotInvokeCoordinator()
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

        Assert.Equal(0, harness.Coordinator.CallCount);
        Assert.Equal("Blocked", harness.Vm.LifecycleState);
    }

    [Fact]
    public async Task StartDeploy_Succeeds_ProjectsOutcomeRows()
    {
        var harness = CreateHarness();
        harness.Outcome.Summary = new DeploymentOutcomeSummary
        {
            OperationState = DeploymentOperationState.Completed,
            TotalVmCount = 1,
            SucceededVmCount = 1,
            VmOutcomes =
            [
                new VmDeploymentOutcomeSummary
                {
                    VmName = "Quick VM 1",
                    Status = VmDeploymentOutcomeStatus.Succeeded,
                    Cleanup = new VmCleanupOutcomeSummary { CleanupRan = false }
                }
            ]
        };
        await ActivateReadyVmAsync(harness);

        await harness.Vm.StartDeployCommand.ExecuteAsync(null);

        Assert.Equal(1, harness.Coordinator.CallCount);
        Assert.Equal("Completed", harness.Vm.LifecycleState);
        Assert.Equal(100, harness.Vm.ProgressPercent);
        var row = Assert.Single(harness.Vm.ResultRows);
        Assert.Equal("Quick VM 1", row.VmName);
        Assert.Equal("Succeeded", row.Status);
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
}
