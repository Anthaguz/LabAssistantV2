using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneAMScenarioMatrixTests
{
    [Fact]
    public void MainWindow_UsesMachinesWorkspaceComposition_WithoutOwningPrimaryMachinesLocalWiring()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("private readonly MachinesWorkspaceComposition _machinesWorkspaceComposition;", source);
        Assert.Contains("_machinesWorkspaceComposition = new MachinesWorkspaceComposition(", source);
        Assert.Contains("new MachinesWorkspaceShellBridge(", source);
        Assert.Contains("() => IsMachinesOverviewActive,", source);
        Assert.Contains("UpdateReadinessPollingState,", source);
        Assert.Contains("() => RootLayout.XamlRoot));", source);
        Assert.Contains("await _machinesWorkspaceComposition.EnsureInventoryAsync(forceRefresh: true);", source);
        Assert.Contains("_machinesWorkspaceComposition.ApplyShellState();", source);
        Assert.Contains("_machinesWorkspaceComposition.DiscardEditDraft();", source);
        Assert.Contains("_ = _machinesWorkspaceComposition.EnsureInventoryAsync(forceRefresh: false);", source);
        Assert.Contains("await _machinesWorkspaceComposition.RefreshRdpReadinessAsync(selectedOnly: false);", source);
        Assert.DoesNotContain("public sealed partial class MainWindow : Window, IMachinesWorkspaceShellBridge", source);

        Assert.DoesNotContain("private readonly MachinesWorkspaceViewModel _machinesWorkspace = new();", source);
        Assert.DoesNotContain("private readonly MachinesWorkspaceController _machinesWorkspaceController;", source);
        Assert.DoesNotContain("new MachinesWorkspaceController(_machinesCapabilityService, _machinesWorkspace, this)", source);
        Assert.DoesNotContain("MachinesOverviewViewHost.SetInventorySource(_machinesWorkspace.Inventory);", source);
        Assert.DoesNotContain("MachinesOverviewViewHost.SetStatusText(_machinesWorkspace.StatusText);", source);
        Assert.DoesNotContain("MachinesOverviewViewHost.RefreshRequested +=", source);
        Assert.DoesNotContain("MachinesOverviewViewHost.SelectedMachineChanged +=", source);
        Assert.DoesNotContain("MachinesOverviewViewHost.MachineEditChanged +=", source);
        Assert.DoesNotContain("private readonly ObservableCollection<MachineInventoryItem> _machineInventory = [];", source);
        Assert.DoesNotContain("private readonly Dictionary<string, MachineRdpReadinessResult> _rdpReadinessByVmKey", source);
        Assert.DoesNotContain("private MachineInventoryItem? _selectedMachine;", source);
        Assert.DoesNotContain("private MachineEditSnapshot? _loadedEditSnapshot;", source);
        Assert.DoesNotContain("private MachineEditDraft? _editDraft;", source);
        Assert.DoesNotContain("private bool _isMachineActionRunning;", source);
        Assert.DoesNotContain("private bool _isMachineEditLoading;", source);
        Assert.DoesNotContain("private bool _isMachineEditApplying;", source);
        Assert.DoesNotContain("private DateTimeOffset _lastRdpReadinessRefreshUtc;", source);
        Assert.DoesNotContain("private async Task<bool> EnsureMachinesInventoryAsync(bool forceRefresh)", source);
        Assert.DoesNotContain("private async Task RunMachineOperationAsync(", source);
        Assert.DoesNotContain("private async Task LoadMachineEditStateAsync()", source);
        Assert.DoesNotContain("private async Task RefreshRdpReadinessAsync(bool selectedOnly)", source);
        Assert.DoesNotContain("private Button RefreshMachinesButton =>", source);
        Assert.DoesNotContain("private ListView MachinesListView =>", source);
        Assert.DoesNotContain("private TextBox CpuCountTextBox =>", source);
        Assert.DoesNotContain("private Button OpenRdpButton =>", source);
    }

    [Fact]
    public void MachinesWorkspaceComposition_OwnsMachinesViewControllerAndViewModelComposition()
    {
        var source = LoadMachinesWorkspaceCompositionSource();
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains("internal sealed class MachinesWorkspaceComposition : IMachinesWorkspaceControllerHost", source);
        Assert.Contains("private readonly MachinesOverviewView _view;", source);
        Assert.Contains("private readonly MachinesWorkspaceViewModel _workspace = new();", source);
        Assert.Contains("private readonly MachinesWorkspaceController _controller;", source);
        Assert.Contains("private readonly IMachinesWorkspaceShellBridge _shellBridge;", source);
        Assert.Contains("internal sealed class MachinesWorkspaceShellBridge : IMachinesWorkspaceShellBridge", source);
        Assert.Contains("private readonly Func<bool> _isMachinesOverviewActive;", source);
        Assert.Contains("private readonly Action _updateReadinessPollingState;", source);
        Assert.Contains("private readonly Func<XamlRoot?> _getXamlRoot;", source);
        Assert.Contains("_controller = new MachinesWorkspaceController(machinesCapabilityService, _workspace, this);", source);
        Assert.Contains("_view.SetInventorySource(_workspace.Inventory);", source);
        Assert.Contains("_view.SetStatusText(_workspace.StatusText);", source);
        Assert.Contains("_view.RefreshRequested += RefreshRequested;", source);
        Assert.Contains("_view.SelectedMachineChanged += SelectedMachineChanged;", source);
        Assert.Contains("_view.MachineEditChanged += MachineEditChanged;", source);
        Assert.Contains("public void ApplyShellState()", source);
        Assert.Contains("public void DiscardEditDraft()", source);
        Assert.Contains("public Task EnsureInventoryAsync(bool forceRefresh)", source);
        Assert.Contains("public Task RefreshRdpReadinessAsync(bool selectedOnly)", source);
        Assert.Contains("bool IMachinesWorkspaceControllerHost.IsMachinesOverviewActive => _shellBridge.IsMachinesOverviewActive;", source);
        Assert.Contains("_shellBridge.UpdateReadinessPollingState();", source);
        Assert.Contains("public async Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview)", source);
        Assert.Contains("public async Task<bool> ShowDeleteConfirmationDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview, MachineDeleteScope effectiveScope)", source);

        Assert.Contains("public sealed partial class MainWindow : Window", mainWindowSource);
        Assert.DoesNotContain("IMachinesWorkspaceShellBridge", mainWindowSource);
    }

    [Fact]
    public void MachinesWorkspaceController_OwnsMachinesActionAndReadinessOrchestration()
    {
        var source = LoadMachinesWorkspaceControllerSource();

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
        var compositionSource = LoadMachinesWorkspaceCompositionSource();
        var machinesXaml = LoadMachinesOverviewXaml();
        var machinesCodeBehindSource = LoadMachinesOverviewCodeBehindSource();

        Assert.Contains("private FrameworkElement MachinesOverviewPanel => MachinesOverviewViewHost;", mainWindowSource);
        Assert.Contains("private bool IsMachinesOverviewActive =>", mainWindowSource);
        Assert.Contains("new MachinesWorkspaceShellBridge(", mainWindowSource);
        Assert.Contains("() => RootLayout.XamlRoot));", mainWindowSource);
        Assert.DoesNotContain("ShowDeleteScopeDialogAsync,", mainWindowSource);
        Assert.DoesNotContain("ShowDeleteConfirmationDialogAsync));", mainWindowSource);
        Assert.Contains("public void ApplyShellState()", compositionSource);
        Assert.Contains("public bool HasInventory => _workspace.Inventory.Count > 0;", compositionSource);
        Assert.Contains("public DateTimeOffset LastRdpReadinessRefreshUtc => _workspace.LastRdpReadinessRefreshUtc;", compositionSource);
        Assert.Contains("private void UpdateMachineActionButtons()", compositionSource);
        Assert.Contains("private void UpdateMachineEditDraftFromControls()", compositionSource);
        Assert.Contains("internal sealed class MachinesWorkspaceShellBridge : IMachinesWorkspaceShellBridge", compositionSource);
        Assert.Contains("Title = \"Delete VM\"", compositionSource);

        Assert.NotNull(FindByName(machinesXaml, "MachinesInventoryRegion"));
        Assert.NotNull(FindByName(machinesXaml, "MachinesDetailsRegion"));
        Assert.NotNull(FindByName(machinesXaml, "ApplyMachineEditsButton"));
        Assert.NotNull(FindByName(machinesXaml, "OpenConsoleButton"));
        Assert.NotNull(FindByName(machinesXaml, "OpenRdpButton"));
        Assert.NotNull(FindByName(machinesXaml, "DeleteVmButton"));

        Assert.Contains("CompactLayoutThreshold = 1024", machinesCodeBehindSource);
        Assert.Contains("public event EventHandler? RefreshRequested;", machinesCodeBehindSource);
        Assert.Contains("public event EventHandler? SelectedMachineChanged;", machinesCodeBehindSource);
        Assert.Contains("public event EventHandler? MachineEditChanged;", machinesCodeBehindSource);
        Assert.Contains("public void UpdateActionState(", machinesCodeBehindSource);
        Assert.Contains("public MachineEditFormValues CaptureEditFormValues()", machinesCodeBehindSource);
        Assert.Contains("UpdateLayoutMode(", machinesCodeBehindSource);
        Assert.DoesNotContain("x:FieldModifier=\"public\"", machinesXaml.ToString());
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

    private static string LoadMachinesWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Machines", "MachinesWorkspaceComposition.cs");
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
