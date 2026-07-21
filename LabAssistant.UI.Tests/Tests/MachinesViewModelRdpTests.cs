using LabAssistant.Business.Machines;
using LabAssistant.WinUI.ViewModels.Machines;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Headless coverage for the RDP action affordance (F22): the button is disabled unless the VM is
/// RDP-ready, and <see cref="MachinesViewModel.RdpActionTooltip"/> explains why on hover instead of
/// through a stray always-on label.
/// </summary>
public sealed class MachinesViewModelRdpTests
{
    private static async Task<MachinesViewModel> CreateWithSelectionAsync(string vmName, string state)
    {
        var service = new MachinesFakeCapabilityService();
        service.Inventory.Add(new MachineInventoryItem { VmId = vmName, VmName = vmName, State = state, OriginLabel = "LabAssistant" });
        service.SnapshotsByVmName[vmName] = new MachineEditSnapshot { CpuCount = 2, StartupMemoryMb = 1024 };

        var viewModel = new MachinesViewModel(service);
        await viewModel.EnsureInventoryAsync(forceRefresh: true);
        viewModel.SelectedMachine = viewModel.Machines.Single();
        await viewModel.RefreshRdpReadinessAsync(selectedOnly: true);
        return viewModel;
    }

    [Fact]
    public void NoSelection_TooltipPromptsToSelectRunningVm()
    {
        var service = new MachinesFakeCapabilityService();
        var viewModel = new MachinesViewModel(service);

        Assert.False(viewModel.CanOpenRdp);
        Assert.Equal("Select a running VM to connect over RDP.", viewModel.RdpActionTooltip);
    }

    [Fact]
    public async Task StoppedVm_RdpDisabled_TooltipExplainsNotRunning()
    {
        var viewModel = await CreateWithSelectionAsync("vm-off", "Off");

        Assert.False(viewModel.CanOpenRdp);
        Assert.Equal("The VM must be running to connect over RDP.", viewModel.RdpActionTooltip);
    }

    [Fact]
    public async Task RunningVm_RdpEnabled_TooltipDescribesAction()
    {
        var viewModel = await CreateWithSelectionAsync("vm-run", "Running");

        Assert.True(viewModel.CanOpenRdp);
        Assert.Equal("Open a Remote Desktop connection to the selected VM.", viewModel.RdpActionTooltip);
    }
}
