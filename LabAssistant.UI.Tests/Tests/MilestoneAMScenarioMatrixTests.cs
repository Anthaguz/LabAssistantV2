using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneAMScenarioMatrixTests
{
    [Fact]
    public void MainWindow_UsesMachinesWorkspaceSeams_WithoutOwningPrimaryMachinesStateOrOrchestration()
    {
        var source = File.ReadAllText(Path.Combine("..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml.cs"));

        Assert.Contains("private readonly MachinesWorkspaceViewModel _machinesWorkspace = new();", source);
        Assert.Contains("private readonly MachinesWorkspaceController _machinesWorkspaceController;", source);
        Assert.Contains("_machinesWorkspaceController = new MachinesWorkspaceController(_machinesCapabilityService, _machinesWorkspace, this);", source);
        Assert.Contains("MachinesListView.ItemsSource = _machinesWorkspace.Inventory;", source);

        Assert.DoesNotContain("private readonly ObservableCollection<MachineInventoryItem> _machineInventory = [];", source);
        Assert.DoesNotContain("private readonly Dictionary<string, MachineRdpReadinessResult> _rdpReadinessByVmKey = new(StringComparer.OrdinalIgnoreCase);", source);
        Assert.DoesNotContain("private async Task<bool> EnsureMachinesInventoryAsync(bool forceRefresh)", source);
        Assert.DoesNotContain("private async Task RunMachineOperationAsync(", source);
        Assert.DoesNotContain("private async Task LoadMachineEditStateAsync()", source);
        Assert.DoesNotContain("private async Task RefreshRdpReadinessAsync(bool selectedOnly)", source);
    }

    [Fact]
    public void MachinesWorkspaceController_OwnsMachinesActionAndReadinessOrchestration()
    {
        var source = File.ReadAllText(Path.Combine("..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Machines", "MachinesWorkspaceController.cs"));

        Assert.Contains("internal sealed class MachinesWorkspaceController", source);
        Assert.Contains("private readonly MachinesWorkspaceViewModel _workspace;", source);
        Assert.Contains("public async Task<bool> EnsureInventoryAsync(bool forceRefresh)", source);
        Assert.Contains("public async Task HandleSelectionChangedAsync(MachineInventoryItem? selectedMachine)", source);
        Assert.Contains("public async Task StartSelectedMachineAsync()", source);
        Assert.Contains("public async Task StopSelectedMachineAsync()", source);
        Assert.Contains("public async Task RestartSelectedMachineAsync()", source);
        Assert.Contains("public async Task OpenSelectedMachineConsoleAsync()", source);
        Assert.Contains("public async Task OpenSelectedMachineRdpAsync()", source);
        Assert.Contains("public async Task DeleteSelectedMachineAsync()", source);
        Assert.Contains("public async Task ApplySelectedMachineEditsAsync()", source);
        Assert.Contains("public async Task RefreshRdpReadinessAsync(bool selectedOnly)", source);
        Assert.Contains("_host.ShowDeleteScopeDialogAsync", source);
        Assert.Contains("_host.ShowDeleteConfirmationDialogAsync", source);
    }

    [Fact]
    public void MachinesWorkspaceViewModel_OwnsInventorySelectionDraftAndReadinessState()
    {
        var source = File.ReadAllText(Path.Combine("..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Machines", "MachinesWorkspaceViewModel.cs"));

        Assert.Contains("public ObservableCollection<MachineInventoryItem> Inventory { get; } = [];", source);
        Assert.Contains("public Dictionary<string, MachineRdpReadinessResult> RdpReadinessByVmKey { get; } = new(StringComparer.OrdinalIgnoreCase);", source);
        Assert.Contains("public MachineInventoryItem? SelectedMachine { get; set; }", source);
        Assert.Contains("public MachineEditSnapshot? LoadedEditSnapshot { get; set; }", source);
        Assert.Contains("public MachineEditDraft? EditDraft { get; set; }", source);
        Assert.Contains("public bool IsMachineActionRunning { get; set; }", source);
        Assert.Contains("public bool IsInventoryRefreshing { get; set; }", source);
        Assert.Contains("public bool IsRdpReadinessRefreshRunning { get; set; }", source);
        Assert.Contains("public string StatusText { get; set; } = \"Select a VM to run actions.\"", source);
        Assert.Contains("public bool HasEditChanges =>", source);
        Assert.Contains("public void DiscardEditDraft()", source);
    }

    [Fact]
    public void MachinesBehaviorAnchors_RemainRepresented_AfterStateAndOrchestrationExtraction()
    {
        var mainWindowSource = File.ReadAllText(Path.Combine("..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml.cs"));
        var machinesViewSource = File.ReadAllText(Path.Combine("..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Machines", "MachinesOverviewView.xaml"));
        var machinesViewCodeBehind = File.ReadAllText(Path.Combine("..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Machines", "MachinesOverviewView.xaml.cs"));

        Assert.Contains("MachinesOverviewPanel.Visibility = IsMachinesOverviewActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.Contains("await _machinesWorkspaceController.EnsureInventoryAsync(forceRefresh: true);", mainWindowSource);
        Assert.Contains("private void SetMachinesStatus(string message)", mainWindowSource);
        Assert.Contains("bool IMachinesWorkspaceControllerHost.IsMachinesOverviewActive => IsMachinesOverviewActive;", mainWindowSource);

        Assert.Contains("x:Name=\"MachinesInventoryRegion\"", machinesViewSource);
        Assert.Contains("x:Name=\"MachinesDetailsRegion\"", machinesViewSource);
        Assert.Contains("x:Name=\"ApplyMachineEditsButton\"", machinesViewSource);
        Assert.Contains("x:Name=\"OpenConsoleButton\"", machinesViewSource);
        Assert.Contains("x:Name=\"OpenRdpButton\"", machinesViewSource);
        Assert.Contains("x:Name=\"DeleteVmButton\"", machinesViewSource);

        Assert.Contains("private const double CompactLayoutThreshold = 1024;", machinesViewCodeBehind);
        Assert.Contains("UpdateLayoutMode(e.NewSize.Width);", machinesViewCodeBehind);
    }
}
