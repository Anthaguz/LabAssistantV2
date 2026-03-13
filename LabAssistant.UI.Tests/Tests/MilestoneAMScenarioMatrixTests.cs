using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneAMScenarioMatrixTests
{
    [Fact]
    public void MainWindow_PreservesShellBoundary_WhileHostingLongLivedMachinesWorkspace()
    {
        var mainWindowSource = LoadMainWindowSource();
        var shellViewModelSource = LoadShellViewModelSource();

        Assert.Contains("public sealed partial class MainWindow : Window", mainWindowSource);
        Assert.Contains("private readonly MachinesWorkspaceComposition _machinesWorkspaceComposition;", mainWindowSource);
        Assert.Contains("_machinesWorkspaceComposition = new MachinesWorkspaceComposition(", mainWindowSource);
        Assert.Contains("new MachinesWorkspaceShellBridge(", mainWindowSource);
        Assert.Contains("() => IsMachinesOverviewActive,", mainWindowSource);
        Assert.Contains("UpdateReadinessPollingState,", mainWindowSource);
        Assert.Contains("() => RootLayout.XamlRoot));", mainWindowSource);
        Assert.Contains("await _machinesWorkspaceComposition.EnsureInventoryAsync(forceRefresh: true);", mainWindowSource);
        Assert.Contains("_ = _machinesWorkspaceComposition.EnsureInventoryAsync(forceRefresh: false);", mainWindowSource);
        Assert.Contains("await _machinesWorkspaceComposition.RefreshRdpReadinessAsync(selectedOnly: false);", mainWindowSource);
        Assert.Contains("_machinesWorkspaceComposition.ApplyShellState();", mainWindowSource);
        Assert.Contains("_machinesWorkspaceComposition.DiscardEditDraft();", mainWindowSource);
        Assert.Contains("private FrameworkElement MachinesOverviewPanel => MachinesOverviewViewHost;", mainWindowSource);
        Assert.Contains("private bool IsMachinesOverviewActive =>", mainWindowSource);
        Assert.Contains("MachinesOverviewPanel.Visibility = IsMachinesOverviewActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);

        Assert.DoesNotContain("public sealed partial class MainWindow : Window, IMachinesWorkspaceShellBridge", mainWindowSource);
        Assert.DoesNotContain("private readonly MachinesWorkspaceViewModel _machinesWorkspace = new();", mainWindowSource);
        Assert.DoesNotContain("private readonly MachinesWorkspaceController _machinesWorkspaceController;", mainWindowSource);
        Assert.DoesNotContain("private async Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync(", mainWindowSource);
        Assert.DoesNotContain("private async Task<bool> ShowDeleteConfirmationDialogAsync(", mainWindowSource);
        Assert.DoesNotContain("private void UpdateMachineDetails()", mainWindowSource);
        Assert.DoesNotContain("private void ClearMachineEditControls()", mainWindowSource);
        Assert.DoesNotContain("private void ApplyMachineEditDraftToControls()", mainWindowSource);
        Assert.DoesNotContain("private void UpdateMachineEditDirtyIndicator()", mainWindowSource);
        Assert.DoesNotContain("private void UpdateMachineEditDraftFromControls()", mainWindowSource);

        Assert.Contains("public const string MachinesOverview = \"machines.overview\";", shellViewModelSource);
        Assert.Contains("public string StartupRoute => ShellRouteKeys.MachinesOverview;", shellViewModelSource);
    }

    [Fact]
    public void MachinesWorkspaceComposition_IsTheEffectiveMachinesLocalCompositionOwner()
    {
        var compositionSource = LoadMachinesWorkspaceCompositionSource();

        Assert.Contains("internal sealed class MachinesWorkspaceComposition : IMachinesWorkspaceControllerHost", compositionSource);
        Assert.Contains("private readonly MachinesOverviewView _view;", compositionSource);
        Assert.Contains("private readonly MachinesWorkspaceViewModel _workspace = new();", compositionSource);
        Assert.Contains("private readonly MachinesWorkspaceController _controller;", compositionSource);
        Assert.Contains("private readonly IMachinesWorkspaceShellBridge _shellBridge;", compositionSource);
        Assert.Contains("_controller = new MachinesWorkspaceController(machinesCapabilityService, _workspace, this);", compositionSource);
        Assert.Contains("_view.SetInventorySource(_workspace.Inventory);", compositionSource);
        Assert.Contains("_view.SetStatusText(_workspace.StatusText);", compositionSource);
        Assert.Contains("_view.RefreshRequested += RefreshRequested;", compositionSource);
        Assert.Contains("_view.SelectedMachineChanged += SelectedMachineChanged;", compositionSource);
        Assert.Contains("_view.MachineEditChanged += MachineEditChanged;", compositionSource);
        Assert.Contains("_view.ApplyMachineEditsRequested += ApplyMachineEditsRequested;", compositionSource);
        Assert.Contains("_view.StartMachineRequested += StartMachineRequested;", compositionSource);
        Assert.Contains("_view.StopMachineRequested += StopMachineRequested;", compositionSource);
        Assert.Contains("_view.RestartMachineRequested += RestartMachineRequested;", compositionSource);
        Assert.Contains("_view.OpenConsoleRequested += OpenConsoleRequested;", compositionSource);
        Assert.Contains("_view.DeleteMachineRequested += DeleteMachineRequested;", compositionSource);
        Assert.Contains("_view.OpenRdpRequested += OpenRdpRequested;", compositionSource);
        Assert.Contains("public bool HasInventory => _workspace.Inventory.Count > 0;", compositionSource);
        Assert.Contains("public DateTimeOffset LastRdpReadinessRefreshUtc => _workspace.LastRdpReadinessRefreshUtc;", compositionSource);
        Assert.Contains("public void ApplyShellState()", compositionSource);
        Assert.Contains("public void DiscardEditDraft()", compositionSource);
        Assert.Contains("public Task EnsureInventoryAsync(bool forceRefresh)", compositionSource);
        Assert.Contains("public Task RefreshRdpReadinessAsync(bool selectedOnly)", compositionSource);
        Assert.Contains("private void UpdateMachineDetails()", compositionSource);
        Assert.Contains("private void ClearMachineEditControls()", compositionSource);
        Assert.Contains("private void ApplyMachineEditDraftToControls()", compositionSource);
        Assert.Contains("private void UpdateMachineEditDraftFromControls()", compositionSource);
        Assert.Contains("private void UpdateMachineEditDirtyIndicator()", compositionSource);
        Assert.Contains("private void UpdateMachineActionButtons()", compositionSource);
        Assert.Contains("bool IMachinesWorkspaceControllerHost.IsMachinesOverviewActive => _shellBridge.IsMachinesOverviewActive;", compositionSource);
    }

    [Fact]
    public void MachinesShellBridge_RemainsNarrowAndShellOwned()
    {
        var compositionSource = LoadMachinesWorkspaceCompositionSource();
        var shellBridgeInterfaceBlock = ExtractSection(
            compositionSource,
            "internal interface IMachinesWorkspaceShellBridge",
            "internal sealed class MachinesWorkspaceShellBridge");
        var shellBridgeClassBlock = ExtractSection(
            compositionSource,
            "internal sealed class MachinesWorkspaceShellBridge",
            "internal sealed class MachinesWorkspaceComposition");

        Assert.Contains("bool IsMachinesOverviewActive { get; }", shellBridgeInterfaceBlock);
        Assert.Contains("void UpdateReadinessPollingState();", shellBridgeInterfaceBlock);
        Assert.Contains("Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync", shellBridgeInterfaceBlock);
        Assert.Contains("Task<bool> ShowDeleteConfirmationDialogAsync", shellBridgeInterfaceBlock);

        Assert.DoesNotContain("UpdateMachineDetails", shellBridgeInterfaceBlock);
        Assert.DoesNotContain("ClearMachineEditControls", shellBridgeInterfaceBlock);
        Assert.DoesNotContain("ApplyMachineEditDraftToControls", shellBridgeInterfaceBlock);
        Assert.DoesNotContain("UpdateMachineEditDirtyIndicator", shellBridgeInterfaceBlock);
        Assert.DoesNotContain("SetSelectedMachineInView", shellBridgeInterfaceBlock);

        Assert.Contains("internal sealed class MachinesWorkspaceShellBridge : IMachinesWorkspaceShellBridge", shellBridgeClassBlock);
        Assert.Contains("private readonly Func<bool> _isMachinesOverviewActive;", shellBridgeClassBlock);
        Assert.Contains("private readonly Action _updateReadinessPollingState;", shellBridgeClassBlock);
        Assert.Contains("private readonly Func<XamlRoot?> _getXamlRoot;", shellBridgeClassBlock);
        Assert.Contains("public async Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview)", shellBridgeClassBlock);
        Assert.Contains("public async Task<bool> ShowDeleteConfirmationDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview, MachineDeleteScope effectiveScope)", shellBridgeClassBlock);
        Assert.Contains("Title = \"Delete VM\"", shellBridgeClassBlock);
        Assert.Contains("Text = $\"Policy: {preview.PolicyMode} - {preview.PolicyMessage}\"", shellBridgeClassBlock);
    }

    [Fact]
    public void MachinesControllerAndWorkspaceViewModel_PreserveWorkflowAndStateSeams()
    {
        var controllerSource = LoadMachinesWorkspaceControllerSource();
        var workspaceSource = LoadMachinesWorkspaceSource();

        Assert.Contains("internal sealed class MachinesWorkspaceController", controllerSource);
        Assert.Contains("private readonly MachinesWorkspaceViewModel _workspace;", controllerSource);
        Assert.Contains("public async Task<bool> EnsureInventoryAsync(bool forceRefresh)", controllerSource);
        Assert.Contains("public async Task HandleSelectionChangedAsync(MachineInventoryItem? selectedMachine)", controllerSource);
        Assert.Contains("public async Task StartSelectedMachineAsync()", controllerSource);
        Assert.Contains("public async Task StopSelectedMachineAsync()", controllerSource);
        Assert.Contains("public async Task RestartSelectedMachineAsync()", controllerSource);
        Assert.Contains("public async Task OpenSelectedMachineConsoleAsync()", controllerSource);
        Assert.Contains("public async Task OpenSelectedMachineRdpAsync()", controllerSource);
        Assert.Contains("public async Task DeleteSelectedMachineAsync()", controllerSource);
        Assert.Contains("public async Task ApplySelectedMachineEditsAsync()", controllerSource);
        Assert.Contains("public async Task RefreshRdpReadinessAsync(bool selectedOnly)", controllerSource);
        Assert.Contains("_host.ShowDeleteScopeDialogAsync", controllerSource);
        Assert.Contains("_host.ShowDeleteConfirmationDialogAsync", controllerSource);

        Assert.Contains("public ObservableCollection<MachineInventoryItem> Inventory { get; } = [];", workspaceSource);
        Assert.Contains("public Dictionary<string, MachineRdpReadinessResult> RdpReadinessByVmKey { get; } = new(StringComparer.OrdinalIgnoreCase);", workspaceSource);
        Assert.Contains("public MachineInventoryItem? SelectedMachine { get; set; }", workspaceSource);
        Assert.Contains("public MachineRdpReadinessResult SelectedRdpReadiness { get; set; }", workspaceSource);
        Assert.Contains("public MachineEditSnapshot? LoadedEditSnapshot { get; set; }", workspaceSource);
        Assert.Contains("public MachineEditDraft? EditDraft { get; set; }", workspaceSource);
        Assert.Contains("public bool IsMachineActionRunning { get; set; }", workspaceSource);
        Assert.Contains("public bool IsInventoryRefreshing { get; set; }", workspaceSource);
        Assert.Contains("public bool IsRdpReadinessRefreshRunning { get; set; }", workspaceSource);
        Assert.Contains("public bool CanRunSelectedMachineActions { get; set; }", workspaceSource);
        Assert.Contains("public bool CanOpenRdp { get; set; }", workspaceSource);
        Assert.Contains("public bool CanApplyEdits { get; set; }", workspaceSource);
        Assert.Contains("public bool HasEditChanges =>", workspaceSource);
        Assert.Contains("public void DiscardEditDraft()", workspaceSource);
    }

    [Fact]
    public void MachinesExtraction_StillProtectsStableRouteLifetimeAndBehaviorAnchors()
    {
        var compositionSource = LoadMachinesWorkspaceCompositionSource();
        var machinesXaml = LoadMachinesOverviewXaml();
        var machinesCodeBehindSource = LoadMachinesOverviewCodeBehindSource();

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
        Assert.Contains("public event EventHandler? ApplyMachineEditsRequested;", machinesCodeBehindSource);
        Assert.Contains("public event EventHandler? OpenConsoleRequested;", machinesCodeBehindSource);
        Assert.Contains("public event EventHandler? OpenRdpRequested;", machinesCodeBehindSource);
        Assert.Contains("public event EventHandler? DeleteMachineRequested;", machinesCodeBehindSource);
        Assert.Contains("public void UpdateActionState(", machinesCodeBehindSource);
        Assert.Contains("public MachineEditFormValues CaptureEditFormValues()", machinesCodeBehindSource);
        Assert.Contains("UpdateLayoutMode(", machinesCodeBehindSource);
        Assert.DoesNotContain("x:FieldModifier=\"public\"", machinesXaml.ToString());

        Assert.Contains("public bool HasInventory => _workspace.Inventory.Count > 0;", compositionSource);
        Assert.Contains("public DateTimeOffset LastRdpReadinessRefreshUtc => _workspace.LastRdpReadinessRefreshUtc;", compositionSource);
        Assert.Contains("UpdateReadinessPollingState()", compositionSource);
        Assert.Contains("OpenConsoleRequested", compositionSource);
        Assert.Contains("OpenRdpRequested", compositionSource);
        Assert.Contains("DeleteMachineRequested", compositionSource);
    }

    [Fact]
    public void MainWindow_PreservesShellBoundary_WhileHostingLongLivedAssetsWorkspace()
    {
        var mainWindowSource = LoadMainWindowSource();
        var shellViewModelSource = LoadShellViewModelSource();

        Assert.Contains("private readonly AssetsWorkspaceComposition _assetsWorkspaceComposition;", mainWindowSource);
        Assert.Contains("_assetsWorkspaceComposition = new AssetsWorkspaceComposition(", mainWindowSource);
        Assert.Contains("new AssetsWorkspaceShellBridge(", mainWindowSource);
        Assert.Contains("() => IsAssetsCapabilityActive,", mainWindowSource);
        Assert.Contains("() => IsAssetsOverviewActive,", mainWindowSource);
        Assert.Contains("() => IsAssetsBaseDisksActive,", mainWindowSource);
        Assert.Contains("() => IsAssetsSwitchesActive,", mainWindowSource);
        Assert.Contains("NavigateToRoute,", mainWindowSource);
        Assert.Contains("EnsureAssetsBaseDisksAsync,", mainWindowSource);
        Assert.Contains("EnsureAssetsSwitchesAsync,", mainWindowSource);
        Assert.Contains("UpdateAssetsOverviewUi,", mainWindowSource);
        Assert.Contains("UpdateAssetsBaseDisksUi,", mainWindowSource);
        Assert.Contains("UpdateAssetsSwitchesUi));", mainWindowSource);
        Assert.Contains("_assetsWorkspaceComposition.ApplyShellState();", mainWindowSource);
        Assert.Contains("private FrameworkElement AssetsOverviewPanel => AssetsOverviewViewHost;", mainWindowSource);
        Assert.Contains("private FrameworkElement AssetsBaseDisksPanel => AssetsBaseDisksViewHost;", mainWindowSource);
        Assert.Contains("private FrameworkElement AssetsSwitchesPanel => AssetsSwitchesViewHost;", mainWindowSource);
        Assert.Contains("private FrameworkElement AssetsLocalNavPanel => AssetsLocalNavigationPanel;", mainWindowSource);
        Assert.Contains("AssetsLocalNavPanel.Visibility = IsAssetsCapabilityActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.Contains("AssetsOverviewPanel.Visibility = IsAssetsOverviewActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.Contains("AssetsBaseDisksPanel.Visibility = IsAssetsBaseDisksActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.Contains("AssetsSwitchesPanel.Visibility = IsAssetsSwitchesActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);

        Assert.DoesNotContain("private bool _isUpdatingAssetsSubviewSelection;", mainWindowSource);
        Assert.DoesNotContain("private void AssetsSubviewTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)", mainWindowSource);
        Assert.DoesNotContain("private void SyncAssetsSubviewSelection()", mainWindowSource);
        Assert.DoesNotContain("AssetsOverviewOpenBaseDisksButton.Click += (_, _) => NavigateToRoute(ShellRouteKeys.AssetsBaseDisks);", mainWindowSource);
        Assert.DoesNotContain("AssetsOverviewOpenSwitchesButton.Click += (_, _) => NavigateToRoute(ShellRouteKeys.AssetsSwitches);", mainWindowSource);

        Assert.Contains("public const string AssetsOverview = \"assets.overview\";", shellViewModelSource);
        Assert.Contains("public const string AssetsBaseDisks = \"assets.base_disks\";", shellViewModelSource);
        Assert.Contains("public const string AssetsSwitches = \"assets.switches\";", shellViewModelSource);
    }

    [Fact]
    public void AssetsWorkspaceComposition_IsTheEffectiveAssetsLocalCompositionOwner()
    {
        var compositionSource = LoadAssetsWorkspaceCompositionSource();

        Assert.Contains("internal sealed class AssetsWorkspaceComposition", compositionSource);
        Assert.Contains("private readonly AssetsOverviewView _overviewView;", compositionSource);
        Assert.Contains("private readonly AssetsBaseDisksView _baseDisksView;", compositionSource);
        Assert.Contains("private readonly AssetsSwitchesView _switchesView;", compositionSource);
        Assert.Contains("private readonly TabView _subviewTabView;", compositionSource);
        Assert.Contains("private readonly IAssetsWorkspaceShellBridge _shellBridge;", compositionSource);
        Assert.Contains("private bool _isUpdatingAssetsSubviewSelection;", compositionSource);
        Assert.Contains("_baseDisksView.AssetsBaseDisksListViewControl.ItemsSource = baseDiskRows;", compositionSource);
        Assert.Contains("_switchesView.AssetsSwitchesListViewControl.ItemsSource = switchRows;", compositionSource);
        Assert.Contains("_switchesView.AssetsSwitchesAttachedVmsListViewControl.ItemsSource = attachedVmNames;", compositionSource);
        Assert.Contains("_overviewView.AssetsOverviewOpenBaseDisksButtonControl.Click += OpenBaseDisksRequested;", compositionSource);
        Assert.Contains("_overviewView.AssetsOverviewOpenSwitchesButtonControl.Click += OpenSwitchesRequested;", compositionSource);
        Assert.Contains("_subviewTabView.SelectionChanged += AssetsSubviewTabView_SelectionChanged;", compositionSource);
        Assert.Contains("public void ApplyShellState()", compositionSource);
        Assert.Contains("SyncAssetsSubviewSelection();", compositionSource);
        Assert.Contains("_ = _shellBridge.EnsureAssetsBaseDisksAsync(forceRefresh: false);", compositionSource);
        Assert.Contains("_ = _shellBridge.EnsureAssetsSwitchesAsync(forceRefresh: false);", compositionSource);
        Assert.Contains("_shellBridge.UpdateAssetsOverviewUi();", compositionSource);
        Assert.Contains("_shellBridge.UpdateAssetsBaseDisksUi();", compositionSource);
        Assert.Contains("_shellBridge.UpdateAssetsSwitchesUi();", compositionSource);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.AssetsBaseDisks);", compositionSource);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.AssetsSwitches);", compositionSource);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.AssetsOverview);", compositionSource);
        Assert.Contains("if (!_shellBridge.IsAssetsCapabilityActive)", compositionSource);
        Assert.Contains("var selectedTab = _shellBridge.IsAssetsOverviewActive", compositionSource);
        Assert.Contains(": _shellBridge.IsAssetsBaseDisksActive", compositionSource);

        Assert.DoesNotContain("MainWindow", compositionSource);
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

    private static string LoadAssetsWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "AssetsWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadShellViewModelSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "ShellViewModel.cs");
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

    private static string ExtractSection(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start marker not found: {startMarker}");
        Assert.True(end > start, $"End marker not found after start marker: {endMarker}");
        return source[start..end];
    }
}
