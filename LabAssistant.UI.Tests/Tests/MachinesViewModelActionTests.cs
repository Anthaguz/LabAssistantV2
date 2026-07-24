using LabAssistant.Business.Machines;
using LabAssistant.WinUI.ViewModels.Machines;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Headless coverage for the new Machines actions and dynamic-memory editor wiring: hard turn off
/// (F09), rename with a shell-provided name (F08), and dynamic-memory field visibility (F16).
/// </summary>
public sealed class MachinesViewModelActionTests
{
    private static async Task<(MachinesViewModel ViewModel, MachinesFakeCapabilityService Service)> CreateWithSelectionAsync(
        string vmName,
        string state,
        bool dynamicMemory = false)
    {
        var service = new MachinesFakeCapabilityService();
        service.Inventory.Add(new MachineInventoryItem { VmId = vmName, VmName = vmName, State = state, OriginLabel = "LabAssistant" });
        service.SnapshotsByVmName[vmName] = new MachineEditSnapshot
        {
            CpuCount = 2,
            StartupMemoryMb = 1024,
            DynamicMemoryEnabled = dynamicMemory,
            MinimumMemoryMb = 512,
            MaximumMemoryMb = 4096,
            MemoryBufferPercent = 20
        };

        var viewModel = new MachinesViewModel(service);
        await viewModel.EnsureInventoryAsync(forceRefresh: true);
        viewModel.SelectedMachine = viewModel.Machines.Single();
        return (viewModel, service);
    }

    [Fact]
    public async Task TurnOffVmCommand_InvokesTurnOffOnService()
    {
        var (viewModel, service) = await CreateWithSelectionAsync("vm-run", "Running");

        await viewModel.TurnOffVmCommand.ExecuteAsync(null);

        Assert.Contains(service.ActionCalls, call => call.VmName == "vm-run" && call.Action == "turn_off");
    }

    [Fact]
    public async Task RenameVmCommand_WhenShellReturnsName_CallsRenameWithThatName()
    {
        var (viewModel, service) = await CreateWithSelectionAsync("vm-run", "Running");
        var bridge = new FakeShellBridge { RenameResult = "vm-renamed" };
        viewModel.AttachShellBridge(bridge);

        await viewModel.RenameVmCommand.ExecuteAsync(null);

        var rename = Assert.Single(service.RenameCalls);
        Assert.Equal("vm-run", rename.VmName);
        Assert.Equal("vm-renamed", rename.NewName);
    }

    [Fact]
    public async Task RenameVmCommand_WhenShellCancels_DoesNotCallRename()
    {
        var (viewModel, service) = await CreateWithSelectionAsync("vm-run", "Running");
        var bridge = new FakeShellBridge { RenameResult = null };
        viewModel.AttachShellBridge(bridge);

        await viewModel.RenameVmCommand.ExecuteAsync(null);

        Assert.Empty(service.RenameCalls);
    }

    [Fact]
    public async Task DynamicMemoryOff_HidesDynamicFields()
    {
        var (viewModel, _) = await CreateWithSelectionAsync("vm-fixed", "Off", dynamicMemory: false);

        Assert.False(viewModel.IsDynamicMemory);
        Assert.False(viewModel.ShowDynamicMemoryFields);
    }

    [Fact]
    public async Task DynamicMemoryOn_ShowsDynamicFields()
    {
        var (viewModel, _) = await CreateWithSelectionAsync("vm-dyn", "Off", dynamicMemory: true);

        Assert.True(viewModel.IsDynamicMemory);
        Assert.True(viewModel.ShowDynamicMemoryFields);
    }

    [Fact]
    public async Task TogglingDynamicMemory_RaisesShowDynamicMemoryFieldsChange()
    {
        var (viewModel, _) = await CreateWithSelectionAsync("vm-toggle", "Off", dynamicMemory: false);
        var raised = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MachinesViewModel.ShowDynamicMemoryFields))
            {
                raised = true;
            }
        };

        viewModel.IsDynamicMemory = true;

        Assert.True(raised);
        Assert.True(viewModel.ShowDynamicMemoryFields);
    }

    private sealed class FakeShellBridge : IMachinesCapabilityShellBridge
    {
        public string? RenameResult { get; set; }

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
            => Task.FromResult(RenameResult);
    }
}
