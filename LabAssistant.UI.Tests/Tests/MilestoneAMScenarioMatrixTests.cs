using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneAMScenarioMatrixTests
{
    [Fact]
    public void MachinesState_MovesBehindWorkspaceOwner_InsteadOfLivingPrimarilyInMainWindow()
    {
        var mainWindowSource = LoadMainWindowSource();
        var workspaceSource = LoadMachinesWorkspaceSource();

        Assert.Contains("private readonly MachinesWorkspaceViewModel _machinesWorkspace = new();", mainWindowSource);
        Assert.Contains("MachinesListView.ItemsSource = _machinesWorkspace.Inventory;", mainWindowSource);
        Assert.Contains("private void SetMachinesStatus(string message)", mainWindowSource);
        Assert.Contains("MachinesStatusTextBlock.Text = _machinesWorkspace.StatusText;", mainWindowSource);

        Assert.DoesNotContain("private readonly ObservableCollection<MachineInventoryItem> _machineInventory = [];", mainWindowSource);
        Assert.DoesNotContain("private readonly Dictionary<string, MachineRdpReadinessResult> _rdpReadinessByVmKey", mainWindowSource);
        Assert.DoesNotContain("private MachineInventoryItem? _selectedMachine;", mainWindowSource);
        Assert.DoesNotContain("private MachineEditSnapshot? _loadedEditSnapshot;", mainWindowSource);
        Assert.DoesNotContain("private MachineEditDraft? _editDraft;", mainWindowSource);
        Assert.DoesNotContain("private bool _isMachineActionRunning;", mainWindowSource);
        Assert.DoesNotContain("private bool _isMachineEditLoading;", mainWindowSource);
        Assert.DoesNotContain("private bool _isMachineEditApplying;", mainWindowSource);
        Assert.DoesNotContain("private DateTimeOffset _lastRdpReadinessRefreshUtc", mainWindowSource);

        Assert.Contains("public ObservableCollection<MachineInventoryItem> Inventory { get; } = [];", workspaceSource);
        Assert.Contains("public MachineInventoryItem? SelectedMachine { get; set; }", workspaceSource);
        Assert.Contains("public MachineEditSnapshot? LoadedEditSnapshot { get; set; }", workspaceSource);
        Assert.Contains("public MachineEditDraft? EditDraft { get; set; }", workspaceSource);
        Assert.Contains("public MachineRdpReadinessResult SelectedRdpReadiness { get; set; }", workspaceSource);
        Assert.Contains("public string StatusText { get; set; }", workspaceSource);
        Assert.Contains("public bool CanRunSelectedMachineActions { get; set; }", workspaceSource);
        Assert.Contains("public bool CanOpenRdp { get; set; }", workspaceSource);
        Assert.Contains("public bool CanApplyEdits { get; set; }", workspaceSource);
    }

    [Fact]
    public void MachinesWorkspaceExtraction_PreservesShellBoundaryAndMachinesBehaviorAnchors()
    {
        var mainWindowSource = LoadMainWindowSource();
        var machinesXaml = LoadMachinesOverviewXaml();
        var machinesCodeBehindSource = LoadMachinesOverviewCodeBehindSource();

        Assert.Contains("private FrameworkElement MachinesOverviewPanel => MachinesOverviewViewHost;", mainWindowSource);
        Assert.Contains("private bool IsMachinesOverviewActive =>", mainWindowSource);
        Assert.Contains("await EnsureMachinesInventoryAsync(forceRefresh: true);", mainWindowSource);
        Assert.Contains("await RefreshRdpReadinessAsync(selectedOnly: false);", mainWindowSource);
        Assert.Contains("private async Task RunMachineOperationAsync(", mainWindowSource);
        Assert.Contains("private async Task LoadMachineEditStateAsync()", mainWindowSource);
        Assert.Contains("private async Task RefreshRdpReadinessAsync(bool selectedOnly)", mainWindowSource);

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
