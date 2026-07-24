using LabAssistant.Business.Machines;
using LabAssistant.WinUI.ViewModels.Machines;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Headless coverage for the F07 multi-select bulk actions: selection gating, bulk power runs over
/// several VMs, partial-failure aggregation, bulk delete scope semantics, and cancellation at a VM
/// boundary. All logic lives in the view model so it is exercised without a live ListView.
/// </summary>
public sealed class MachinesViewModelBulkActionTests
{
    private static async Task<(MachinesViewModel ViewModel, MachinesFakeCapabilityService Service)> CreateWithInventoryAsync(
        params string[] vmNames)
    {
        var service = new MachinesFakeCapabilityService();
        foreach (var name in vmNames)
        {
            service.Inventory.Add(new MachineInventoryItem { VmId = name, VmName = name, State = "Off", OriginLabel = "LabAssistant" });
        }

        var viewModel = new MachinesViewModel(service);
        await viewModel.EnsureInventoryAsync(forceRefresh: true);
        return (viewModel, service);
    }

    private static List<MachineListItem> Rows(MachinesViewModel viewModel, params string[] vmNames)
        => vmNames.Select(name => viewModel.Machines.Single(m => m.VmName == name)).ToList();

    [Fact]
    public async Task UpdateSelection_FlipsSelectionGatesAndDetail()
    {
        var (viewModel, _) = await CreateWithInventoryAsync("vm-a", "vm-b", "vm-c");

        viewModel.UpdateSelection(new List<MachineListItem>());
        Assert.Equal(0, viewModel.SelectedCount);
        Assert.False(viewModel.HasAnySelection);
        Assert.False(viewModel.HasSingleSelection);
        Assert.True(viewModel.ShowSelectionHint);
        Assert.Null(viewModel.SelectedMachine);

        viewModel.UpdateSelection(Rows(viewModel, "vm-a"));
        Assert.Equal(1, viewModel.SelectedCount);
        Assert.True(viewModel.HasAnySelection);
        Assert.True(viewModel.HasSingleSelection);
        Assert.True(viewModel.ShowSingleMachineControls);
        Assert.Equal("vm-a", viewModel.SelectedMachine?.VmName);

        viewModel.UpdateSelection(Rows(viewModel, "vm-a", "vm-b"));
        Assert.Equal(2, viewModel.SelectedCount);
        Assert.True(viewModel.HasAnySelection);
        Assert.False(viewModel.HasSingleSelection);
        Assert.True(viewModel.ShowBulkActions);
        Assert.False(viewModel.ShowSingleMachineControls);
        Assert.Equal("2 selected", viewModel.SelectionSummaryText);
        // The single-VM detail pane is cleared while more than one VM is selected.
        Assert.Null(viewModel.SelectedMachine);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("stop")]
    [InlineData("restart")]
    [InlineData("turn_off")]
    public async Task BulkPowerAction_RunsOverEverySelectedVm(string action)
    {
        var (viewModel, service) = await CreateWithInventoryAsync("vm-a", "vm-b", "vm-c");
        viewModel.UpdateSelection(Rows(viewModel, "vm-a", "vm-b", "vm-c"));

        var command = action switch
        {
            "start" => viewModel.StartVmCommand,
            "stop" => viewModel.StopVmCommand,
            "restart" => viewModel.RestartVmCommand,
            "turn_off" => viewModel.TurnOffVmCommand,
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        await command.ExecuteAsync(null);

        Assert.Equal(3, service.ActionCalls.Count(c => c.Action == action));
        Assert.Contains(service.ActionCalls, c => c.VmName == "vm-a" && c.Action == action);
        Assert.Contains(service.ActionCalls, c => c.VmName == "vm-b" && c.Action == action);
        Assert.Contains(service.ActionCalls, c => c.VmName == "vm-c" && c.Action == action);
        Assert.Contains("3 of 3", viewModel.StatusMessage);
    }

    [Fact]
    public async Task BulkPowerAction_WhenOneVmThrows_OthersStillRunAndSummaryReportsFailure()
    {
        var (viewModel, service) = await CreateWithInventoryAsync("vm-a", "vm-b", "vm-c");
        service.ActionExceptionsByVmName["vm-b"] = "boom";
        viewModel.UpdateSelection(Rows(viewModel, "vm-a", "vm-b", "vm-c"));

        await viewModel.StartVmCommand.ExecuteAsync(null);

        // The failing VM never records an action, but the others still run: isolation, not abort.
        Assert.Contains(service.ActionCalls, c => c.VmName == "vm-a" && c.Action == "start");
        Assert.Contains(service.ActionCalls, c => c.VmName == "vm-c" && c.Action == "start");
        Assert.DoesNotContain(service.ActionCalls, c => c.VmName == "vm-b" && c.Action == "start");
        Assert.Contains("Started 2 of 3", viewModel.StatusMessage);
        Assert.Contains("vm-b - boom", viewModel.StatusMessage);
    }

    [Fact]
    public async Task BulkDelete_VmOnlyScope_DeletesNoStorageForAnyVm()
    {
        var (viewModel, service) = await CreateWithInventoryAsync("vm-safe", "vm-unsafe");
        service.SafeForAutomaticStorageDeletionByVmName["vm-safe"] = true;
        service.SafeForAutomaticStorageDeletionByVmName["vm-unsafe"] = false;
        var bridge = new CapturingShellBridge { BulkDeleteScopeResult = MachineDeleteScope.VmRegistrationOnly };
        viewModel.AttachShellBridge(bridge);
        viewModel.UpdateSelection(Rows(viewModel, "vm-safe", "vm-unsafe"));

        await viewModel.DeleteVmCommand.ExecuteAsync(null);

        Assert.Equal(2, service.DeleteCalls.Count);
        Assert.All(service.DeleteCalls, call => Assert.Equal(MachineDeleteScope.VmRegistrationOnly, call.Scope));
        // The dialog was given both VMs and the unsafe flag surfaced so the user can see the risk.
        Assert.NotNull(bridge.CapturedBulkDeleteCandidates);
        Assert.Equal(2, bridge.CapturedBulkDeleteCandidates!.Count);
        Assert.Contains(bridge.CapturedBulkDeleteCandidates!, c => c.Vm.VmName == "vm-unsafe" && !c.Preview.SafeForAutomaticStorageDeletion);
    }

    [Fact]
    public async Task BulkDelete_VmAndStorageScope_DeletesStorageForAllIncludingUnsafeVm()
    {
        var (viewModel, service) = await CreateWithInventoryAsync("vm-safe", "vm-unsafe");
        service.SafeForAutomaticStorageDeletionByVmName["vm-safe"] = true;
        service.SafeForAutomaticStorageDeletionByVmName["vm-unsafe"] = false;
        var bridge = new CapturingShellBridge { BulkDeleteScopeResult = MachineDeleteScope.VmAndStorage };
        viewModel.AttachShellBridge(bridge);
        viewModel.UpdateSelection(Rows(viewModel, "vm-safe", "vm-unsafe"));

        await viewModel.DeleteVmCommand.ExecuteAsync(null);

        Assert.Equal(2, service.DeleteCalls.Count);
        Assert.All(service.DeleteCalls, call => Assert.Equal(MachineDeleteScope.VmAndStorage, call.Scope));
        // The explicit VM + storage choice overrides safety even for the unsafe-flagged VM.
        Assert.Contains(service.DeleteCalls, call => call.VmName == "vm-unsafe" && call.Scope == MachineDeleteScope.VmAndStorage);
    }

    [Fact]
    public async Task BulkDelete_WhenDialogCancelled_DeletesNothing()
    {
        var (viewModel, service) = await CreateWithInventoryAsync("vm-a", "vm-b");
        var bridge = new CapturingShellBridge { BulkDeleteScopeResult = null };
        viewModel.AttachShellBridge(bridge);
        viewModel.UpdateSelection(Rows(viewModel, "vm-a", "vm-b"));

        await viewModel.DeleteVmCommand.ExecuteAsync(null);

        Assert.Equal(1, bridge.BulkDeleteDialogCallCount);
        Assert.Empty(service.DeleteCalls);
        Assert.Equal("Delete cancelled.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task BulkPowerAction_WhenCancelledMidBatch_StopsAtNextVmBoundary()
    {
        var (viewModel, service) = await CreateWithInventoryAsync("vm-a", "vm-b", "vm-c");
        viewModel.UpdateSelection(Rows(viewModel, "vm-a", "vm-b", "vm-c"));

        // Request cancellation while the first VM is being processed; the runner must finish that VM
        // and then stop at the next boundary rather than mid-op.
        service.OnActionInvoked = (vm, _) =>
        {
            if (vm.VmName == "vm-a")
            {
                viewModel.CleanupAsync().GetAwaiter().GetResult();
            }
        };

        await viewModel.StartVmCommand.ExecuteAsync(null);

        Assert.Contains(service.ActionCalls, c => c.VmName == "vm-a" && c.Action == "start");
        Assert.DoesNotContain(service.ActionCalls, c => c.VmName == "vm-b" && c.Action == "start");
        Assert.DoesNotContain(service.ActionCalls, c => c.VmName == "vm-c" && c.Action == "start");
        Assert.Contains("Started 1 of 3", viewModel.StatusMessage);
        Assert.Contains("Cancelled", viewModel.StatusMessage);
    }

    private sealed class CapturingShellBridge : IMachinesCapabilityShellBridge
    {
        public MachineDeleteScope? BulkDeleteScopeResult { get; set; }

        public IReadOnlyList<MachineBulkDeleteCandidate>? CapturedBulkDeleteCandidates { get; private set; }

        public int BulkDeleteDialogCallCount { get; private set; }

        public bool IsMachinesOverviewActive => true;

        public void UpdateReadinessPollingState() { }

        public Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview)
            => Task.FromResult<MachineDeleteScope?>(null);

        public Task<bool> ShowDeleteConfirmationDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview, MachineDeleteScope effectiveScope)
            => Task.FromResult(false);

        public Task<MachineDeleteScope?> ShowBulkDeleteScopeDialogAsync(IReadOnlyList<MachineBulkDeleteCandidate> candidates)
        {
            BulkDeleteDialogCallCount++;
            CapturedBulkDeleteCandidates = candidates;
            return Task.FromResult(BulkDeleteScopeResult);
        }

        public Task<string?> ShowRenameDialogAsync(MachineInventoryItem vm)
            => Task.FromResult<string?>(null);
    }
}
