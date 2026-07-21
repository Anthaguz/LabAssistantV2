using LabAssistant.Business.Machines;
using LabAssistant.WinUI.ViewModels.Machines;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Headless coverage for the Machines edit dirty-state contract (F17): the "Unsaved changes"
/// legend and the Save button enabled-state are both driven by <see cref="MachinesViewModel.HasDirtyEdits"/>
/// and <see cref="MachinesViewModel.CanSaveChanges"/>, so they can never disagree.
/// </summary>
public sealed class MachinesViewModelEditStateTests
{
    private static MachineEditSnapshot BaselineSnapshot() => new()
    {
        CpuCount = 4,
        StartupMemoryMb = 2048,
        DynamicMemoryEnabled = false,
        MinimumMemoryMb = 1024,
        MaximumMemoryMb = 4096,
        MemoryBufferPercent = 20,
        NetworkAdapters = Array.Empty<MachineNetworkAdapterConfig>()
    };

    private static async Task<(MachinesViewModel ViewModel, MachinesFakeCapabilityService Service)> CreateSelectedAsync()
    {
        var service = new MachinesFakeCapabilityService();
        service.Inventory.Add(new MachineInventoryItem { VmId = "id-1", VmName = "vm-1", State = "Off", OriginLabel = "LabAssistant" });
        service.SnapshotsByVmName["vm-1"] = BaselineSnapshot();

        var viewModel = new MachinesViewModel(service);
        await viewModel.EnsureInventoryAsync(forceRefresh: true);

        // Selecting the machine synchronously drives the edit-snapshot load because the fake
        // completes every task inline.
        viewModel.SelectedMachine = viewModel.Machines.Single();
        return (viewModel, service);
    }

    [Fact]
    public async Task FreshSelection_IsNotDirty_AndSaveDisabled()
    {
        var (viewModel, _) = await CreateSelectedAsync();

        Assert.False(viewModel.HasDirtyEdits);
        Assert.False(viewModel.CanSaveChanges);
        Assert.Equal("4", viewModel.CpuCount);
    }

    [Fact]
    public async Task EditingCpuCount_MarksDirty_AndEnablesSave()
    {
        var (viewModel, _) = await CreateSelectedAsync();

        viewModel.CpuCount = "8";

        Assert.True(viewModel.HasDirtyEdits);
        Assert.True(viewModel.CanSaveChanges);
    }

    [Fact]
    public async Task RevertingEditBackToBaseline_ClearsDirty_AndDisablesSave()
    {
        var (viewModel, _) = await CreateSelectedAsync();

        viewModel.CpuCount = "8";
        Assert.True(viewModel.HasDirtyEdits);

        viewModel.CpuCount = "4";

        Assert.False(viewModel.HasDirtyEdits);
        Assert.False(viewModel.CanSaveChanges);
    }

    [Fact]
    public async Task DirtyAndSaveEnabled_AlwaysAgree_AcrossEdits()
    {
        var (viewModel, _) = await CreateSelectedAsync();

        // The legend (HasDirtyEdits) and the button (CanSaveChanges) must stay in lockstep.
        Assert.Equal(viewModel.HasDirtyEdits, viewModel.CanSaveChanges);

        viewModel.StartupMemory = "4096";
        Assert.Equal(viewModel.HasDirtyEdits, viewModel.CanSaveChanges);
        Assert.True(viewModel.HasDirtyEdits);

        viewModel.StartupMemory = "2048";
        Assert.Equal(viewModel.HasDirtyEdits, viewModel.CanSaveChanges);
        Assert.False(viewModel.HasDirtyEdits);
    }

    [Fact]
    public async Task InvalidNumericEdit_DoesNotEnableSave()
    {
        var (viewModel, _) = await CreateSelectedAsync();

        viewModel.CpuCount = "not-a-number";

        Assert.False(viewModel.HasDirtyEdits);
        Assert.False(viewModel.CanSaveChanges);
    }

    [Fact]
    public async Task NoSelection_SaveDisabled_AndNotDirty()
    {
        var (viewModel, _) = await CreateSelectedAsync();

        viewModel.SelectedMachine = null;

        Assert.False(viewModel.HasDirtyEdits);
        Assert.False(viewModel.CanSaveChanges);
    }
}
