using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneAMScenarioMatrixTests
{
    [Fact]
    public void MainWindow_UsesMachinesWorkspaceSeams_WithoutOwningPrimaryMachinesStateOrOrchestration()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("private readonly MachinesWorkspaceViewModel _machinesWorkspace = new();", source);
        Assert.Contains("private readonly MachinesWorkspaceController _machinesWorkspaceController;", source);
        Assert.Contains("_machinesWorkspaceController = new MachinesWorkspaceController(_machinesCapabilityService, _machinesWorkspace, this);", source);
        Assert.Contains("MachinesListView.ItemsSource = _machinesWorkspace.Inventory;", source);
        Assert.Contains("MachinesStatusTextBlock.Text = _machinesWorkspace.StatusText;", source);

        Assert.DoesNotContain("private readonly ObservableCollection<MachineInventoryItem> _machineInventory = [];", source);
        Assert.DoesNotContain("private readonly Dictionary<string, MachineRdpReadinessResult> _rdpReadinessByVmKey", source);
        Assert.DoesNotContain("private MachineInventoryItem? _selectedMachine;", source);
        Assert.DoesNotContain("private MachineEditSnapshot? _loadedEditSnapshot;", source);
        Assert.DoesNotContain("private MachineEditDraft? _editDraft;", source);
        Assert.DoesNotContain("private bool _isMachineActionRunning;", source);
        Assert.DoesNotContain("private bool _isMachineEditLoading;", source);
        Assert.DoesNotContain("private bool _isMachineEditApplying;", source);
        Assert.DoesNotContain("private DateTimeOffset _lastRdpReadinessRefreshUtc", source);
        Assert.DoesNotContain("private async Task<bool> EnsureMachinesInventoryAsync(bool forceRefresh)", source);
        Assert.DoesNotContain("private async Task RunMachineOperationAsync(", source);
        Assert.DoesNotContain("private async Task LoadMachineEditStateAsync()", source);
        Assert.DoesNotContain("private async Task RefreshRdpReadinessAsync(bool selectedOnly)", source);
    }

    [Fact]
    public void MachinesWorkspaceController_OwnsMachinesActionAndReadinessOrchestration()
    {
        var source = LoadMachinesWorkspaceControllerSource();
        var mainWindowSource = LoadMainWindowSource();

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

        Assert.Contains("await _machinesWorkspaceController.EnsureInventoryAsync(forceRefresh: true);", mainWindowSource);
        Assert.Contains("await _machinesWorkspaceController.HandleSelectionChangedAsync(machine);", mainWindowSource);
        Assert.Contains("await _machinesWorkspaceController.StartSelectedMachineAsync();", mainWindowSource);
        Assert.Contains("await _machinesWorkspaceController.StopSelectedMachineAsync();", mainWindowSource);
        Assert.Contains("await _machinesWorkspaceController.RestartSelectedMachineAsync();", mainWindowSource);
        Assert.Contains("await _machinesWorkspaceController.OpenSelectedMachineConsoleAsync();", mainWindowSource);
        Assert.Contains("await _machinesWorkspaceController.OpenSelectedMachineRdpAsync();", mainWindowSource);
        Assert.Contains("await _machinesWorkspaceController.DeleteSelectedMachineAsync();", mainWindowSource);
        Assert.Contains("await _machinesWorkspaceController.ApplySelectedMachineEditsAsync();", mainWindowSource);
        Assert.Contains("await _machinesWorkspaceController.RefreshRdpReadinessAsync(selectedOnly: false);", mainWindowSource);
    }

    [Fact]
    public void MachinesWorkspaceViewModel_OwnsInventorySelectionDraftAndReadinessState()
    {
        var source = LoadMachinesWorkspaceSource();

        Assert.Contains("public ObservableCollection<MachineInventoryItem> Inventory { get; } = [];", source);
        Assert.Contains("public Dictionary<string, MachineRdpReadinessResult> RdpReadinessByVmKey { get; } = new(StringComparer.OrdinalIgnoreCase);", source);
        Assert.Contains("public IReadOnlyList<string> AvailableSwitches { get; set; } = Array.Empty<string>();", source);
        Assert.Contains("public MachineInventoryItem? SelectedMachine { get; set; }", source);
        Assert.Contains("public MachineEditSnapshot? LoadedEditSnapshot { get; set; }", source);
        Assert.Contains("public MachineEditDraft? EditDraft { get; set; }", source);
        Assert.Contains("public MachineRdpReadinessResult SelectedRdpReadiness { get; set; }", source);
        Assert.Contains("public bool IsMachineActionRunning { get; set; }", source);
        Assert.Contains("public bool IsInventoryRefreshing { get; set; }", source);
        Assert.Contains("public bool IsRdpReadinessRefreshRunning { get; set; }", source);
        Assert.Contains("public string StatusText { get; set; } = \"Select a VM to run actions.\"", source);
        Assert.Contains("public bool CanRunSelectedMachineActions { get; set; }", source);
        Assert.Contains("public bool CanOpenRdp { get; set; }", source);
        Assert.Contains("public bool CanApplyEdits { get; set; }", source);
        Assert.Contains("public bool HasEditChanges =>", source);
        Assert.Contains("public void DiscardEditDraft()", source);
        Assert.Contains("private static MachineRdpReadinessResult CreateUnknownReadiness(string message)", source);
    }

    [Fact]
    public void MachinesWorkspaceExtraction_PreservesShellBoundaryAndMachinesBehaviorAnchors()
    {
        var mainWindowSource = LoadMainWindowSource();
        var machinesXaml = LoadMachinesOverviewXaml();
        var machinesCodeBehindSource = LoadMachinesOverviewCodeBehindSource();

        Assert.Contains("private FrameworkElement MachinesOverviewPanel => MachinesOverviewViewHost;", mainWindowSource);
        Assert.Contains("private bool IsMachinesOverviewActive =>", mainWindowSource);
        Assert.Contains("bool IMachinesWorkspaceControllerHost.IsMachinesOverviewActive => IsMachinesOverviewActive;", mainWindowSource);

        Assert.NotNull(FindByName(machinesXaml, "MachinesInventoryRegion"));
        Assert.NotNull(FindByName(machinesXaml, "MachinesDetailsRegion"));
        Assert.NotNull(FindByName(machinesXaml, "ApplyMachineEditsButton"));
        Assert.NotNull(FindByName(machinesXaml, "OpenConsoleButton"));
        Assert.NotNull(FindByName(machinesXaml, "OpenRdpButton"));
        Assert.NotNull(FindByName(machinesXaml, "DeleteVmButton"));

        Assert.Contains("CompactLayoutThreshold = 1024", machinesCodeBehindSource);
        Assert.Contains("UpdateLayoutMode(", machinesCodeBehindSource);
    }

    private static string LoadMainWindowSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadMachinesWorkspaceSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Machines", "MachinesWorkspaceViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadMachinesWorkspaceControllerSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Machines", "MachinesWorkspaceController.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XDocument LoadMachinesOverviewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Machines", "MachinesOverviewView.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static string LoadMachinesOverviewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Machines", "MachinesOverviewView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
