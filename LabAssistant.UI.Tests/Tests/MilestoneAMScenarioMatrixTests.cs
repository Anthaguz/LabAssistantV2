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
        Assert.Contains("private readonly AssetsBaseDisksWorkspaceComposition _assetsBaseDisksWorkspaceComposition;", mainWindowSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceComposition _assetsSwitchesWorkspaceComposition;", mainWindowSource);
        Assert.Contains("_assetsWorkspaceComposition = new AssetsWorkspaceComposition(", mainWindowSource);
        Assert.Contains("_assetsBaseDisksWorkspaceComposition,", mainWindowSource);
        Assert.Contains("_assetsSwitchesWorkspaceComposition,", mainWindowSource);
        Assert.Contains("_assetsSwitchesWorkspaceComposition = new AssetsSwitchesWorkspaceComposition(", mainWindowSource);
        Assert.Contains("_assetsBaseDisksWorkspaceComposition = new AssetsBaseDisksWorkspaceComposition(", mainWindowSource);
        Assert.Contains("new AssetsBaseDisksCompositionHost(", mainWindowSource);
        Assert.Contains("new AssetsSwitchesCompositionHost(", mainWindowSource);
        Assert.Contains("new AssetsWorkspaceHost()", mainWindowSource);
        Assert.Contains("new AssetsWorkspaceShellBridge(", mainWindowSource);
        Assert.Contains("_assetsWorkspaceComposition.ApplyShellState();", mainWindowSource);
        Assert.Contains("await _assetsBaseDisksWorkspaceComposition.EnsureInventoryAsync(forceRefresh: true);", mainWindowSource);
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
        Assert.DoesNotContain("private TextBlock AssetsOverviewBaseDisksSummaryTextBlock =>", mainWindowSource);
        Assert.DoesNotContain("private TextBlock AssetsOverviewSwitchesSummaryTextBlock =>", mainWindowSource);
        Assert.DoesNotContain("private void UpdateAssetsOverviewUi()", mainWindowSource);
        Assert.DoesNotContain("private async Task EnsureAssetsBaseDisksAsync(bool forceRefresh)", mainWindowSource);
        Assert.DoesNotContain("private void UpdateAssetsBaseDisksUi()", mainWindowSource);
        Assert.DoesNotContain("private readonly AssetsBaseDisksWorkspaceViewModel _assetsBaseDisksWorkspace = new();", mainWindowSource);
        Assert.DoesNotContain("private readonly AssetsBaseDisksWorkspaceController _assetsBaseDisksController;", mainWindowSource);
        Assert.DoesNotContain("private readonly ObservableCollection<AssetsBaseDiskListRow> _assetsBaseDiskRows = [];", mainWindowSource);
        Assert.DoesNotContain("private AssetsBaseDiskListRow? _selectedAssetsBaseDiskRow;", mainWindowSource);
        Assert.DoesNotContain("private AssetsBaseDiskDraft? _pendingAssetsBaseDiskDraft;", mainWindowSource);
        Assert.DoesNotContain("private bool _isAssetsBaseDisksLoading;", mainWindowSource);
        Assert.DoesNotContain("private bool _isAssetsBaseDisksSaving;", mainWindowSource);
        Assert.DoesNotContain("private bool _isAssetsBaseDisksRemoving;", mainWindowSource);
        Assert.DoesNotContain("private bool _isUpdatingAssetsBaseDisksEditor;", mainWindowSource);
        Assert.DoesNotContain("private bool _hasAssetsBaseDisksErrorState;", mainWindowSource);
        Assert.DoesNotContain("private readonly ObservableCollection<AssetsSwitchListRow> _assetsSwitchRows = [];", mainWindowSource);
        Assert.DoesNotContain("private readonly ObservableCollection<string> _assetsSwitchAttachedVmNames = [];", mainWindowSource);
        Assert.DoesNotContain("private readonly AssetsSwitchesWorkspaceViewModel _assetsSwitchesWorkspace = new();", mainWindowSource);
        Assert.DoesNotContain("private readonly AssetsSwitchesWorkspaceController _assetsSwitchesController;", mainWindowSource);
        Assert.DoesNotContain("private async Task RefreshAssetsSwitchValidationAsync()", mainWindowSource);
        Assert.DoesNotContain("private async Task LoadAssetsSwitchAttachedVmNamesAsync(string switchName)", mainWindowSource);
        Assert.DoesNotContain("private void ApplyAssetsSwitchValidationResult(AssetsSwitchValidationResult validation)", mainWindowSource);
        Assert.DoesNotContain("private void ApplyAssetsSwitchDeleteAssessment(AssetsSwitchDeleteAssessment assessment)", mainWindowSource);
        Assert.DoesNotContain("private Task EnsureAssetsSwitchesAsync(bool forceRefresh)", mainWindowSource);
        Assert.DoesNotContain("private void UpdateAssetsSwitchesUi()", mainWindowSource);
        Assert.DoesNotContain("private async void AssetsSwitchesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)", mainWindowSource);
        Assert.DoesNotContain("private async void AssetsSwitchesRefreshButton_Click(object sender, RoutedEventArgs e)", mainWindowSource);
        Assert.DoesNotContain("private AssetsSwitchDraft CaptureAssetsSwitchDraftFromEditor(bool isNewOverride)", mainWindowSource);
        Assert.DoesNotContain("private void ApplyAssetsSwitchesWorkspaceState(AssetsSwitchesWorkspaceViewModel workspace, bool canValidateOrApply, bool isExternalSwitchTypeSelected)", mainWindowSource);
        Assert.DoesNotContain("AssetsOverviewOpenBaseDisksButton.Click += (_, _) => NavigateToRoute(ShellRouteKeys.AssetsBaseDisks);", mainWindowSource);
        Assert.DoesNotContain("AssetsOverviewOpenSwitchesButton.Click += (_, _) => NavigateToRoute(ShellRouteKeys.AssetsSwitches);", mainWindowSource);

        Assert.Contains("public const string AssetsOverview = \"assets.overview\";", shellViewModelSource);
        Assert.Contains("public const string AssetsBaseDisks = \"assets.base_disks\";", shellViewModelSource);
        Assert.Contains("public const string AssetsSwitches = \"assets.switches\";", shellViewModelSource);
        Assert.Contains("public string StartupRoute => ShellRouteKeys.MachinesOverview;", shellViewModelSource);
    }

    [Fact]
    public void AssetsOverview_ProtectsRefinedLocalOwnershipModel()
    {
        var compositionSource = LoadAssetsWorkspaceCompositionSource();
        var baseDisksCompositionSource = LoadAssetsBaseDisksWorkspaceCompositionSource();
        var controllerSource = LoadAssetsBaseDisksWorkspaceControllerSource();
        var overviewCompositionSource = LoadAssetsOverviewWorkspaceCompositionSource();
        var baseDisksWorkspaceSource = LoadAssetsBaseDisksWorkspaceViewModelSource();
        var switchesCompositionSource = LoadAssetsSwitchesWorkspaceCompositionSource();
        var overviewWorkspaceSource = LoadAssetsOverviewWorkspaceViewModelSource();
        var overviewXaml = LoadAssetsOverviewXaml();
        var overviewCodeBehindSource = LoadAssetsOverviewCodeBehindSource();

        Assert.Contains("internal sealed class AssetsWorkspaceComposition", compositionSource);
        Assert.Contains("private readonly AssetsBaseDisksWorkspaceComposition _baseDisksWorkspaceComposition;", compositionSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceComposition _switchesWorkspaceComposition;", compositionSource);
        Assert.Contains("private readonly TabView _subviewTabView;", compositionSource);
        Assert.Contains("private readonly AssetsOverviewWorkspaceComposition _overviewWorkspaceComposition;", compositionSource);
        Assert.Contains("private readonly IAssetsWorkspaceHost _host;", compositionSource);
        Assert.Contains("private readonly IAssetsWorkspaceShellBridge _shellBridge;", compositionSource);
        Assert.Contains("private bool _isUpdatingAssetsSubviewSelection;", compositionSource);
        Assert.Contains("AssetsBaseDisksWorkspaceComposition baseDisksWorkspaceComposition,", compositionSource);
        Assert.Contains("AssetsSwitchesWorkspaceComposition switchesWorkspaceComposition,", compositionSource);
        Assert.Contains("_overviewWorkspaceComposition = new AssetsOverviewWorkspaceComposition(", compositionSource);
        Assert.Contains("new AssetsOverviewWorkspaceHost(", compositionSource);
        Assert.Contains("new AssetsOverviewWorkspaceShellBridge(", compositionSource);
        Assert.Contains("() => _baseDisksWorkspaceComposition.IsLoading,", compositionSource);
        Assert.Contains("() => _baseDisksWorkspaceComposition.InventoryCount,", compositionSource);
        Assert.Contains("() => _switchesWorkspaceComposition.IsLoading,", compositionSource);
        Assert.Contains("() => _switchesWorkspaceComposition.InventoryCount),", compositionSource);
        Assert.Contains("_subviewTabView.SelectionChanged += AssetsSubviewTabView_SelectionChanged;", compositionSource);
        Assert.Contains("public void ApplyShellState()", compositionSource);
        Assert.Contains("SyncAssetsSubviewSelection();", compositionSource);
        Assert.Contains("_overviewWorkspaceComposition.ApplyShellState();", compositionSource);
        Assert.Contains("_baseDisksWorkspaceComposition.ApplyShellState();", compositionSource);
        Assert.Contains("_switchesWorkspaceComposition.ApplyShellState();", compositionSource);
        Assert.DoesNotContain("private readonly AssetsOverviewView _overviewView;", compositionSource);
        Assert.DoesNotContain("private void UpdateAssetsOverviewUi()", compositionSource);
        Assert.DoesNotContain("OpenBaseDisksRequested", compositionSource);
        Assert.DoesNotContain("OpenSwitchesRequested", compositionSource);
        Assert.DoesNotContain("ObservableCollection<AssetsBaseDiskListRow> baseDiskRows", compositionSource);
        Assert.DoesNotContain("_host.EnsureAssetsSwitchesAsync", compositionSource);
        Assert.DoesNotContain("_host.UpdateAssetsSwitchesUi()", compositionSource);
        Assert.DoesNotContain("LoadAsync(isRefresh: forceRefresh)", compositionSource);
        Assert.DoesNotContain("ValidateAsync(draft)", compositionSource);
        Assert.DoesNotContain("RemoveAsync(", compositionSource);
        Assert.DoesNotContain("_overviewView.SetBaseDisksSummary(", compositionSource);
        Assert.DoesNotContain("_overviewView.SetSwitchesSummary(", compositionSource);
        Assert.NotNull(FindByName(overviewXaml, "AssetsOverviewBaseDisksSummaryTextBlock"));
        Assert.NotNull(FindByName(overviewXaml, "AssetsOverviewOpenBaseDisksButton"));
        Assert.NotNull(FindByName(overviewXaml, "AssetsOverviewSwitchesSummaryTextBlock"));
        Assert.NotNull(FindByName(overviewXaml, "AssetsOverviewOpenSwitchesButton"));
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.AssetsOverview);", compositionSource);
        Assert.Contains("if (!_shellBridge.IsAssetsCapabilityActive)", compositionSource);
        Assert.Contains("var selectedTab = _shellBridge.IsAssetsOverviewActive", compositionSource);
        Assert.Contains(": _shellBridge.IsAssetsBaseDisksActive", compositionSource);

        Assert.Contains("internal sealed class AssetsOverviewWorkspaceComposition", overviewCompositionSource);
        Assert.Contains("private readonly AssetsOverviewView _view;", overviewCompositionSource);
        Assert.Contains("private readonly AssetsOverviewWorkspaceViewModel _workspace = new();", overviewCompositionSource);
        Assert.Contains("private readonly IAssetsOverviewWorkspaceHost _host;", overviewCompositionSource);
        Assert.Contains("private readonly IAssetsOverviewWorkspaceShellBridge _shellBridge;", overviewCompositionSource);
        Assert.Contains("WireHandlers();", overviewCompositionSource);
        Assert.Contains("ApplyWorkspaceState();", overviewCompositionSource);
        Assert.Contains("public void ApplyShellState()", overviewCompositionSource);
        Assert.Contains("if (!_shellBridge.IsAssetsOverviewActive)", overviewCompositionSource);
        Assert.Contains("RefreshSummary();", overviewCompositionSource);
        Assert.Contains("_view.OpenBaseDisksRequested += OpenBaseDisksRequested;", overviewCompositionSource);
        Assert.Contains("_view.OpenSwitchesRequested += OpenSwitchesRequested;", overviewCompositionSource);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.AssetsBaseDisks);", overviewCompositionSource);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.AssetsSwitches);", overviewCompositionSource);
        Assert.Contains("_workspace.RefreshSummary(", overviewCompositionSource);
        Assert.Contains("_view.UpdateSummary(_workspace.BaseDisksSummaryText, _workspace.SwitchesSummaryText);", overviewCompositionSource);
        Assert.DoesNotContain("AssetsOverviewOpenBaseDisksButtonControl", overviewCompositionSource);
        Assert.DoesNotContain("AssetsOverviewOpenSwitchesButtonControl", overviewCompositionSource);
        Assert.DoesNotContain("SetBaseDisksSummary", overviewCompositionSource);
        Assert.DoesNotContain("SetSwitchesSummary", overviewCompositionSource);

        Assert.Contains("internal sealed class AssetsOverviewWorkspaceViewModel", overviewWorkspaceSource);
        Assert.Contains("public string BaseDisksSummaryText { get; private set; }", overviewWorkspaceSource);
        Assert.Contains("public string SwitchesSummaryText { get; private set; }", overviewWorkspaceSource);
        Assert.Contains("public void RefreshSummary(bool isBaseDisksLoading, bool isSwitchesLoading, int assetsBaseDiskCount, int assetsSwitchCount)", overviewWorkspaceSource);
        Assert.Contains("\"Base disk inventory is loading.\"", overviewWorkspaceSource);
        Assert.Contains("\"Switch inventory is loading.\"", overviewWorkspaceSource);

        Assert.Contains("internal sealed class AssetsBaseDisksWorkspaceViewModel", baseDisksWorkspaceSource);
        Assert.Contains("public ObservableCollection<AssetsBaseDiskListRow> Inventory { get; } = [];", baseDisksWorkspaceSource);
        Assert.Contains("public AssetsBaseDiskListRow? SelectedRow { get; set; }", baseDisksWorkspaceSource);
        Assert.Contains("public AssetsBaseDiskDraft? PendingDraft { get; set; }", baseDisksWorkspaceSource);
        Assert.Contains("public bool IsLoading { get; set; }", baseDisksWorkspaceSource);
        Assert.Contains("public bool IsSaving { get; set; }", baseDisksWorkspaceSource);
        Assert.Contains("public bool IsRemoving { get; set; }", baseDisksWorkspaceSource);
        Assert.Contains("public bool IsUpdatingEditor { get; set; }", baseDisksWorkspaceSource);
        Assert.Contains("public bool HasErrorState { get; set; }", baseDisksWorkspaceSource);
        Assert.Contains("public string StatusText { get; set; }", baseDisksWorkspaceSource);
        Assert.Contains("public string ErrorStateText { get; set; }", baseDisksWorkspaceSource);
        Assert.Contains("public string SelectedDiskSummaryText { get; set; }", baseDisksWorkspaceSource);
        Assert.Contains("public string SelectedDiskValidationText { get; set; }", baseDisksWorkspaceSource);
        Assert.Contains("public string ReferenceWarningText { get; set; }", baseDisksWorkspaceSource);

        Assert.Contains("internal sealed class AssetsSwitchesWorkspaceComposition : IAssetsSwitchesWorkspaceHost", switchesCompositionSource);
        Assert.Contains("private readonly AssetsSwitchesView _view;", switchesCompositionSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceViewModel _workspace = new();", switchesCompositionSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceController _controller;", switchesCompositionSource);
        Assert.Contains("private readonly IAssetsSwitchesCompositionHost _host;", switchesCompositionSource);
        Assert.Contains("_controller = new AssetsSwitchesWorkspaceController(capabilityService, _workspace, this);", switchesCompositionSource);
        Assert.Contains("_view.SetInventorySource(_workspace.Inventory);", switchesCompositionSource);
        Assert.Contains("_view.SetAttachedVmSource(_workspace.AttachedVmNames);", switchesCompositionSource);
        Assert.Contains("public bool IsLoading => _workspace.IsLoading;", switchesCompositionSource);
        Assert.Contains("public int InventoryCount => _workspace.Inventory.Count;", switchesCompositionSource);
        Assert.Contains("public Task EnsureInventoryAsync(bool forceRefresh)", switchesCompositionSource);
        Assert.Contains("public void ApplyShellState()", switchesCompositionSource);
        Assert.Contains("SelectedSwitchChanged += AssetsSwitchesListView_SelectionChanged;", switchesCompositionSource);
        Assert.Contains("RefreshRequested += AssetsSwitchesRefreshRequested;", switchesCompositionSource);
        Assert.Contains("CreateRequested += AssetsSwitchesCreateRequested;", switchesCompositionSource);
        Assert.Contains("ApplyRequested += AssetsSwitchesApplyRequested;", switchesCompositionSource);
        Assert.Contains("DeleteRequested += AssetsSwitchesDeleteRequested;", switchesCompositionSource);
        Assert.Contains("EditorChanged += AssetsSwitchesEditorChanged;", switchesCompositionSource);

        Assert.Contains("internal sealed class AssetsBaseDisksWorkspaceComposition : IAssetsBaseDisksWorkspaceHost", baseDisksCompositionSource);
        Assert.Contains("private readonly AssetsBaseDisksView _view;", baseDisksCompositionSource);
        Assert.Contains("private readonly AssetsBaseDisksWorkspaceViewModel _workspace = new();", baseDisksCompositionSource);
        Assert.Contains("private readonly AssetsBaseDisksWorkspaceController _controller;", baseDisksCompositionSource);
        Assert.Contains("private readonly IAssetsBaseDisksCompositionHost _host;", baseDisksCompositionSource);
        Assert.Contains("_controller = new AssetsBaseDisksWorkspaceController(capabilityService, _workspace, this);", baseDisksCompositionSource);
        Assert.Contains("_view.SetInventorySource(_workspace.Inventory);", baseDisksCompositionSource);
        Assert.Contains("WireHandlers();", baseDisksCompositionSource);
        Assert.Contains("public Task EnsureInventoryAsync(bool forceRefresh) => _controller.EnsureInventoryAsync(forceRefresh);", baseDisksCompositionSource);
        Assert.Contains("public void ApplyShellState()", baseDisksCompositionSource);
        Assert.Contains("_ = _controller.EnsureInventoryAsync(forceRefresh: false);", baseDisksCompositionSource);
        Assert.Contains("_controller.ApplyWorkspaceState();", baseDisksCompositionSource);
        Assert.Contains("AssetsBaseDisksListView_SelectionChanged", baseDisksCompositionSource);
        Assert.Contains("AssetsBaseDisksRefreshRequested", baseDisksCompositionSource);
        Assert.Contains("AssetsBaseDisksImportRequested", baseDisksCompositionSource);
        Assert.Contains("AssetsBaseDisksValidateRequested", baseDisksCompositionSource);
        Assert.Contains("AssetsBaseDisksSaveMetadataRequested", baseDisksCompositionSource);
        Assert.Contains("AssetsBaseDisksRemoveRequested", baseDisksCompositionSource);
        Assert.Contains("AssetsBaseDisksMetadataChanged", baseDisksCompositionSource);
        Assert.Contains("BuildViewState", baseDisksCompositionSource);
        Assert.Contains("AssetsBaseDiskDraft? IAssetsBaseDisksWorkspaceHost.CaptureDraft(bool isNewOverride) => CaptureDraft(isNewOverride);", baseDisksCompositionSource);
        Assert.Contains("void IAssetsBaseDisksWorkspaceHost.ApplyEditorDraft(AssetsBaseDiskDraft draft) => _view.ApplyEditorDraft(draft);", baseDisksCompositionSource);
        Assert.Contains("void IAssetsBaseDisksWorkspaceHost.ClearEditorFields() => _view.ClearEditor();", baseDisksCompositionSource);
        Assert.Contains("void IAssetsBaseDisksWorkspaceHost.SetSelectedRow(AssetsBaseDiskListRow? row) => _view.SetSelectedBaseDisk(row);", baseDisksCompositionSource);
        Assert.Contains("void IAssetsBaseDisksWorkspaceHost.SetDraftPath(string path) => _view.SetDraftPath(path);", baseDisksCompositionSource);
        Assert.DoesNotContain("MainWindow", baseDisksCompositionSource);

        Assert.Contains("internal sealed class AssetsBaseDisksWorkspaceController", controllerSource);
        Assert.Contains("public async Task EnsureInventoryAsync(bool forceRefresh)", controllerSource);
        Assert.Contains("public void HandleSelectionChanged(AssetsBaseDiskListRow? selectedRow)", controllerSource);
        Assert.Contains("public void BeginImport()", controllerSource);
        Assert.Contains("public async Task ValidateAsync()", controllerSource);
        Assert.Contains("public async Task RemoveSelectedAsync()", controllerSource);
        Assert.Contains("public void HandleBrowsePath()", controllerSource);
        Assert.Contains("public async Task SaveDraftAsync()", controllerSource);
        Assert.Contains("public void HandleMetadataChanged()", controllerSource);
        Assert.Contains("public void ApplyWorkspaceState()", controllerSource);
        Assert.Contains("_host.ApplyWorkspaceState(_workspace, canSaveDraft);", controllerSource);
        Assert.Contains("LoadEditorFromRow", controllerSource);
        Assert.Contains("LoadEditorFromDraft", controllerSource);
        Assert.Contains("ClearEditor", controllerSource);
        Assert.DoesNotContain("MainWindow", controllerSource);

        Assert.DoesNotContain("MainWindow", compositionSource);
    }

    [Fact]
    public void AssetsOverviewView_ExposesNarrowInteractionSurface_InsteadOfControlBagAccess()
    {
        var overviewXaml = LoadAssetsOverviewXaml();
        var overviewCodeBehindSource = LoadAssetsOverviewCodeBehindSource();
        var overviewCompositionSource = LoadAssetsOverviewWorkspaceCompositionSource();

        Assert.NotNull(FindByName(overviewXaml, "AssetsOverviewBaseDisksSummaryTextBlock"));
        Assert.NotNull(FindByName(overviewXaml, "AssetsOverviewOpenBaseDisksButton"));
        Assert.NotNull(FindByName(overviewXaml, "AssetsOverviewSwitchesSummaryTextBlock"));
        Assert.NotNull(FindByName(overviewXaml, "AssetsOverviewOpenSwitchesButton"));

        Assert.Contains("public event EventHandler? OpenBaseDisksRequested;", overviewCodeBehindSource);
        Assert.Contains("public event EventHandler? OpenSwitchesRequested;", overviewCodeBehindSource);
        Assert.Contains("public void UpdateSummary(string baseDisksSummaryText, string switchesSummaryText)", overviewCodeBehindSource);
        Assert.DoesNotContain("AssetsOverviewOpenBaseDisksButtonControl", overviewCodeBehindSource);
        Assert.DoesNotContain("AssetsOverviewOpenSwitchesButtonControl", overviewCodeBehindSource);
        Assert.DoesNotContain("AssetsOverviewBaseDisksSummaryTextBlockControl", overviewCodeBehindSource);
        Assert.DoesNotContain("AssetsOverviewSwitchesSummaryTextBlockControl", overviewCodeBehindSource);
        Assert.DoesNotContain("SetBaseDisksSummary(string text)", overviewCodeBehindSource);
        Assert.DoesNotContain("SetSwitchesSummary(string text)", overviewCodeBehindSource);

        Assert.Contains("_view.OpenBaseDisksRequested += OpenBaseDisksRequested;", overviewCompositionSource);
        Assert.Contains("_view.OpenSwitchesRequested += OpenSwitchesRequested;", overviewCompositionSource);
        Assert.Contains("_view.UpdateSummary(_workspace.BaseDisksSummaryText, _workspace.SwitchesSummaryText);", overviewCompositionSource);
        Assert.DoesNotContain("AssetsOverviewOpenBaseDisksButtonControl", overviewCompositionSource);
        Assert.DoesNotContain("AssetsOverviewOpenSwitchesButtonControl", overviewCompositionSource);
        Assert.DoesNotContain("SetBaseDisksSummary", overviewCompositionSource);
        Assert.DoesNotContain("SetSwitchesSummary", overviewCompositionSource);
    }

    [Fact]
    public void AssetsBaseDisksView_ExposesNarrowInteractionSurface_InsteadOfControlBagAccess()
    {
        var baseDisksXaml = LoadAssetsBaseDisksViewXaml();
        var baseDisksViewSource = LoadAssetsBaseDisksViewCodeBehindSource();
        var baseDisksCompositionSource = LoadAssetsBaseDisksWorkspaceCompositionSource();

        Assert.NotNull(FindByName(baseDisksXaml, "AssetsBaseDisksListView"));
        Assert.NotNull(FindByName(baseDisksXaml, "AssetsBaseDisksRefreshButton"));
        Assert.NotNull(FindByName(baseDisksXaml, "AssetsBaseDisksImportButton"));
        Assert.NotNull(FindByName(baseDisksXaml, "AssetsBaseDisksSaveMetadataButton"));

        Assert.Contains("public event EventHandler? RefreshRequested;", baseDisksViewSource);
        Assert.Contains("public event EventHandler? ImportRequested;", baseDisksViewSource);
        Assert.Contains("public event EventHandler? ValidateRequested;", baseDisksViewSource);
        Assert.Contains("public event EventHandler? SaveMetadataRequested;", baseDisksViewSource);
        Assert.Contains("public event EventHandler? RemoveRequested;", baseDisksViewSource);
        Assert.Contains("public event EventHandler? BrowsePathRequested;", baseDisksViewSource);
        Assert.Contains("public event EventHandler? SelectedBaseDiskChanged;", baseDisksViewSource);
        Assert.Contains("public event EventHandler? MetadataChanged;", baseDisksViewSource);
        Assert.Contains("public AssetsBaseDiskListRow? SelectedBaseDisk =>", baseDisksViewSource);
        Assert.Contains("public void SetInventorySource(object? itemsSource)", baseDisksViewSource);
        Assert.Contains("public AssetsBaseDiskFormValues CaptureFormValues()", baseDisksViewSource);
        Assert.Contains("public void ApplyEditorDraft(AssetsBaseDiskDraft draft)", baseDisksViewSource);
        Assert.Contains("public void UpdateWorkspaceState(AssetsBaseDisksViewState state)", baseDisksViewSource);
        Assert.DoesNotContain("public Button", baseDisksViewSource);
        Assert.DoesNotContain("public TextBox", baseDisksViewSource);
        Assert.DoesNotContain("public ListView", baseDisksViewSource);
        Assert.DoesNotContain("public Border", baseDisksViewSource);
        Assert.DoesNotContain("AssetsBaseDisksRefreshButtonControl", baseDisksViewSource);
        Assert.DoesNotContain("AssetsBaseDisksListViewControl", baseDisksViewSource);

        Assert.Contains("_view.RefreshRequested += AssetsBaseDisksRefreshRequested;", baseDisksCompositionSource);
        Assert.Contains("_view.ImportRequested += AssetsBaseDisksImportRequested;", baseDisksCompositionSource);
        Assert.Contains("_view.ValidateRequested += AssetsBaseDisksValidateRequested;", baseDisksCompositionSource);
        Assert.Contains("_view.SaveMetadataRequested += AssetsBaseDisksSaveMetadataRequested;", baseDisksCompositionSource);
        Assert.Contains("_view.RemoveRequested += AssetsBaseDisksRemoveRequested;", baseDisksCompositionSource);
        Assert.Contains("_view.BrowsePathRequested += AssetsBaseDisksBrowsePathRequested;", baseDisksCompositionSource);
        Assert.Contains("_view.SelectedBaseDiskChanged += AssetsBaseDisksListView_SelectionChanged;", baseDisksCompositionSource);
        Assert.Contains("_view.MetadataChanged += AssetsBaseDisksMetadataChanged;", baseDisksCompositionSource);
        Assert.Contains("_view.UpdateWorkspaceState(BuildViewState(workspace, canSaveDraft));", baseDisksCompositionSource);
        Assert.DoesNotContain("AssetsBaseDisksRefreshButtonControl", baseDisksCompositionSource);
        Assert.DoesNotContain("AssetsBaseDisksListViewControl", baseDisksCompositionSource);
        Assert.DoesNotContain("AssetsBaseDisksOsNameTextBoxControl", baseDisksCompositionSource);
    }

    [Fact]
    public void AssetsSwitchesView_ExposesNarrowInteractionSurface_InsteadOfControlBagAccess()
    {
        var switchesXaml = LoadAssetsSwitchesViewXaml();
        var switchesXamlSource = LoadAssetsSwitchesViewXamlSource();
        var switchesViewSource = LoadAssetsSwitchesViewCodeBehindSource();
        var switchesCompositionSource = LoadAssetsSwitchesWorkspaceCompositionSource();

        Assert.NotNull(FindByName(switchesXaml, "AssetsSwitchesListView"));
        Assert.NotNull(FindByName(switchesXaml, "AssetsSwitchesRefreshButton"));
        Assert.NotNull(FindByName(switchesXaml, "AssetsSwitchesCreateButton"));
        Assert.NotNull(FindByName(switchesXaml, "AssetsSwitchesApplyButton"));
        Assert.NotNull(FindByName(switchesXaml, "AssetsSwitchesDeleteButton"));

        Assert.Contains("internal sealed class AssetsSwitchesViewPresentationModel : INotifyPropertyChanged", switchesViewSource);
        Assert.Contains("private readonly AssetsSwitchesViewPresentationModel _presentation = new();", switchesViewSource);
        Assert.Contains("DataContext = _presentation;", switchesViewSource);
        Assert.Contains("public event EventHandler? RefreshRequested;", switchesViewSource);
        Assert.Contains("public event EventHandler? CreateRequested;", switchesViewSource);
        Assert.Contains("public event EventHandler? ApplyRequested;", switchesViewSource);
        Assert.Contains("public event EventHandler? DeleteRequested;", switchesViewSource);
        Assert.Contains("public event EventHandler? SelectedSwitchChanged;", switchesViewSource);
        Assert.Contains("public event EventHandler? EditorChanged;", switchesViewSource);
        Assert.Contains("public AssetsSwitchListRow? SelectedSwitch =>", switchesViewSource);
        Assert.Contains("public void SetInventorySource(object? itemsSource)", switchesViewSource);
        Assert.Contains("public void SetAttachedVmSource(object? itemsSource)", switchesViewSource);
        Assert.Contains("public AssetsSwitchesFormValues CaptureFormValues()", switchesViewSource);
        Assert.Contains("public void ApplyEditorDraft(AssetsSwitchDraft draft)", switchesViewSource);
        Assert.Contains("public void UpdateWorkspaceState(AssetsSwitchesViewState state)", switchesViewSource);
        Assert.Contains("_presentation.Apply(state);", switchesViewSource);
        Assert.DoesNotContain("public Button", switchesViewSource);
        Assert.DoesNotContain("public TextBox", switchesViewSource);
        Assert.DoesNotContain("public ListView", switchesViewSource);
        Assert.DoesNotContain("public Border", switchesViewSource);
        Assert.DoesNotContain("AssetsSwitchesRefreshButtonControl", switchesViewSource);
        Assert.DoesNotContain("AssetsSwitchesRefreshButton.IsEnabled =", switchesViewSource);
        Assert.DoesNotContain("AssetsSwitchesStatusTextBlock.Text =", switchesViewSource);
        Assert.DoesNotContain("AssetsSwitchesLoadingStatePanel.Visibility =", switchesViewSource);

        Assert.Contains("IsEnabled=\"{Binding CanRefresh, Mode=OneWay}\"", switchesXamlSource);
        Assert.Contains("Text=\"{Binding StatusText, Mode=OneWay}\"", switchesXamlSource);
        Assert.Contains("Visibility=\"{Binding StatusVisibility, Mode=OneWay}\"", switchesXamlSource);
        Assert.Contains("Text=\"{Binding ErrorStateText, Mode=OneWay}\"", switchesXamlSource);

        Assert.Contains("_view.SelectedSwitchChanged += AssetsSwitchesListView_SelectionChanged;", switchesCompositionSource);
        Assert.Contains("_view.RefreshRequested += AssetsSwitchesRefreshRequested;", switchesCompositionSource);
        Assert.Contains("_view.CreateRequested += AssetsSwitchesCreateRequested;", switchesCompositionSource);
        Assert.Contains("_view.ApplyRequested += AssetsSwitchesApplyRequested;", switchesCompositionSource);
        Assert.Contains("_view.DeleteRequested += AssetsSwitchesDeleteRequested;", switchesCompositionSource);
        Assert.Contains("_view.EditorChanged += AssetsSwitchesEditorChanged;", switchesCompositionSource);
        Assert.Contains("_view.UpdateWorkspaceState(BuildViewState(workspace, canValidateOrApply, isExternalSwitchTypeSelected));", switchesCompositionSource);
    }

    [Fact]
    public void AssetsSwitches_ControllerViewModelAndComposition_ProtectRefinedLocalArchitecture()
    {
        var workspaceSource = LoadAssetsSwitchesWorkspaceViewModelSource();
        var controllerSource = LoadAssetsSwitchesWorkspaceControllerSource();
        var compositionSource = LoadAssetsSwitchesWorkspaceCompositionSource();
        var viewSource = LoadAssetsSwitchesViewCodeBehindSource();

        Assert.Contains("internal sealed class AssetsSwitchesWorkspaceViewModel", workspaceSource);
        Assert.Contains("public ObservableCollection<AssetsSwitchListRow> Inventory { get; } = [];", workspaceSource);
        Assert.Contains("public ObservableCollection<string> AttachedVmNames { get; } = [];", workspaceSource);
        Assert.Contains("public AssetsSwitchListRow? SelectedRow { get; set; }", workspaceSource);
        Assert.Contains("public AssetsSwitchDraft? PendingDraft { get; set; }", workspaceSource);
        Assert.Contains("public bool IsLoading { get; set; }", workspaceSource);
        Assert.Contains("public bool IsSaving { get; set; }", workspaceSource);
        Assert.Contains("public bool IsDeleting { get; set; }", workspaceSource);
        Assert.Contains("public bool IsUpdatingEditor { get; set; }", workspaceSource);
        Assert.Contains("public bool HasErrorState { get; set; }", workspaceSource);
        Assert.Contains("public int ValidationRequestVersion { get; set; }", workspaceSource);
        Assert.Contains("public int AssessmentRequestVersion { get; set; }", workspaceSource);
        Assert.Contains("public string StatusText { get; set; } = \"Select a virtual switch or click New to begin.\";", workspaceSource);
        Assert.Contains("public string SelectedSwitchValidationText { get; set; } = \"Select a switch or click New to begin.\";", workspaceSource);

        Assert.Contains("internal sealed class AssetsSwitchesWorkspaceController", controllerSource);
        Assert.Contains("private readonly IAssetsSwitchesCapabilityService _capabilityService;", controllerSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceViewModel _workspace;", controllerSource);
        Assert.Contains("private readonly IAssetsSwitchesWorkspaceHost _host;", controllerSource);
        Assert.Contains("public async Task EnsureInventoryAsync(bool forceRefresh)", controllerSource);
        Assert.Contains("public async Task HandleSelectionChangedAsync(AssetsSwitchListRow? selectedRow)", controllerSource);
        Assert.Contains("public async Task BeginCreateAsync()", controllerSource);
        Assert.Contains("public async Task SaveDraftAsync()", controllerSource);
        Assert.Contains("public async Task DeleteSelectedAsync()", controllerSource);
        Assert.Contains("public async Task HandleEditorChangedAsync()", controllerSource);
        Assert.Contains("public void ApplyWorkspaceState()", controllerSource);
        Assert.Contains("LoadAttachedVmNamesAsync", controllerSource);
        Assert.Contains("RefreshValidationAsync", controllerSource);
        Assert.Contains("ApplyDeleteAssessment", controllerSource);
        Assert.DoesNotContain("internal sealed class AssetsSwitchesWorkspaceHost", controllerSource);

        Assert.Contains("internal sealed class AssetsSwitchesWorkspaceComposition : IAssetsSwitchesWorkspaceHost", compositionSource);
        Assert.Contains("private readonly AssetsSwitchesView _view;", compositionSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceViewModel _workspace = new();", compositionSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceController _controller;", compositionSource);
        Assert.Contains("private readonly IAssetsSwitchesCompositionHost _host;", compositionSource);
        Assert.Contains("_controller = new AssetsSwitchesWorkspaceController(capabilityService, _workspace, this);", compositionSource);
        Assert.Contains("_view.SetInventorySource(_workspace.Inventory);", compositionSource);
        Assert.Contains("_view.SetAttachedVmSource(_workspace.AttachedVmNames);", compositionSource);
        Assert.Contains("public bool IsLoading => _workspace.IsLoading;", compositionSource);
        Assert.Contains("public int InventoryCount => _workspace.Inventory.Count;", compositionSource);
        Assert.Contains("public Task EnsureInventoryAsync(bool forceRefresh)", compositionSource);
        Assert.Contains("public void ApplyShellState()", compositionSource);

        Assert.Contains("internal sealed class AssetsSwitchesViewPresentationModel : INotifyPropertyChanged", viewSource);
        Assert.Contains("public void Apply(AssetsSwitchesViewState state)", viewSource);
        Assert.DoesNotContain("public Button", viewSource);
        Assert.DoesNotContain("public TextBox", viewSource);
        Assert.DoesNotContain("public ListView", viewSource);
    }

    [Fact]
    public void AssetsShellBridge_RemainsNarrowAndShellOwned()
    {
        var compositionSource = LoadAssetsWorkspaceCompositionSource();
        var baseDisksCompositionSource = LoadAssetsBaseDisksWorkspaceCompositionSource();
        var controllerSource = LoadAssetsBaseDisksWorkspaceControllerSource();
        var switchesCompositionSource = LoadAssetsSwitchesWorkspaceCompositionSource();
        var overviewCompositionSource = LoadAssetsOverviewWorkspaceCompositionSource();
        var shellBridgeInterfaceBlock = ExtractSection(
            compositionSource,
            "internal interface IAssetsWorkspaceShellBridge",
            "internal interface IAssetsWorkspaceHost");
        var shellBridgeClassBlock = ExtractSection(
            compositionSource,
            "internal sealed class AssetsWorkspaceShellBridge",
            "internal sealed class AssetsWorkspaceHost");
        var hostInterfaceBlock = ExtractSection(
            compositionSource,
            "internal interface IAssetsWorkspaceHost",
            "internal sealed class AssetsWorkspaceShellBridge");
        var hostClassBlock = ExtractSection(
            compositionSource,
            "internal sealed class AssetsWorkspaceHost",
            "internal sealed class AssetsWorkspaceComposition");

        Assert.Contains("bool IsAssetsCapabilityActive { get; }", shellBridgeInterfaceBlock);
        Assert.Contains("bool IsAssetsOverviewActive { get; }", shellBridgeInterfaceBlock);
        Assert.Contains("bool IsAssetsBaseDisksActive { get; }", shellBridgeInterfaceBlock);
        Assert.Contains("bool IsAssetsSwitchesActive { get; }", shellBridgeInterfaceBlock);
        Assert.Contains("void NavigateToRoute(string routeKey);", shellBridgeInterfaceBlock);
        Assert.DoesNotContain("EnsureAssetsBaseDisksAsync", shellBridgeInterfaceBlock);
        Assert.DoesNotContain("EnsureAssetsSwitchesAsync", shellBridgeInterfaceBlock);
        Assert.DoesNotContain("UpdateAssetsOverviewUi", shellBridgeInterfaceBlock);
        Assert.DoesNotContain("UpdateAssetsBaseDisksUi", shellBridgeInterfaceBlock);
        Assert.DoesNotContain("UpdateAssetsSwitchesUi", shellBridgeInterfaceBlock);

        Assert.Contains("internal sealed class AssetsWorkspaceShellBridge : IAssetsWorkspaceShellBridge", shellBridgeClassBlock);
        Assert.Contains("public bool IsAssetsCapabilityActive =>", shellBridgeClassBlock);
        Assert.Contains("public bool IsAssetsOverviewActive =>", shellBridgeClassBlock);
        Assert.Contains("public bool IsAssetsBaseDisksActive =>", shellBridgeClassBlock);
        Assert.Contains("public bool IsAssetsSwitchesActive =>", shellBridgeClassBlock);
        Assert.Contains("public void NavigateToRoute(string routeKey) =>", shellBridgeClassBlock);
        Assert.DoesNotContain("_ensureAssetsBaseDisksAsync", shellBridgeClassBlock);
        Assert.DoesNotContain("_updateAssetsOverviewUi", shellBridgeClassBlock);

        Assert.DoesNotContain("UpdateAssetsOverviewUi", hostInterfaceBlock);
        Assert.DoesNotContain("IsAssetsSwitchesLoading", hostInterfaceBlock);
        Assert.DoesNotContain("AssetsSwitchCount", hostInterfaceBlock);
        Assert.DoesNotContain("EnsureAssetsSwitchesAsync", hostInterfaceBlock);
        Assert.DoesNotContain("UpdateAssetsSwitchesUi", hostInterfaceBlock);
        Assert.DoesNotContain("Task EnsureAssetsBaseDisksAsync(bool forceRefresh);", hostInterfaceBlock);
        Assert.DoesNotContain("void UpdateAssetsBaseDisksUi();", hostInterfaceBlock);
        Assert.DoesNotContain("bool IsAssetsBaseDisksLoading { get; }", hostInterfaceBlock);
        Assert.DoesNotContain("int AssetsBaseDiskCount { get; }", hostInterfaceBlock);
        Assert.DoesNotContain("Task", hostInterfaceBlock);

        Assert.Contains("internal sealed class AssetsWorkspaceHost : IAssetsWorkspaceHost", hostClassBlock);
        Assert.DoesNotContain("_updateAssetsOverviewUi", hostClassBlock);
        Assert.DoesNotContain("public bool IsAssetsSwitchesLoading =>", hostClassBlock);
        Assert.DoesNotContain("public int AssetsSwitchCount =>", hostClassBlock);
        Assert.DoesNotContain("public Task EnsureAssetsSwitchesAsync(bool forceRefresh) =>", hostClassBlock);
        Assert.DoesNotContain("public void UpdateAssetsSwitchesUi() =>", hostClassBlock);
        Assert.DoesNotContain("public Task EnsureAssetsBaseDisksAsync(bool forceRefresh) =>", hostClassBlock);
        Assert.DoesNotContain("public void UpdateAssetsBaseDisksUi() =>", hostClassBlock);
        Assert.DoesNotContain("public bool IsAssetsBaseDisksLoading =>", hostClassBlock);
        Assert.DoesNotContain("public int AssetsBaseDiskCount =>", hostClassBlock);
        Assert.DoesNotContain("public Task", hostClassBlock);

        var baseDisksCompositionHostInterfaceBlock = ExtractSection(
            baseDisksCompositionSource,
            "internal interface IAssetsBaseDisksCompositionHost",
            "internal sealed class AssetsBaseDisksCompositionHost");
        var baseDisksCompositionHostClassBlock = ExtractSection(
            baseDisksCompositionSource,
            "internal sealed class AssetsBaseDisksCompositionHost",
            "internal sealed class AssetsBaseDisksWorkspaceComposition");

        Assert.Contains("string? PickBaseDiskFilePath();", baseDisksCompositionHostInterfaceBlock);
        Assert.Contains("Task<bool> ShowRemoveConfirmationDialogAsync", baseDisksCompositionHostInterfaceBlock);
        Assert.DoesNotContain("CaptureDraft", baseDisksCompositionHostInterfaceBlock);
        Assert.DoesNotContain("ApplyWorkspaceState", baseDisksCompositionHostInterfaceBlock);

        Assert.Contains("internal sealed class AssetsBaseDisksCompositionHost : IAssetsBaseDisksCompositionHost", baseDisksCompositionHostClassBlock);
        Assert.Contains("public string? PickBaseDiskFilePath() =>", baseDisksCompositionHostClassBlock);
        Assert.Contains("public Task<bool> ShowRemoveConfirmationDialogAsync", baseDisksCompositionHostClassBlock);

        var baseDisksHostInterfaceBlock = ExtractSection(
            controllerSource,
            "internal interface IAssetsBaseDisksWorkspaceHost",
            "internal sealed class AssetsBaseDisksWorkspaceController");

        Assert.Contains("AssetsBaseDiskDraft? CaptureDraft(bool isNewOverride);", baseDisksHostInterfaceBlock);
        Assert.Contains("void ApplyEditorDraft(AssetsBaseDiskDraft draft);", baseDisksHostInterfaceBlock);
        Assert.Contains("void ClearEditorFields();", baseDisksHostInterfaceBlock);
        Assert.Contains("void SetSelectedRow(AssetsBaseDiskListRow? row);", baseDisksHostInterfaceBlock);
        Assert.Contains("void ApplyWorkspaceState(AssetsBaseDisksWorkspaceViewModel workspace, bool canSaveDraft);", baseDisksHostInterfaceBlock);
        Assert.Contains("string? PickBaseDiskFilePath();", baseDisksHostInterfaceBlock);
        Assert.Contains("void SetDraftPath(string path);", baseDisksHostInterfaceBlock);
        Assert.Contains("Task<bool> ShowRemoveConfirmationDialogAsync", baseDisksHostInterfaceBlock);
        Assert.DoesNotContain("NavigateToRoute", baseDisksHostInterfaceBlock);
        Assert.DoesNotContain("internal sealed class AssetsBaseDisksWorkspaceHost", controllerSource);

        var overviewShellBridgeInterfaceBlock = ExtractSection(
            overviewCompositionSource,
            "internal interface IAssetsOverviewWorkspaceShellBridge",
            "internal interface IAssetsOverviewWorkspaceHost");
        var overviewHostInterfaceBlock = ExtractSection(
            overviewCompositionSource,
            "internal interface IAssetsOverviewWorkspaceHost",
            "internal sealed class AssetsOverviewWorkspaceShellBridge");
        var overviewShellBridgeClassBlock = ExtractSection(
            overviewCompositionSource,
            "internal sealed class AssetsOverviewWorkspaceShellBridge",
            "internal sealed class AssetsOverviewWorkspaceHost");
        var overviewHostClassBlock = ExtractSection(
            overviewCompositionSource,
            "internal sealed class AssetsOverviewWorkspaceHost",
            "internal sealed class AssetsOverviewWorkspaceComposition");

        Assert.Contains("bool IsAssetsOverviewActive { get; }", overviewShellBridgeInterfaceBlock);
        Assert.Contains("void NavigateToRoute(string routeKey);", overviewShellBridgeInterfaceBlock);
        Assert.DoesNotContain("IsAssetsCapabilityActive", overviewShellBridgeInterfaceBlock);
        Assert.DoesNotContain("EnsureAssetsBaseDisksAsync", overviewShellBridgeInterfaceBlock);

        Assert.Contains("bool IsAssetsBaseDisksLoading { get; }", overviewHostInterfaceBlock);
        Assert.Contains("bool IsAssetsSwitchesLoading { get; }", overviewHostInterfaceBlock);
        Assert.Contains("int AssetsBaseDiskCount { get; }", overviewHostInterfaceBlock);
        Assert.Contains("int AssetsSwitchCount { get; }", overviewHostInterfaceBlock);
        Assert.DoesNotContain("UpdateAssetsBaseDisksUi", overviewHostInterfaceBlock);
        Assert.DoesNotContain("UpdateAssetsSwitchesUi", overviewHostInterfaceBlock);

        Assert.Contains("internal sealed class AssetsOverviewWorkspaceShellBridge : IAssetsOverviewWorkspaceShellBridge", overviewShellBridgeClassBlock);
        Assert.Contains("public bool IsAssetsOverviewActive =>", overviewShellBridgeClassBlock);
        Assert.Contains("public void NavigateToRoute(string routeKey) =>", overviewShellBridgeClassBlock);

        Assert.Contains("internal sealed class AssetsOverviewWorkspaceHost : IAssetsOverviewWorkspaceHost", overviewHostClassBlock);
        Assert.Contains("public bool IsAssetsBaseDisksLoading =>", overviewHostClassBlock);
        Assert.Contains("public bool IsAssetsSwitchesLoading =>", overviewHostClassBlock);
        Assert.Contains("public int AssetsBaseDiskCount =>", overviewHostClassBlock);
        Assert.Contains("public int AssetsSwitchCount =>", overviewHostClassBlock);

        var switchesCompositionHostInterfaceBlock = ExtractSection(
            switchesCompositionSource,
            "internal interface IAssetsSwitchesCompositionHost",
            "internal sealed class AssetsSwitchesCompositionHost");
        var switchesCompositionHostClassBlock = ExtractSection(
            switchesCompositionSource,
            "internal sealed class AssetsSwitchesCompositionHost",
            "internal sealed class AssetsSwitchesWorkspaceComposition");
        var switchesHostInterfaceBlock = ExtractSection(
            LoadAssetsSwitchesWorkspaceControllerSource(),
            "internal interface IAssetsSwitchesWorkspaceHost",
            "internal sealed class AssetsSwitchesWorkspaceController");

        Assert.Contains("Task<bool> ShowDeleteConfirmationDialogAsync", switchesCompositionHostInterfaceBlock);
        Assert.DoesNotContain("CaptureDraft", switchesCompositionHostInterfaceBlock);
        Assert.DoesNotContain("ApplyWorkspaceState", switchesCompositionHostInterfaceBlock);

        Assert.Contains("internal sealed class AssetsSwitchesCompositionHost : IAssetsSwitchesCompositionHost", switchesCompositionHostClassBlock);
        Assert.Contains("public Task<bool> ShowDeleteConfirmationDialogAsync", switchesCompositionHostClassBlock);

        Assert.Contains("AssetsSwitchDraft? CaptureDraft(bool isNewOverride);", switchesHostInterfaceBlock);
        Assert.Contains("string GetSelectedSwitchType();", switchesHostInterfaceBlock);
        Assert.Contains("void ApplyEditorDraft(AssetsSwitchDraft draft);", switchesHostInterfaceBlock);
        Assert.Contains("void ClearEditorFields();", switchesHostInterfaceBlock);
        Assert.Contains("void SetSelectedRow(AssetsSwitchListRow? row);", switchesHostInterfaceBlock);
        Assert.Contains("void ApplyWorkspaceState(AssetsSwitchesWorkspaceViewModel workspace, bool canValidateOrApply, bool isExternalSwitchTypeSelected);", switchesHostInterfaceBlock);
        Assert.Contains("Task<bool> ShowDeleteConfirmationDialogAsync", switchesHostInterfaceBlock);
        Assert.DoesNotContain("NavigateToRoute", switchesHostInterfaceBlock);
        Assert.DoesNotContain("internal sealed class AssetsSwitchesWorkspaceHost", LoadAssetsSwitchesWorkspaceControllerSource());

        Assert.Contains("internal sealed class AssetsSwitchesWorkspaceComposition : IAssetsSwitchesWorkspaceHost", switchesCompositionSource);
        Assert.Contains("private readonly AssetsSwitchesView _view;", switchesCompositionSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceViewModel _workspace = new();", switchesCompositionSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceController _controller;", switchesCompositionSource);
        Assert.Contains("private readonly IAssetsSwitchesCompositionHost _host;", switchesCompositionSource);
        Assert.Contains("_controller = new AssetsSwitchesWorkspaceController(capabilityService, _workspace, this);", switchesCompositionSource);
        Assert.Contains("_view.SetInventorySource(_workspace.Inventory);", switchesCompositionSource);
        Assert.Contains("_view.SetAttachedVmSource(_workspace.AttachedVmNames);", switchesCompositionSource);
        Assert.Contains("_view.SelectedSwitchChanged += AssetsSwitchesListView_SelectionChanged;", switchesCompositionSource);
        Assert.Contains("_view.RefreshRequested += AssetsSwitchesRefreshRequested;", switchesCompositionSource);
        Assert.Contains("_view.CreateRequested += AssetsSwitchesCreateRequested;", switchesCompositionSource);
        Assert.Contains("_view.ApplyRequested += AssetsSwitchesApplyRequested;", switchesCompositionSource);
        Assert.Contains("_view.DeleteRequested += AssetsSwitchesDeleteRequested;", switchesCompositionSource);
        Assert.Contains("_view.EditorChanged += AssetsSwitchesEditorChanged;", switchesCompositionSource);
        Assert.Contains("public bool IsLoading => _workspace.IsLoading;", switchesCompositionSource);
        Assert.Contains("public int InventoryCount => _workspace.Inventory.Count;", switchesCompositionSource);
        Assert.Contains("public void ApplyShellState()", switchesCompositionSource);
        Assert.Contains("public Task EnsureInventoryAsync(bool forceRefresh)", switchesCompositionSource);
    }

    [Fact]
    public void AssetsExtraction_ClosureMatrix_ProtectsFullSharedAndLaneArchitecture()
    {
        var mainWindowSource = LoadMainWindowSource();
        var shellViewModelSource = LoadShellViewModelSource();
        var sharedCompositionSource = LoadAssetsWorkspaceCompositionSource();
        var overviewCompositionSource = LoadAssetsOverviewWorkspaceCompositionSource();
        var overviewWorkspaceSource = LoadAssetsOverviewWorkspaceViewModelSource();
        var baseDisksCompositionSource = LoadAssetsBaseDisksWorkspaceCompositionSource();
        var baseDisksControllerSource = LoadAssetsBaseDisksWorkspaceControllerSource();
        var baseDisksWorkspaceSource = LoadAssetsBaseDisksWorkspaceViewModelSource();
        var baseDisksViewSource = LoadAssetsBaseDisksViewCodeBehindSource();
        var switchesCompositionSource = LoadAssetsSwitchesWorkspaceCompositionSource();
        var switchesControllerSource = LoadAssetsSwitchesWorkspaceControllerSource();
        var switchesWorkspaceSource = LoadAssetsSwitchesWorkspaceViewModelSource();
        var switchesViewSource = LoadAssetsSwitchesViewCodeBehindSource();

        Assert.Contains("private readonly AssetsWorkspaceComposition _assetsWorkspaceComposition;", mainWindowSource);
        Assert.Contains("private readonly AssetsBaseDisksWorkspaceComposition _assetsBaseDisksWorkspaceComposition;", mainWindowSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceComposition _assetsSwitchesWorkspaceComposition;", mainWindowSource);
        Assert.Contains("_assetsWorkspaceComposition = new AssetsWorkspaceComposition(", mainWindowSource);
        Assert.Contains("_assetsBaseDisksWorkspaceComposition = new AssetsBaseDisksWorkspaceComposition(", mainWindowSource);
        Assert.Contains("_assetsSwitchesWorkspaceComposition = new AssetsSwitchesWorkspaceComposition(", mainWindowSource);
        Assert.Contains("_assetsWorkspaceComposition.ApplyShellState();", mainWindowSource);
        Assert.Contains("await _assetsBaseDisksWorkspaceComposition.EnsureInventoryAsync(forceRefresh: true);", mainWindowSource);
        Assert.DoesNotContain("private readonly AssetsBaseDisksWorkspaceViewModel _assetsBaseDisksWorkspace = new();", mainWindowSource);
        Assert.DoesNotContain("private readonly AssetsSwitchesWorkspaceViewModel _assetsSwitchesWorkspace = new();", mainWindowSource);
        Assert.DoesNotContain("private readonly AssetsSwitchesWorkspaceController _assetsSwitchesController;", mainWindowSource);

        Assert.Contains("public const string AssetsOverview = \"assets.overview\";", shellViewModelSource);
        Assert.Contains("public const string AssetsBaseDisks = \"assets.base_disks\";", shellViewModelSource);
        Assert.Contains("public const string AssetsSwitches = \"assets.switches\";", shellViewModelSource);

        Assert.Contains("internal sealed class AssetsWorkspaceComposition", sharedCompositionSource);
        Assert.Contains("private readonly AssetsOverviewWorkspaceComposition _overviewWorkspaceComposition;", sharedCompositionSource);
        Assert.Contains("private readonly AssetsBaseDisksWorkspaceComposition _baseDisksWorkspaceComposition;", sharedCompositionSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceComposition _switchesWorkspaceComposition;", sharedCompositionSource);
        Assert.Contains("private readonly IAssetsWorkspaceHost _host;", sharedCompositionSource);
        Assert.Contains("private readonly IAssetsWorkspaceShellBridge _shellBridge;", sharedCompositionSource);
        Assert.Contains("_overviewWorkspaceComposition.ApplyShellState();", sharedCompositionSource);
        Assert.Contains("_baseDisksWorkspaceComposition.ApplyShellState();", sharedCompositionSource);
        Assert.Contains("_switchesWorkspaceComposition.ApplyShellState();", sharedCompositionSource);
        Assert.DoesNotContain("LoadAsync(isRefresh: forceRefresh)", sharedCompositionSource);
        Assert.DoesNotContain("ValidateAsync(draft)", sharedCompositionSource);
        Assert.DoesNotContain("RemoveAsync(", sharedCompositionSource);

        Assert.Contains("internal sealed class AssetsOverviewWorkspaceComposition", overviewCompositionSource);
        Assert.Contains("private readonly AssetsOverviewWorkspaceViewModel _workspace = new();", overviewCompositionSource);
        Assert.Contains("private readonly IAssetsOverviewWorkspaceHost _host;", overviewCompositionSource);
        Assert.Contains("private readonly IAssetsOverviewWorkspaceShellBridge _shellBridge;", overviewCompositionSource);
        Assert.Contains("RefreshSummary();", overviewCompositionSource);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.AssetsBaseDisks);", overviewCompositionSource);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.AssetsSwitches);", overviewCompositionSource);

        Assert.Contains("internal sealed class AssetsOverviewWorkspaceViewModel", overviewWorkspaceSource);
        Assert.Contains("public void RefreshSummary(bool isBaseDisksLoading, bool isSwitchesLoading, int assetsBaseDiskCount, int assetsSwitchCount)", overviewWorkspaceSource);

        Assert.Contains("internal sealed class AssetsBaseDisksWorkspaceViewModel", baseDisksWorkspaceSource);
        Assert.Contains("public ObservableCollection<AssetsBaseDiskListRow> Inventory { get; } = [];", baseDisksWorkspaceSource);
        Assert.Contains("public AssetsBaseDiskDraft? PendingDraft { get; set; }", baseDisksWorkspaceSource);
        Assert.Contains("internal sealed class AssetsBaseDisksWorkspaceController", baseDisksControllerSource);
        Assert.Contains("private readonly AssetsBaseDisksWorkspaceViewModel _workspace;", baseDisksControllerSource);
        Assert.Contains("private readonly IAssetsBaseDisksWorkspaceHost _host;", baseDisksControllerSource);
        Assert.Contains("public async Task EnsureInventoryAsync(bool forceRefresh)", baseDisksControllerSource);
        Assert.Contains("internal sealed class AssetsBaseDisksWorkspaceComposition : IAssetsBaseDisksWorkspaceHost", baseDisksCompositionSource);
        Assert.Contains("private readonly AssetsBaseDisksWorkspaceController _controller;", baseDisksCompositionSource);
        Assert.Contains("public void ApplyShellState()", baseDisksCompositionSource);
        Assert.Contains("_ = _controller.EnsureInventoryAsync(forceRefresh: false);", baseDisksCompositionSource);
        Assert.Contains("public void UpdateWorkspaceState(AssetsBaseDisksViewState state)", baseDisksViewSource);

        Assert.Contains("internal sealed class AssetsSwitchesWorkspaceViewModel", switchesWorkspaceSource);
        Assert.Contains("public ObservableCollection<AssetsSwitchListRow> Inventory { get; } = [];", switchesWorkspaceSource);
        Assert.Contains("public ObservableCollection<string> AttachedVmNames { get; } = [];", switchesWorkspaceSource);
        Assert.Contains("internal sealed class AssetsSwitchesWorkspaceController", switchesControllerSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceViewModel _workspace;", switchesControllerSource);
        Assert.Contains("private readonly IAssetsSwitchesWorkspaceHost _host;", switchesControllerSource);
        Assert.Contains("public async Task EnsureInventoryAsync(bool forceRefresh)", switchesControllerSource);
        Assert.Contains("internal sealed class AssetsSwitchesWorkspaceComposition : IAssetsSwitchesWorkspaceHost", switchesCompositionSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceController _controller;", switchesCompositionSource);
        Assert.Contains("public void ApplyShellState()", switchesCompositionSource);
        Assert.Contains("_ = _controller.EnsureInventoryAsync(forceRefresh: false);", switchesCompositionSource);
        Assert.Contains("internal sealed class AssetsSwitchesViewPresentationModel : INotifyPropertyChanged", switchesViewSource);
        Assert.Contains("public void UpdateWorkspaceState(AssetsSwitchesViewState state)", switchesViewSource);
    }

    [Fact]
    public void MainWindow_PreservesShellBoundary_WhileHostingLongLivedTemplatesWorkspace()
    {
        var mainWindowSource = LoadMainWindowSource();
        var shellViewModelSource = LoadShellViewModelSource();

        Assert.Contains("private readonly TemplatesWorkspaceComposition _templatesWorkspaceComposition;", mainWindowSource);
        Assert.Contains("_templatesWorkspaceComposition = new TemplatesWorkspaceComposition(", mainWindowSource);
        Assert.Contains("new TemplatesWorkspaceShellBridge(", mainWindowSource);
        Assert.Contains("() => IsTemplatesCapabilityActive,", mainWindowSource);
        Assert.Contains("() => IsTemplatesLibraryActive,", mainWindowSource);
        Assert.Contains("() => IsTemplatesEditorActive));", mainWindowSource);
        Assert.Contains("_templatesWorkspaceComposition.ApplyShellState();", mainWindowSource);
        Assert.Contains("ApplyTemplatesWorkspaceUiState();", mainWindowSource);
        Assert.Contains("private TemplatesWorkspaceUiState CreateTemplatesWorkspaceUiState()", mainWindowSource);
        Assert.Contains("private FrameworkElement TemplatesWorkspaceHost => TemplatesWorkspacePanel;", mainWindowSource);
        Assert.Contains("private IList<TemplateLibraryItem> TemplatesLibraryItems => _templatesWorkspaceComposition.LibraryItems;", mainWindowSource);
        Assert.Contains("new TemplatesEditorWorkspaceComposition(", mainWindowSource);
        Assert.Contains("new TemplatesEditorWorkspaceHost(", mainWindowSource);
        Assert.Contains("LoadTemplateEditorReferenceDataAsync,", mainWindowSource);
        Assert.Contains("await _templatesWorkspaceComposition.ShowEditorDocumentAsync(document, statusText);", mainWindowSource);
        Assert.Contains("() => NavigateToRoute(ShellRouteKeys.TemplatesLibrary)", mainWindowSource);
        Assert.Contains("private bool IsTemplatesLibraryActive =>", mainWindowSource);
        Assert.Contains("private bool IsTemplatesEditorActive =>", mainWindowSource);
        Assert.Contains("private bool IsTemplatesCapabilityActive =>", mainWindowSource);

        Assert.DoesNotContain("private readonly ObservableCollection<TemplateLibraryItem> _templateLibraryItems = [];", mainWindowSource);
        Assert.DoesNotContain("private TemplateLibraryItem? _selectedTemplateLibraryItem;", mainWindowSource);
        Assert.DoesNotContain("TemplatesWorkspaceHost.Visibility = IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.DoesNotContain("TemplatesLibraryViewHost.Visibility = IsTemplatesLibraryActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.DoesNotContain("TemplatesEditorViewHost.Visibility = IsTemplatesEditorActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.DoesNotContain("if (IsTemplatesLibraryActive)", mainWindowSource);
        Assert.DoesNotContain("_ = EnsureTemplatesLibraryAsync(forceRefresh: false);", mainWindowSource);
        Assert.DoesNotContain("private void UpdateTemplatesUi()", mainWindowSource);
        Assert.DoesNotContain("private TextBox TemplateNameTextBox =>", mainWindowSource);
        Assert.DoesNotContain("private TextBox TemplateDescriptionTextBox =>", mainWindowSource);
        Assert.DoesNotContain("private TextBlock TemplateEditorStatusTextBlock =>", mainWindowSource);
        Assert.DoesNotContain("private readonly ObservableCollection<VmTemplate> _templateVmEntries = [];", mainWindowSource);
        Assert.DoesNotContain("private VmTemplate? _selectedTemplateVmEntry;", mainWindowSource);
        Assert.DoesNotContain("private VmTemplate? SelectedTemplateVmEntry =>", mainWindowSource);
        Assert.DoesNotContain("TemplateVmListView.SelectionChanged += TemplateVmListView_SelectionChanged;", mainWindowSource);
        Assert.DoesNotContain("private void TemplateVmListView_SelectionChanged(", mainWindowSource);
        Assert.DoesNotContain("SaveTemplateButton.Click += SaveTemplateButton_Click;", mainWindowSource);
        Assert.DoesNotContain("SaveTemplateAsButton.Click += SaveTemplateAsButton_Click;", mainWindowSource);
        Assert.DoesNotContain("ValidateTemplateButton.Click += ValidateTemplateButton_Click;", mainWindowSource);
        Assert.DoesNotContain("BackToLibraryButton.Click += BackToLibraryButton_Click;", mainWindowSource);
        Assert.DoesNotContain("AddTemplateVmButton.Click += AddTemplateVmButton_Click;", mainWindowSource);
        Assert.DoesNotContain("RemoveTemplateVmButton.Click += RemoveTemplateVmButton_Click;", mainWindowSource);
        Assert.DoesNotContain("ApplyTemplateVmChangesButton.Click += ApplyTemplateVmChangesButton_Click;", mainWindowSource);
        Assert.DoesNotContain("WireTemplatesHandlers()", mainWindowSource);

        Assert.Contains("public const string TemplatesLibrary = \"templates.library\";", shellViewModelSource);
        Assert.Contains("public const string TemplatesEditor = \"templates.editor\";", shellViewModelSource);
    }

    [Fact]
    public void TemplatesLibrary_RefinedArchitecture_KeepsSharedCompositionAsDelegatingHost()
    {
        var compositionSource = LoadTemplatesWorkspaceCompositionSource();
        var libraryCompositionSource = LoadTemplatesLibraryWorkspaceCompositionSource();
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains("internal interface ITemplatesWorkspaceShellBridge", compositionSource);
        Assert.Contains("bool IsTemplatesCapabilityActive { get; }", compositionSource);
        Assert.Contains("bool IsTemplatesLibraryActive { get; }", compositionSource);
        Assert.Contains("bool IsTemplatesEditorActive { get; }", compositionSource);
        Assert.Contains("internal sealed class TemplatesWorkspaceShellBridge : ITemplatesWorkspaceShellBridge", compositionSource);
        Assert.Contains("internal sealed class TemplatesWorkspaceComposition", compositionSource);
        Assert.Contains("private readonly FrameworkElement _workspaceHost;", compositionSource);
        Assert.Contains("private readonly TemplatesLibraryWorkspaceComposition _libraryComposition;", compositionSource);
        Assert.Contains("private readonly TemplatesEditorWorkspaceComposition _editorComposition;", compositionSource);
        Assert.Contains("private readonly ITemplatesWorkspaceShellBridge _shellBridge;", compositionSource);
        Assert.Contains("_workspaceHost.Visibility = _shellBridge.IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;", compositionSource);
        Assert.Contains("_editorComposition.ApplyShellState(_shellBridge.IsTemplatesEditorActive);", compositionSource);
        Assert.Contains("_libraryComposition.ApplyShellState(_shellBridge.IsTemplatesLibraryActive);", compositionSource);
        Assert.Contains("public IList<TemplateLibraryItem> LibraryItems => _libraryComposition.LibraryItems;", compositionSource);
        Assert.Contains("public TemplateLibraryItem? SelectedLibraryItem => _libraryComposition.SelectedItem;", compositionSource);
        Assert.Contains("public Task EnsureLibraryAsync(bool forceRefresh) => _libraryComposition.EnsureLibraryAsync(forceRefresh);", compositionSource);
        Assert.Contains("public Task ShowEditorDocumentAsync(TemplateEditorDocument document, string statusText) => _editorComposition.ShowDocumentAsync(document, statusText);", compositionSource);
        Assert.Contains("public void SetEditorStatus(string statusText) => _editorComposition.SetStatus(statusText);", compositionSource);
        Assert.Contains("public TemplatesEditorDocumentHeaderInteractionState CaptureEditorDocumentHeaderState() => _editorComposition.CaptureDocumentHeaderState();", compositionSource);
        Assert.Contains("public IReadOnlyList<VmTemplate> EditorVmEntries => _editorComposition.VmEntries;", compositionSource);
        Assert.Contains("public VmTemplate? SelectedEditorVmEntry => _editorComposition.SelectedVmEntry;", compositionSource);
        Assert.Contains("public void ReplaceEditorVmEntries(IReadOnlyList<VmTemplate> vmEntries) => _editorComposition.ReplaceVmEntries(vmEntries);", compositionSource);
        Assert.Contains("public bool AddEditorVmEntry() => _editorComposition.AddVmEntry();", compositionSource);
        Assert.Contains("public Task RemoveSelectedEditorVmEntryAsync() => _editorComposition.RemoveSelectedVmEntryAsync();", compositionSource);
        Assert.Contains("public void RefreshEditorVmEntries() => _editorComposition.RefreshVmEntries();", compositionSource);
        Assert.Contains("_libraryComposition.ApplyUiState(state.IsLoading, state.HasSelectedLibraryItem);", compositionSource);
        Assert.Contains("internal readonly record struct TemplatesWorkspaceUiState(", compositionSource);
        Assert.Contains("public void ApplyUiState(TemplatesWorkspaceUiState state)", compositionSource);
        Assert.Contains("bool HasSelectedLibraryItem);", compositionSource);
        Assert.DoesNotContain("bool HasSelectedTemplateVmEntry", compositionSource);
        Assert.Contains("_editorComposition.RefreshUiState();", compositionSource);
        Assert.DoesNotContain("WireLibraryHandlers()", compositionSource);
        Assert.DoesNotContain("TemplatesLibraryView_OpenTemplateRequested", compositionSource);
        Assert.DoesNotContain("NavigateToRoute(ShellRouteKeys.TemplatesEditor);", compositionSource);
        Assert.DoesNotContain("TemplatesSubviewTabView", compositionSource);
        Assert.DoesNotContain("internal interface ITemplatesWorkspaceHost", compositionSource);
        Assert.DoesNotContain("internal sealed class TemplatesWorkspaceHost", compositionSource);
        Assert.DoesNotContain("TemplateLibraryListView.SelectedItem = _selectedTemplateLibraryItem;", compositionSource);
        Assert.DoesNotContain("private readonly TemplatesEditorView _editorView;", compositionSource);
        Assert.DoesNotContain("_editorView.TemplateEditorContextTextBlockControl.Text = state.TemplateEditorContextText;", compositionSource);
        Assert.Contains("new TemplatesLibraryWorkspaceComposition(", mainWindowSource);
        Assert.Contains("new TemplatesLibraryWorkspaceHost(", mainWindowSource);

        Assert.Contains("internal interface ITemplatesLibraryWorkspaceHost", libraryCompositionSource);
        Assert.Contains("internal sealed class TemplatesLibraryWorkspaceHost : ITemplatesLibraryWorkspaceHost", libraryCompositionSource);
        Assert.Contains("internal sealed class TemplatesLibraryWorkspaceComposition : ITemplatesLibraryWorkspaceControllerHost", libraryCompositionSource);
        Assert.Contains("private readonly TemplatesLibraryView _view;", libraryCompositionSource);
        Assert.Contains("private readonly ITemplatesLibraryWorkspaceHost _host;", libraryCompositionSource);
        Assert.Contains("private readonly TemplatesLibraryWorkspaceViewModel _workspace = new();", libraryCompositionSource);
        Assert.Contains("private readonly TemplatesLibraryWorkspaceController _controller;", libraryCompositionSource);
        Assert.Contains("_controller = new TemplatesLibraryWorkspaceController(templatesCapabilityService, _workspace, this);", libraryCompositionSource);
        Assert.Contains("_view.SetInventorySource(_workspace.Items);", libraryCompositionSource);
        Assert.Contains("WireHandlers();", libraryCompositionSource);
        Assert.Contains("public void ApplyShellState(bool isLibraryActive)", libraryCompositionSource);
        Assert.Contains("_view.Visibility = isLibraryActive ? Visibility.Visible : Visibility.Collapsed;", libraryCompositionSource);
        Assert.Contains("_ = _controller.EnsureLibraryAsync(forceRefresh: false);", libraryCompositionSource);
        Assert.Contains("public Task EnsureLibraryAsync(bool forceRefresh) => _controller.EnsureLibraryAsync(forceRefresh);", libraryCompositionSource);
        Assert.Contains("public void ApplyUiState(bool isLoading, bool hasSelectedLibraryItem)", libraryCompositionSource);
        Assert.Contains("void ITemplatesLibraryWorkspaceControllerHost.ApplyWorkspaceState()", libraryCompositionSource);
        Assert.Contains("_host.ApplyTemplatesWorkspaceUiState();", libraryCompositionSource);
    }

    [Fact]
    public void TemplatesLibrary_RefinedArchitecture_PreservesLocalStateControllerAndCompositionSeams()
    {
        var mainWindowSource = LoadMainWindowSource();
        var libraryWorkspaceSource = LoadTemplatesLibraryWorkspaceSource();
        var libraryControllerSource = LoadTemplatesLibraryWorkspaceControllerSource();
        var libraryCompositionSource = LoadTemplatesLibraryWorkspaceCompositionSource();

        Assert.Contains("internal sealed class TemplatesLibraryWorkspaceViewModel", libraryWorkspaceSource);
        Assert.Contains("public ObservableCollection<TemplateLibraryItem> Items { get; } = [];", libraryWorkspaceSource);
        Assert.Contains("public string SearchQuery { get; private set; } = string.Empty;", libraryWorkspaceSource);
        Assert.Contains("public string StatusText { get; private set; } = \"No templates loaded.\";", libraryWorkspaceSource);
        Assert.Contains("public TemplateLibraryItem? SelectedItem { get; private set; }", libraryWorkspaceSource);
        Assert.Contains("public string? SelectedTemplateFilePath { get; private set; }", libraryWorkspaceSource);
        Assert.Contains("public bool IsLoading { get; private set; }", libraryWorkspaceSource);
        Assert.Contains("public bool HasErrorState { get; private set; }", libraryWorkspaceSource);
        Assert.Contains("public void SetSearchQuery(string? searchQuery)", libraryWorkspaceSource);
        Assert.Contains("public void BeginLoading()", libraryWorkspaceSource);
        Assert.Contains("public void ApplyInventory(IReadOnlyList<TemplateLibraryItem> items, string statusText)", libraryWorkspaceSource);
        Assert.Contains("SelectedItem = string.IsNullOrWhiteSpace(SelectedTemplateFilePath)", libraryWorkspaceSource);
        Assert.Contains("public void SetSelectedItem(TemplateLibraryItem? selectedItem)", libraryWorkspaceSource);
        Assert.Contains("public void SetFailure(string statusText)", libraryWorkspaceSource);

        Assert.Contains("await _templatesWorkspaceComposition.EnsureLibraryAsync(forceRefresh: true);", mainWindowSource);
        Assert.DoesNotContain("TemplateLibraryListView.ItemsSource = _templateLibraryItems;", mainWindowSource);
        Assert.DoesNotContain("TemplatesLibraryView.SelectedTemplateChanged += TemplatesLibraryView_SelectedTemplateChanged;", mainWindowSource);
        Assert.DoesNotContain("ApplyTemplateSearchButton.Click += ApplyTemplateSearchButton_Click;", mainWindowSource);
        Assert.DoesNotContain("private ListView TemplateLibraryListView =>", mainWindowSource);
        Assert.DoesNotContain("private Button ApplyTemplateSearchButton =>", mainWindowSource);

        Assert.Contains("internal sealed class TemplatesLibraryWorkspaceController", libraryControllerSource);
        Assert.Contains("public async Task EnsureLibraryAsync(bool forceRefresh)", libraryControllerSource);
        Assert.Contains("public void HandleSearchTextChanged(string? searchQuery)", libraryControllerSource);
        Assert.Contains("public void HandleSelectionChanged(TemplateLibraryItem? selectedItem)", libraryControllerSource);
        Assert.Contains("public async Task OpenSelectedTemplateInEditorAsync()", libraryControllerSource);
        Assert.Contains("public async Task CreateTemplateAsync()", libraryControllerSource);
        Assert.Contains("public async Task DeleteSelectedTemplateAsync()", libraryControllerSource);
        Assert.Contains("public async Task ImportTemplateAsync()", libraryControllerSource);
        Assert.Contains("public async Task ExportSelectedTemplateAsync()", libraryControllerSource);
        Assert.Contains("await _templatesCapabilityService.LoadLibraryAsync(_workspace.SearchQuery);", libraryControllerSource);
        Assert.Contains("await _host.ShowTemplateEditorAsync(document, \"Template loaded.\");", libraryControllerSource);
        Assert.Contains("_host.ReconcileDeployTemplateSelection(_workspace.Items);", libraryControllerSource);
        Assert.Contains("internal sealed class TemplatesLibraryWorkspaceComposition", libraryCompositionSource);
        Assert.Contains("private readonly TemplatesLibraryWorkspaceViewModel _workspace = new();", libraryCompositionSource);
        Assert.Contains("private readonly TemplatesLibraryWorkspaceController _controller;", libraryCompositionSource);
        Assert.Contains("_view.UpdateViewState(BuildViewState(isLoading, hasSelectedLibraryItem));", libraryCompositionSource);
        Assert.Contains("_controller.HandleSearchTextChanged(_view.CaptureInteractionState().SearchText);", libraryCompositionSource);
        Assert.Contains("_controller.HandleSelectionChanged(_view.CaptureInteractionState().SelectedTemplate);", libraryCompositionSource);
    }

    [Fact]
    public void TemplatesLibraryView_UsesNarrowInteractionSurface_InsteadOfControlBagExposure()
    {
        var libraryViewSource = LoadTemplatesLibraryViewCodeBehindSource();

        Assert.Contains("public readonly record struct TemplatesLibraryInteractionState(", libraryViewSource);
        Assert.Contains("public readonly record struct TemplatesLibraryViewState(", libraryViewSource);
        Assert.Contains("public event EventHandler? SelectedTemplateChanged;", libraryViewSource);
        Assert.Contains("public event EventHandler? ApplySearchRequested;", libraryViewSource);
        Assert.Contains("public event EventHandler? OpenTemplateRequested;", libraryViewSource);
        Assert.Contains("public TemplatesLibraryInteractionState CaptureInteractionState()", libraryViewSource);
        Assert.Contains("public void UpdateViewState(TemplatesLibraryViewState state)", libraryViewSource);
        Assert.Contains("SetSearchText(state.SearchText);", libraryViewSource);
        Assert.Contains("SetSelectedTemplate(state.SelectedTemplate);", libraryViewSource);
        Assert.Contains("TemplateLibraryListView.SelectionChanged += TemplateLibraryListView_SelectionChanged;", libraryViewSource);
        Assert.Contains("ApplyTemplateSearchButton.Click += ApplyTemplateSearchButton_Click;", libraryViewSource);
        Assert.DoesNotContain("public ListView TemplateLibraryListViewControl =>", libraryViewSource);
        Assert.DoesNotContain("public TextBox TemplateSearchTextBoxControl =>", libraryViewSource);
        Assert.DoesNotContain("public Button ApplyTemplateSearchButtonControl =>", libraryViewSource);
        Assert.DoesNotContain("public Button OpenTemplateInEditorButtonControl =>", libraryViewSource);
    }

    [Fact]
    public void TemplatesEditor_UsesRefinedEditorLocalArchitecture()
    {
        var editorWorkspaceSource = LoadTemplatesEditorWorkspaceSource();
        var editorControllerSource = LoadTemplatesEditorWorkspaceControllerSource();
        var editorCompositionSource = LoadTemplatesEditorWorkspaceCompositionSource();
        var editorViewSource = LoadTemplatesEditorViewCodeBehindSource();
        var templatesWorkspaceCompositionSource = LoadTemplatesWorkspaceCompositionSource();
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains("internal sealed class TemplatesEditorWorkspaceViewModel", editorWorkspaceSource);
        Assert.Contains("public TemplateEditorDocument? ActiveDocument { get; private set; }", editorWorkspaceSource);
        Assert.Contains("public ObservableCollection<VmTemplate> VmEntries { get; } = [];", editorWorkspaceSource);
        Assert.Contains("public VmTemplate? SelectedVmEntry { get; private set; }", editorWorkspaceSource);
        Assert.Contains("public string TemplateName { get; private set; } = string.Empty;", editorWorkspaceSource);
        Assert.Contains("public string TemplateDescription { get; private set; } = string.Empty;", editorWorkspaceSource);
        Assert.Contains("public string StatusText { get; private set; } = \"No template loaded.\";", editorWorkspaceSource);
        Assert.Contains("public ObservableCollection<VmTemplate> VmEntries { get; } = [];", editorWorkspaceSource);
        Assert.Contains("public string VmNameDraft { get; private set; } = string.Empty;", editorWorkspaceSource);
        Assert.Contains("public string VmMemoryDraft { get; private set; } = string.Empty;", editorWorkspaceSource);
        Assert.Contains("public string VmCpuDraft { get; private set; } = string.Empty;", editorWorkspaceSource);
        Assert.Contains("public void ClearDocument()", editorWorkspaceSource);
        Assert.Contains("public void SetDocument(TemplateEditorDocument document)", editorWorkspaceSource);
        Assert.Contains("public void ReplaceVmEntries(IReadOnlyList<VmTemplate> vmEntries)", editorWorkspaceSource);
        Assert.Contains("public void SetSelectedVmEntry(VmTemplate? selectedVmEntry)", editorWorkspaceSource);
        Assert.Contains("public void SetVmDraftState(TemplatesEditorVmDraftState state)", editorWorkspaceSource);
        Assert.Contains("public TemplatesEditorVmDraftSnapshot CaptureVmDraftSnapshot()", editorWorkspaceSource);
        Assert.Contains("public void SetDocumentHeaderDraft(string? templateName, string? templateDescription)", editorWorkspaceSource);

        Assert.Contains("internal interface ITemplatesEditorWorkspaceControllerHost", editorControllerSource);
        Assert.Contains("internal sealed class TemplatesEditorWorkspaceController", editorControllerSource);
        Assert.Contains("private readonly ITemplatesCapabilityService _templatesCapabilityService;", editorControllerSource);
        Assert.Contains("private readonly TemplatesEditorWorkspaceViewModel _workspace;", editorControllerSource);
        Assert.Contains("private readonly ITemplatesEditorWorkspaceControllerHost _host;", editorControllerSource);
        Assert.Contains("public bool ApplySelectedVmDraft(bool showSuccessStatus)", editorControllerSource);
        Assert.Contains("public bool AddVmEntry()", editorControllerSource);
        Assert.Contains("public async Task RemoveSelectedVmEntryAsync()", editorControllerSource);
        Assert.Contains("public async Task SaveAsync()", editorControllerSource);
        Assert.Contains("public async Task SaveAsAsync()", editorControllerSource);
        Assert.Contains("public async Task ValidateAsync()", editorControllerSource);
        Assert.Contains("private bool TryApplyEditorFieldsToDocument(bool showSuccessStatus)", editorControllerSource);
        Assert.Contains("private bool TryApplySelectedVmDraft(bool showSuccessStatus)", editorControllerSource);
        Assert.Contains("private bool TryValidateSelectedSwitches(", editorControllerSource);

        Assert.Contains("internal readonly record struct TemplatesEditorReferenceData(", editorCompositionSource);
        Assert.Contains("internal interface ITemplatesEditorWorkspaceHost", editorCompositionSource);
        Assert.Contains("internal sealed class TemplatesEditorWorkspaceHost : ITemplatesEditorWorkspaceHost", editorCompositionSource);
        Assert.Contains("internal sealed class TemplatesEditorWorkspaceComposition", editorCompositionSource);
        Assert.Contains("private readonly TemplatesEditorView _view;", editorCompositionSource);
        Assert.Contains("private readonly ITemplatesEditorWorkspaceHost _host;", editorCompositionSource);
        Assert.Contains("private readonly TemplatesEditorWorkspaceViewModel _workspace = new();", editorCompositionSource);
        Assert.Contains("private readonly TemplatesEditorWorkspaceController _controller;", editorCompositionSource);
        Assert.Contains("_view.DocumentHeaderChanged += TemplatesEditorView_DocumentHeaderChanged;", editorCompositionSource);
        Assert.Contains("_view.SelectedVmChanged += TemplatesEditorView_SelectedVmChanged;", editorCompositionSource);
        Assert.Contains("_view.VmDraftChanged += TemplatesEditorView_VmDraftChanged;", editorCompositionSource);
        Assert.Contains("_view.AddVmRequested += TemplatesEditorView_AddVmRequested;", editorCompositionSource);
        Assert.Contains("_view.RemoveVmRequested += TemplatesEditorView_RemoveVmRequested;", editorCompositionSource);
        Assert.Contains("_view.ApplyVmChangesRequested += TemplatesEditorView_ApplyVmChangesRequested;", editorCompositionSource);
        Assert.Contains("_view.SaveRequested += TemplatesEditorView_SaveRequested;", editorCompositionSource);
        Assert.Contains("_view.SaveAsRequested += TemplatesEditorView_SaveAsRequested;", editorCompositionSource);
        Assert.Contains("_view.ValidateRequested += TemplatesEditorView_ValidateRequested;", editorCompositionSource);
        Assert.Contains("_view.BackToLibraryRequested += TemplatesEditorView_BackToLibraryRequested;", editorCompositionSource);
        Assert.Contains("_view.SetVmEntriesSource(_workspace.VmEntries);", editorCompositionSource);
        Assert.Contains("public IReadOnlyList<VmTemplate> VmEntries => _workspace.VmEntries;", editorCompositionSource);
        Assert.Contains("public VmTemplate? SelectedVmEntry => _workspace.SelectedVmEntry;", editorCompositionSource);
        Assert.Contains("public TemplateEditorDocument? ActiveDocument => _workspace.ActiveDocument;", editorCompositionSource);
        Assert.Contains("public TemplatesEditorVmDraftSnapshot CaptureVmDraftState() => _workspace.CaptureVmDraftSnapshot();", editorCompositionSource);
        Assert.Contains("public async Task ShowDocumentAsync(TemplateEditorDocument document, string statusText)", editorCompositionSource);
        Assert.Contains("public TemplatesEditorDocumentHeaderInteractionState CaptureDocumentHeaderState()", editorCompositionSource);
        Assert.Contains("public bool ApplySelectedVmDraft(bool showSuccessStatus) => _controller.ApplySelectedVmDraft(showSuccessStatus);", editorCompositionSource);
        Assert.Contains("public Task SaveAsync() => _controller.SaveAsync();", editorCompositionSource);
        Assert.Contains("public Task SaveAsAsync() => _controller.SaveAsAsync();", editorCompositionSource);
        Assert.Contains("public Task ValidateAsync() => _controller.ValidateAsync();", editorCompositionSource);
        Assert.Contains("public void RefreshUiState()", editorCompositionSource);
        Assert.Contains("await _host.LoadReferenceDataAsync(forceRefresh: false);", editorCompositionSource);
        Assert.Contains("_host.NavigateToEditor();", editorCompositionSource);
        Assert.Contains("_host.NavigateToLibrary();", editorCompositionSource);
        Assert.Contains("_view.UpdateDocumentHeaderState(new TemplatesEditorDocumentHeaderViewState(", editorCompositionSource);
        Assert.Contains("_view.UpdateVmDraftState(new TemplatesEditorVmDraftViewState(", editorCompositionSource);
        Assert.Contains("_view.UpdateActionState(new TemplatesEditorActionState(", editorCompositionSource);
        Assert.Contains("_view.UpdateVmSelection(_workspace.SelectedVmEntry);", editorCompositionSource);

        Assert.Contains("public readonly record struct TemplatesEditorDocumentHeaderInteractionState(", editorViewSource);
        Assert.Contains("public readonly record struct TemplatesEditorDocumentHeaderViewState(", editorViewSource);
        Assert.Contains("public readonly record struct TemplatesEditorActionState(", editorViewSource);
        Assert.Contains("public readonly record struct TemplatesEditorVmListInteractionState(", editorViewSource);
        Assert.Contains("internal readonly record struct TemplatesEditorVmDraftInteractionState(", editorViewSource);
        Assert.Contains("internal readonly record struct TemplatesEditorVmDraftViewState(", editorViewSource);
        Assert.Contains("public event EventHandler? DocumentHeaderChanged;", editorViewSource);
        Assert.Contains("public event EventHandler? SelectedVmChanged;", editorViewSource);
        Assert.Contains("public event EventHandler? VmDraftChanged;", editorViewSource);
        Assert.Contains("public event EventHandler? AddVmRequested;", editorViewSource);
        Assert.Contains("public event EventHandler? RemoveVmRequested;", editorViewSource);
        Assert.Contains("public event EventHandler? ApplyVmChangesRequested;", editorViewSource);
        Assert.Contains("public event EventHandler? SaveRequested;", editorViewSource);
        Assert.Contains("public event EventHandler? SaveAsRequested;", editorViewSource);
        Assert.Contains("public event EventHandler? ValidateRequested;", editorViewSource);
        Assert.Contains("public event EventHandler? BackToLibraryRequested;", editorViewSource);
        Assert.Contains("public TemplatesEditorDocumentHeaderInteractionState CaptureDocumentHeaderInteractionState()", editorViewSource);
        Assert.Contains("public TemplatesEditorVmListInteractionState CaptureVmListInteractionState()", editorViewSource);
        Assert.Contains("internal TemplatesEditorVmDraftInteractionState CaptureVmDraftInteractionState()", editorViewSource);
        Assert.Contains("public void UpdateDocumentHeaderState(TemplatesEditorDocumentHeaderViewState state)", editorViewSource);
        Assert.Contains("internal void UpdateVmDraftState(TemplatesEditorVmDraftViewState state)", editorViewSource);
        Assert.Contains("public void UpdateActionState(TemplatesEditorActionState state)", editorViewSource);
        Assert.Contains("public void SetVmEntriesSource(object? itemsSource)", editorViewSource);
        Assert.Contains("public void UpdateVmSelection(VmTemplate? selectedVmEntry)", editorViewSource);
        Assert.Contains("public void RefreshVmEntries()", editorViewSource);
        Assert.Contains("DocumentHeaderChanged?.Invoke(this, EventArgs.Empty);", editorViewSource);
        Assert.Contains("SelectedVmChanged?.Invoke(this, EventArgs.Empty);", editorViewSource);
        Assert.Contains("VmDraftChanged?.Invoke(this, EventArgs.Empty);", editorViewSource);
        Assert.Contains("AddVmRequested?.Invoke(this, EventArgs.Empty);", editorViewSource);
        Assert.Contains("RemoveVmRequested?.Invoke(this, EventArgs.Empty);", editorViewSource);
        Assert.Contains("ApplyVmChangesRequested?.Invoke(this, EventArgs.Empty);", editorViewSource);
        Assert.Contains("SaveRequested?.Invoke(this, EventArgs.Empty);", editorViewSource);
        Assert.Contains("SaveAsRequested?.Invoke(this, EventArgs.Empty);", editorViewSource);
        Assert.Contains("ValidateRequested?.Invoke(this, EventArgs.Empty);", editorViewSource);
        Assert.Contains("BackToLibraryRequested?.Invoke(this, EventArgs.Empty);", editorViewSource);
        Assert.DoesNotContain("public TextBox TemplateNameTextBoxControl =>", editorViewSource);
        Assert.DoesNotContain("public TextBox TemplateDescriptionTextBoxControl =>", editorViewSource);
        Assert.DoesNotContain("public TextBlock TemplateEditorStatusTextBlockControl =>", editorViewSource);
        Assert.DoesNotContain("public ListView TemplateVmListViewControl =>", editorViewSource);
        Assert.DoesNotContain("public Button AddTemplateVmButtonControl =>", editorViewSource);
        Assert.DoesNotContain("public Button RemoveTemplateVmButtonControl =>", editorViewSource);
        Assert.DoesNotContain("public Button ApplyTemplateVmChangesButtonControl =>", editorViewSource);
        Assert.DoesNotContain("public Button SaveTemplateButtonControl =>", editorViewSource);
        Assert.DoesNotContain("public Button SaveTemplateAsButtonControl =>", editorViewSource);
        Assert.DoesNotContain("public Button ValidateTemplateButtonControl =>", editorViewSource);
        Assert.DoesNotContain("public Button BackToLibraryButtonControl =>", editorViewSource);

        Assert.Contains("public TemplateEditorDocument? ActiveEditorDocument => _editorComposition.ActiveDocument;", templatesWorkspaceCompositionSource);
        Assert.Contains("public TemplatesEditorVmDraftSnapshot CaptureEditorVmDraftState() => _editorComposition.CaptureVmDraftState();", templatesWorkspaceCompositionSource);
        Assert.Contains("public Task ShowEditorDocumentAsync(TemplateEditorDocument document, string statusText) => _editorComposition.ShowDocumentAsync(document, statusText);", templatesWorkspaceCompositionSource);
        Assert.Contains("public Task SaveEditorAsync() => _editorComposition.SaveAsync();", templatesWorkspaceCompositionSource);
        Assert.Contains("public Task SaveEditorAsAsync() => _editorComposition.SaveAsAsync();", templatesWorkspaceCompositionSource);
        Assert.Contains("public Task ValidateEditorAsync() => _editorComposition.ValidateAsync();", templatesWorkspaceCompositionSource);
        Assert.DoesNotContain("private readonly TemplatesEditorView _editorView;", templatesWorkspaceCompositionSource);
        Assert.DoesNotContain("NavigateToRoute(ShellRouteKeys.TemplatesEditor);", templatesWorkspaceCompositionSource);

        Assert.Contains("_templatesWorkspaceComposition.SetEditorVmReferenceData(_templateAvailableSwitches, _templateVhdxCatalogOptions);", mainWindowSource);
        Assert.Contains("await _templatesWorkspaceComposition.ShowEditorDocumentAsync(document, statusText);", mainWindowSource);
        Assert.Contains("new TemplatesEditorWorkspaceHost(", mainWindowSource);
        Assert.DoesNotContain("SaveTemplateButton.Click += SaveTemplateButton_Click;", mainWindowSource);
        Assert.DoesNotContain("AddTemplateVmButton.Click += AddTemplateVmButton_Click;", mainWindowSource);
        Assert.DoesNotContain("private void BindTemplateEditorDocument()", mainWindowSource);
        Assert.DoesNotContain("private void UpdateTemplateVmEditorPanel()", mainWindowSource);
        Assert.DoesNotContain("private void RenderTemplateSwitchRowsFromVm()", mainWindowSource);
        Assert.DoesNotContain("private void UpdateTemplateVhdxSelectorFromVm()", mainWindowSource);
        Assert.DoesNotContain("private TemplateVhdxNormalizationResult EvaluateTemplateVhdxNormalization(", mainWindowSource);
        Assert.DoesNotContain("private bool _isUpdatingTemplateVmEditorControls;", mainWindowSource);
        Assert.DoesNotContain("private bool PullEditorFieldsIntoDocument()", mainWindowSource);
        Assert.DoesNotContain("private bool TryValidateTemplateSelectedSwitches(", mainWindowSource);
    }

    [Fact]
    public void TemplatesExtraction_ClosureCoverage_ProtectsSharedLibraryEditorAndLifetimeBoundaries()
    {
        var mainWindowSource = LoadMainWindowSource();
        var shellViewModelSource = LoadShellViewModelSource();
        var templatesWorkspaceCompositionSource = LoadTemplatesWorkspaceCompositionSource();
        var libraryWorkspaceSource = LoadTemplatesLibraryWorkspaceSource();
        var libraryControllerSource = LoadTemplatesLibraryWorkspaceControllerSource();
        var libraryCompositionSource = LoadTemplatesLibraryWorkspaceCompositionSource();
        var libraryViewSource = LoadTemplatesLibraryViewCodeBehindSource();
        var editorWorkspaceSource = LoadTemplatesEditorWorkspaceSource();
        var editorControllerSource = LoadTemplatesEditorWorkspaceControllerSource();
        var editorCompositionSource = LoadTemplatesEditorWorkspaceCompositionSource();
        var editorViewSource = LoadTemplatesEditorViewCodeBehindSource();

        Assert.Contains("internal sealed class TemplatesWorkspaceComposition", templatesWorkspaceCompositionSource);
        Assert.Contains("private readonly TemplatesLibraryWorkspaceComposition _libraryComposition;", templatesWorkspaceCompositionSource);
        Assert.Contains("private readonly TemplatesEditorWorkspaceComposition _editorComposition;", templatesWorkspaceCompositionSource);
        Assert.Contains("private readonly ITemplatesWorkspaceShellBridge _shellBridge;", templatesWorkspaceCompositionSource);
        Assert.Contains("public Task EnsureLibraryAsync(bool forceRefresh) => _libraryComposition.EnsureLibraryAsync(forceRefresh);", templatesWorkspaceCompositionSource);
        Assert.Contains("public Task ShowEditorDocumentAsync(TemplateEditorDocument document, string statusText) => _editorComposition.ShowDocumentAsync(document, statusText);", templatesWorkspaceCompositionSource);
        Assert.Contains("_libraryComposition.ApplyShellState(_shellBridge.IsTemplatesLibraryActive);", templatesWorkspaceCompositionSource);
        Assert.Contains("_editorComposition.ApplyShellState(_shellBridge.IsTemplatesEditorActive);", templatesWorkspaceCompositionSource);
        Assert.Contains("_workspaceHost.Visibility = _shellBridge.IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;", templatesWorkspaceCompositionSource);

        Assert.Contains("internal sealed class TemplatesLibraryWorkspaceViewModel", libraryWorkspaceSource);
        Assert.Contains("internal sealed class TemplatesLibraryWorkspaceController", libraryControllerSource);
        Assert.Contains("internal sealed class TemplatesLibraryWorkspaceComposition", libraryCompositionSource);
        Assert.Contains("public void ApplyShellState(bool isLibraryActive)", libraryCompositionSource);
        Assert.Contains("_ = _controller.EnsureLibraryAsync(forceRefresh: false);", libraryCompositionSource);
        Assert.Contains("public readonly record struct TemplatesLibraryInteractionState(", libraryViewSource);
        Assert.Contains("public readonly record struct TemplatesLibraryViewState(", libraryViewSource);

        Assert.Contains("internal sealed class TemplatesEditorWorkspaceViewModel", editorWorkspaceSource);
        Assert.Contains("internal sealed class TemplatesEditorWorkspaceController", editorControllerSource);
        Assert.Contains("internal sealed class TemplatesEditorWorkspaceComposition", editorCompositionSource);
        Assert.Contains("public void ApplyShellState(bool isEditorActive)", editorCompositionSource);
        Assert.Contains("await _host.LoadReferenceDataAsync(forceRefresh: false);", editorCompositionSource);
        Assert.Contains("_host.NavigateToEditor();", editorCompositionSource);
        Assert.Contains("_host.NavigateToLibrary();", editorCompositionSource);
        Assert.Contains("public readonly record struct TemplatesEditorDocumentHeaderInteractionState(", editorViewSource);
        Assert.Contains("public readonly record struct TemplatesEditorActionState(", editorViewSource);
        Assert.Contains("public readonly record struct TemplatesEditorVmListInteractionState(", editorViewSource);

        Assert.Contains("public const string TemplatesLibrary = \"templates.library\";", shellViewModelSource);
        Assert.Contains("public const string TemplatesEditor = \"templates.editor\";", shellViewModelSource);
        Assert.Contains("private readonly TemplatesWorkspaceComposition _templatesWorkspaceComposition;", mainWindowSource);
        Assert.Contains("private FrameworkElement TemplatesWorkspaceHost => TemplatesWorkspacePanel;", mainWindowSource);
        Assert.Contains("private bool IsTemplatesCapabilityActive =>", mainWindowSource);
        Assert.Contains("private bool IsTemplatesLibraryActive =>", mainWindowSource);
        Assert.Contains("private bool IsTemplatesEditorActive =>", mainWindowSource);
        Assert.Contains("ApplyTemplatesWorkspaceUiState();", mainWindowSource);
        Assert.Contains("await _templatesWorkspaceComposition.EnsureLibraryAsync(forceRefresh: true);", mainWindowSource);
        Assert.Contains("await _templatesWorkspaceComposition.ShowEditorDocumentAsync(document, statusText);", mainWindowSource);

        Assert.DoesNotContain("private readonly TemplatesLibraryView _templatesLibraryView = new();", mainWindowSource);
        Assert.DoesNotContain("private readonly TemplatesEditorView _templatesEditorView = new();", mainWindowSource);
        Assert.DoesNotContain("private readonly ObservableCollection<TemplateLibraryItem> _templateLibraryItems = [];", mainWindowSource);
        Assert.DoesNotContain("private readonly ObservableCollection<VmTemplate> _templateVmEntries = [];", mainWindowSource);
        Assert.DoesNotContain("private TemplateLibraryItem? _selectedTemplateLibraryItem;", mainWindowSource);
        Assert.DoesNotContain("private VmTemplate? _selectedTemplateVmEntry;", mainWindowSource);
        Assert.DoesNotContain("TemplatesWorkspaceHost.Visibility = IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.DoesNotContain("TemplatesLibraryViewHost.Visibility = IsTemplatesLibraryActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.DoesNotContain("TemplatesEditorViewHost.Visibility = IsTemplatesEditorActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.DoesNotContain("if (IsTemplatesLibraryActive)", mainWindowSource);
        Assert.DoesNotContain("if (IsTemplatesEditorActive)", mainWindowSource);
        Assert.DoesNotContain("private void UpdateTemplatesUi()", mainWindowSource);
        Assert.DoesNotContain("private void BindTemplateEditorDocument()", mainWindowSource);
        Assert.DoesNotContain("private void UpdateTemplateVmEditorPanel()", mainWindowSource);
        Assert.DoesNotContain("private bool PullEditorFieldsIntoDocument()", mainWindowSource);
        Assert.DoesNotContain("WireTemplatesHandlers()", mainWindowSource);
        Assert.DoesNotContain("NavigateToRoute(ShellRouteKeys.TemplatesEditor);", templatesWorkspaceCompositionSource);
    }

    [Fact]
    public void MainWindow_PreservesShellBoundary_WhileHostingLongLivedDeployWorkspace()
    {
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains("private readonly DeployWorkspaceComposition _deployWorkspaceComposition;", mainWindowSource);
        Assert.Contains("_deployWorkspaceComposition = new DeployWorkspaceComposition(", mainWindowSource);
        Assert.Contains("DeployLocalNavigationPanel,", mainWindowSource);
        Assert.Contains("DeployOverviewViewHost,", mainWindowSource);
        Assert.Contains("DeployOnTheFlyViewHost,", mainWindowSource);
        Assert.Contains("DeployFromTemplateViewHost,", mainWindowSource);
        Assert.Contains("CreateDeployWorkspaceUiState,", mainWindowSource);
        Assert.Contains("new DeployWorkspaceShellBridge(", mainWindowSource);
        Assert.Contains("_deployWorkspaceComposition.RefreshSharedUiState();", mainWindowSource);
        Assert.Contains("_deployWorkspaceComposition.ApplyShellState();", mainWindowSource);

        Assert.DoesNotContain("private bool _isUpdatingDeploySubviewSelection;", mainWindowSource);
        Assert.DoesNotContain("private void DeploySubviewTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)", mainWindowSource);
        Assert.DoesNotContain("private void SyncDeploySubviewSelection()", mainWindowSource);
        Assert.DoesNotContain("private void UpdateDeployOverviewUi()", mainWindowSource);
        Assert.DoesNotContain("private void ApplyDeployWorkspaceUiState()", mainWindowSource);
        Assert.DoesNotContain("DeployLocalNavPanel.Visibility = IsDeployCapabilityActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.DoesNotContain("DeployOverviewPanel.Visibility = IsDeployOverviewActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.DoesNotContain("DeployFromTemplatePanel.Visibility = IsDeployFromTemplateActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.DoesNotContain("DeployOnTheFlyPanel.Visibility = IsDeployOnTheFlyActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.DoesNotContain("DeployOverviewOpenQuickDeployButton.Click += (_, _) => NavigateToRoute(ShellRouteKeys.DeployOnTheFly);", mainWindowSource);
        Assert.DoesNotContain("DeployOverviewOpenFromTemplateButton.Click += (_, _) => NavigateToRoute(ShellRouteKeys.DeployFromTemplate);", mainWindowSource);
    }

    [Fact]
    public void DeployOverview_ProtectsRefinedOverviewLocalArchitecture()
    {
        var mainWindowSource = LoadMainWindowSource();
        var compositionSource = LoadDeployWorkspaceCompositionSource();
        var overviewCompositionSource = LoadDeployOverviewWorkspaceCompositionSource();
        var overviewWorkspaceSource = LoadDeployOverviewWorkspaceViewModelSource();
        var overviewCodeBehindSource = LoadDeployOverviewCodeBehindSource();
        var overviewXaml = LoadDeployOverviewXaml();

        Assert.Contains("private readonly DeployWorkspaceComposition _deployWorkspaceComposition;", mainWindowSource);
        Assert.Contains("_deployWorkspaceComposition = new DeployWorkspaceComposition(", mainWindowSource);
        Assert.Contains("DeployOverviewViewHost,", mainWindowSource);
        Assert.Contains("_deployWorkspaceComposition.RefreshSharedUiState();", mainWindowSource);
        Assert.Contains("_deployWorkspaceComposition.ApplyShellState();", mainWindowSource);
        Assert.DoesNotContain("private void UpdateDeployOverviewUi()", mainWindowSource);
        Assert.DoesNotContain("DeployOverviewOpenQuickDeployButton.Click += (_, _) => NavigateToRoute(ShellRouteKeys.DeployOnTheFly);", mainWindowSource);
        Assert.DoesNotContain("DeployOverviewOpenFromTemplateButton.Click += (_, _) => NavigateToRoute(ShellRouteKeys.DeployFromTemplate);", mainWindowSource);

        Assert.Contains("internal sealed class DeployWorkspaceComposition", compositionSource);
        Assert.Contains("private readonly FrameworkElement _localNavigationHost;", compositionSource);
        Assert.Contains("private readonly FrameworkElement _overviewHost;", compositionSource);
        Assert.Contains("private readonly FrameworkElement _onTheFlyHost;", compositionSource);
        Assert.Contains("private readonly FrameworkElement _fromTemplateHost;", compositionSource);
        Assert.Contains("private readonly DeployOverviewWorkspaceComposition _overviewWorkspaceComposition;", compositionSource);
        Assert.Contains("private readonly IDeployWorkspaceShellBridge _shellBridge;", compositionSource);
        Assert.Contains("private bool _isUpdatingDeploySubviewSelection;", compositionSource);
        Assert.Contains("_overviewWorkspaceComposition = new DeployOverviewWorkspaceComposition(", compositionSource);
        Assert.Contains("public void RefreshSharedUiState()", compositionSource);
        Assert.Contains("public void ApplyShellState()", compositionSource);
        Assert.Contains("_localNavigationHost.Visibility = _shellBridge.IsDeployCapabilityActive ? Visibility.Visible : Visibility.Collapsed;", compositionSource);
        Assert.Contains("_overviewHost.Visibility = _shellBridge.IsDeployOverviewActive ? Visibility.Visible : Visibility.Collapsed;", compositionSource);
        Assert.Contains("_onTheFlyHost.Visibility = _shellBridge.IsDeployOnTheFlyActive ? Visibility.Visible : Visibility.Collapsed;", compositionSource);
        Assert.Contains("_fromTemplateHost.Visibility = _shellBridge.IsDeployFromTemplateActive ? Visibility.Visible : Visibility.Collapsed;", compositionSource);
        Assert.Contains("_subviewTabView.SelectionChanged += DeploySubviewTabView_SelectionChanged;", compositionSource);
        Assert.Contains("public void RefreshSharedUiState() => _overviewWorkspaceComposition.RefreshUiState();", compositionSource);
        Assert.Contains("_overviewWorkspaceComposition.ApplyShellState();", compositionSource);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.DeployOverview);", compositionSource);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.DeployOnTheFly);", compositionSource);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.DeployFromTemplate);", compositionSource);

        Assert.DoesNotContain("DeployTemplateSelectorComboBox", compositionSource);
        Assert.DoesNotContain("DeployOnTheFlyVmEntriesListView", compositionSource);
        Assert.DoesNotContain("EvaluateDeployOnTheFlyReadinessAsync", compositionSource);
        Assert.DoesNotContain("UpdateDeployUi()", compositionSource);
        Assert.DoesNotContain("UpdateDeployOnTheFlyUi()", compositionSource);
        Assert.DoesNotContain("public void ApplyUiState(DeployWorkspaceUiState state)", compositionSource);
        Assert.DoesNotContain("internal interface IDeployWorkspaceHost", compositionSource);
        Assert.DoesNotContain("internal sealed class DeployWorkspaceHost", compositionSource);
        Assert.DoesNotContain("private readonly DeployOverviewView _overviewView;", compositionSource);
        Assert.DoesNotContain("private readonly Func<DeployWorkspaceUiState> _getUiState;", compositionSource);
        Assert.DoesNotContain("private DeployWorkspaceUiState _uiState;", compositionSource);
        Assert.DoesNotContain("_overviewView.DeployOverviewOpenQuickDeployButtonControl", compositionSource);
        Assert.DoesNotContain("_overviewView.DeployOverviewFromTemplateSummaryTextBlockControl", compositionSource);

        Assert.Contains("internal sealed class DeployOverviewWorkspaceComposition", overviewCompositionSource);
        Assert.Contains("private readonly DeployOverviewView _view;", overviewCompositionSource);
        Assert.Contains("private readonly DeployOverviewWorkspaceViewModel _workspace = new();", overviewCompositionSource);
        Assert.Contains("private readonly IDeployOverviewWorkspaceHost _host;", overviewCompositionSource);
        Assert.Contains("private readonly IDeployOverviewWorkspaceShellBridge _shellBridge;", overviewCompositionSource);
        Assert.Contains("new DeployOverviewWorkspaceHost(", compositionSource);
        Assert.Contains("new DeployOverviewWorkspaceShellBridge(", compositionSource);
        Assert.Contains("WireHandlers();", overviewCompositionSource);
        Assert.Contains("ApplyWorkspaceState();", overviewCompositionSource);
        Assert.Contains("public void RefreshUiState()", overviewCompositionSource);
        Assert.Contains("public void ApplyShellState()", overviewCompositionSource);
        Assert.Contains("if (!_shellBridge.IsDeployOverviewActive)", overviewCompositionSource);
        Assert.Contains("_view.OpenQuickDeployRequested += OpenQuickDeployRequested;", overviewCompositionSource);
        Assert.Contains("_view.OpenFromTemplateRequested += OpenFromTemplateRequested;", overviewCompositionSource);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.DeployOnTheFly);", overviewCompositionSource);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.DeployFromTemplate);", overviewCompositionSource);
        Assert.Contains("_workspace.RefreshSummary(", overviewCompositionSource);
        Assert.Contains("_view.UpdateSummary(_workspace.QuickDeploySummaryText, _workspace.FromTemplateSummaryText);", overviewCompositionSource);
        Assert.DoesNotContain("DeployOverviewOpenQuickDeployButtonControl", overviewCompositionSource);
        Assert.DoesNotContain("DeployOverviewOpenFromTemplateButtonControl", overviewCompositionSource);
        Assert.DoesNotContain("DeployOverviewQuickDeploySummaryTextBlockControl", overviewCompositionSource);
        Assert.DoesNotContain("DeployOverviewFromTemplateSummaryTextBlockControl", overviewCompositionSource);

        Assert.Contains("internal sealed class DeployOverviewWorkspaceViewModel", overviewWorkspaceSource);
        Assert.Contains("public string QuickDeploySummaryText { get; private set; }", overviewWorkspaceSource);
        Assert.Contains("public string FromTemplateSummaryText { get; private set; }", overviewWorkspaceSource);
        Assert.Contains("public void RefreshSummary(int quickDeployDraftCount, bool isLoadingTemplates, int availableTemplateCount)", overviewWorkspaceSource);
        Assert.Contains("\"Template inventory is loading.\"", overviewWorkspaceSource);

        Assert.NotNull(FindByName(overviewXaml, "DeployOverviewQuickDeploySummaryTextBlock"));
        Assert.NotNull(FindByName(overviewXaml, "DeployOverviewOpenQuickDeployButton"));
        Assert.NotNull(FindByName(overviewXaml, "DeployOverviewFromTemplateSummaryTextBlock"));
        Assert.NotNull(FindByName(overviewXaml, "DeployOverviewOpenFromTemplateButton"));
        Assert.Contains("public event EventHandler? OpenQuickDeployRequested;", overviewCodeBehindSource);
        Assert.Contains("public event EventHandler? OpenFromTemplateRequested;", overviewCodeBehindSource);
        Assert.Contains("public void UpdateSummary(string quickDeploySummaryText, string fromTemplateSummaryText)", overviewCodeBehindSource);
        Assert.Contains("OpenQuickDeployRequested?.Invoke(this, EventArgs.Empty);", overviewCodeBehindSource);
        Assert.Contains("OpenFromTemplateRequested?.Invoke(this, EventArgs.Empty);", overviewCodeBehindSource);
        Assert.DoesNotContain("DeployOverviewOpenQuickDeployButtonControl", overviewCodeBehindSource);
        Assert.DoesNotContain("DeployOverviewOpenFromTemplateButtonControl", overviewCodeBehindSource);
        Assert.DoesNotContain("DeployOverviewQuickDeploySummaryTextBlockControl", overviewCodeBehindSource);
        Assert.DoesNotContain("DeployOverviewFromTemplateSummaryTextBlockControl", overviewCodeBehindSource);
    }

    [Fact]
    public void DeployFromTemplate_UsesLocalSelectionReviewStateSeam()
    {
        var mainWindowSource = LoadMainWindowSource();
        var deployWorkspaceCompositionSource = LoadDeployWorkspaceCompositionSource();
        var fromTemplateCompositionSource = LoadDeployFromTemplateWorkspaceCompositionSource();
        var fromTemplateWorkspaceSource = LoadDeployFromTemplateWorkspaceViewModelSource();

        Assert.Contains("private readonly DeployFromTemplateWorkspaceComposition _deployFromTemplateWorkspaceComposition;", mainWindowSource);
        Assert.Contains("_deployFromTemplateWorkspaceComposition = new DeployFromTemplateWorkspaceComposition(", mainWindowSource);
        Assert.Contains("_deployFromTemplateWorkspaceComposition.ActiveTemplateDocument", mainWindowSource);
        Assert.Contains("_deployFromTemplateWorkspaceComposition.SelectedTemplateLibraryItem", mainWindowSource);
        Assert.DoesNotContain("private TemplateLibraryItem? _selectedDeployTemplateLibraryItem;", mainWindowSource);
        Assert.DoesNotContain("private TemplateEditorDocument? _activeDeployTemplateDocument;", mainWindowSource);

        Assert.Contains("private readonly DeployFromTemplateWorkspaceComposition _fromTemplateWorkspaceComposition;", deployWorkspaceCompositionSource);
        Assert.Contains("DeployFromTemplateWorkspaceComposition fromTemplateWorkspaceComposition,", deployWorkspaceCompositionSource);
        Assert.Contains("_fromTemplateWorkspaceComposition = fromTemplateWorkspaceComposition;", deployWorkspaceCompositionSource);
        Assert.Contains("_fromTemplateWorkspaceComposition.ApplyShellState();", deployWorkspaceCompositionSource);

        Assert.Contains("internal sealed class DeployFromTemplateWorkspaceComposition", fromTemplateCompositionSource);
        Assert.Contains("private readonly DeployFromTemplateView _view;", fromTemplateCompositionSource);
        Assert.Contains("private readonly DeployFromTemplateWorkspaceViewModel _workspace = new();", fromTemplateCompositionSource);
        Assert.Contains("_view.DeployTemplateSelectorComboBoxControl.ItemsSource = templateItemsSource;", fromTemplateCompositionSource);
        Assert.Contains("public TemplateLibraryItem? SelectedTemplateLibraryItem => _workspace.SelectedTemplateLibraryItem;", fromTemplateCompositionSource);
        Assert.Contains("public TemplateEditorDocument? ActiveTemplateDocument => _workspace.ActiveTemplateDocument;", fromTemplateCompositionSource);
        Assert.Contains("public void SetSelectedTemplateLibraryItem(TemplateLibraryItem? selectedTemplateLibraryItem)", fromTemplateCompositionSource);
        Assert.Contains("public void SetLoadedTemplateDocument(TemplateEditorDocument document, string actionStatusText)", fromTemplateCompositionSource);
        Assert.Contains("public void SetActionStatus(string actionStatusText)", fromTemplateCompositionSource);
        Assert.Contains("public void RefreshReviewState(bool hasBlockingFailures)", fromTemplateCompositionSource);
        Assert.Contains("public void ReconcileSelection(IReadOnlyList<TemplateLibraryItem> items)", fromTemplateCompositionSource);
        Assert.DoesNotContain("EvaluateDeployReadinessAsync", fromTemplateCompositionSource);
        Assert.DoesNotContain("BuildDeployContext", fromTemplateCompositionSource);

        Assert.Contains("internal sealed class DeployFromTemplateWorkspaceViewModel", fromTemplateWorkspaceSource);
        Assert.Contains("public TemplateLibraryItem? SelectedTemplateLibraryItem { get; private set; }", fromTemplateWorkspaceSource);
        Assert.Contains("public string? SelectedTemplateFilePath { get; private set; }", fromTemplateWorkspaceSource);
        Assert.Contains("public TemplateEditorDocument? ActiveTemplateDocument { get; private set; }", fromTemplateWorkspaceSource);
        Assert.Contains("public string TemplateSummaryText { get; private set; }", fromTemplateWorkspaceSource);
        Assert.Contains("public string TemplateRemediationText { get; private set; }", fromTemplateWorkspaceSource);
        Assert.Contains("public string ActionStatusText { get; private set; } = \"No action selected.\";", fromTemplateWorkspaceSource);
        Assert.Contains("public void ClearSelection(string actionStatusText)", fromTemplateWorkspaceSource);
        Assert.Contains("public void SetLoadedTemplateDocument(TemplateEditorDocument document, string actionStatusText)", fromTemplateWorkspaceSource);
        Assert.Contains("public void RefreshReviewState(bool hasBlockingFailures)", fromTemplateWorkspaceSource);
        Assert.Contains("public void ReconcileSelection(IReadOnlyList<TemplateLibraryItem> items)", fromTemplateWorkspaceSource);
    }

    [Fact]
    public void DeployWorkspaceShellBridge_RemainsNarrowAndShellOwned()
    {
        var compositionSource = LoadDeployWorkspaceCompositionSource();
        var shellBridgeInterfaceBlock = ExtractSection(
            compositionSource,
            "internal interface IDeployWorkspaceShellBridge",
            "internal sealed class DeployWorkspaceShellBridge");
        var shellBridgeClassBlock = ExtractSection(
            compositionSource,
            "internal sealed class DeployWorkspaceShellBridge",
            "internal sealed class DeployWorkspaceComposition");

        Assert.Contains("bool IsDeployCapabilityActive { get; }", shellBridgeInterfaceBlock);
        Assert.Contains("bool IsDeployOverviewActive { get; }", shellBridgeInterfaceBlock);
        Assert.Contains("bool IsDeployOnTheFlyActive { get; }", shellBridgeInterfaceBlock);
        Assert.Contains("bool IsDeployFromTemplateActive { get; }", shellBridgeInterfaceBlock);
        Assert.Contains("void NavigateToRoute(string routeKey);", shellBridgeInterfaceBlock);

        Assert.DoesNotContain("DeployWorkspaceUiState", shellBridgeInterfaceBlock);
        Assert.DoesNotContain("RefreshSharedUiState", shellBridgeInterfaceBlock);
        Assert.DoesNotContain("UpdateOverviewUi", shellBridgeInterfaceBlock);
        Assert.DoesNotContain("QuickDeployDraftCount", shellBridgeInterfaceBlock);
        Assert.DoesNotContain("AvailableTemplateCount", shellBridgeInterfaceBlock);

        Assert.Contains("internal sealed class DeployWorkspaceShellBridge : IDeployWorkspaceShellBridge", shellBridgeClassBlock);
        Assert.Contains("private readonly Func<bool> _isDeployCapabilityActive;", shellBridgeClassBlock);
        Assert.Contains("private readonly Func<bool> _isDeployOverviewActive;", shellBridgeClassBlock);
        Assert.Contains("private readonly Func<bool> _isDeployOnTheFlyActive;", shellBridgeClassBlock);
        Assert.Contains("private readonly Func<bool> _isDeployFromTemplateActive;", shellBridgeClassBlock);
        Assert.Contains("private readonly Action<string> _navigateToRoute;", shellBridgeClassBlock);
        Assert.Contains("public bool IsDeployCapabilityActive => _isDeployCapabilityActive();", shellBridgeClassBlock);
        Assert.Contains("public bool IsDeployOverviewActive => _isDeployOverviewActive();", shellBridgeClassBlock);
        Assert.Contains("public bool IsDeployOnTheFlyActive => _isDeployOnTheFlyActive();", shellBridgeClassBlock);
        Assert.Contains("public bool IsDeployFromTemplateActive => _isDeployFromTemplateActive();", shellBridgeClassBlock);
        Assert.Contains("public void NavigateToRoute(string routeKey) => _navigateToRoute(routeKey);", shellBridgeClassBlock);

        Assert.DoesNotContain("DeployOverviewView", shellBridgeClassBlock);
        Assert.DoesNotContain("TabView", shellBridgeClassBlock);
        Assert.DoesNotContain("DeployWorkspaceUiState", shellBridgeClassBlock);
        Assert.DoesNotContain("RefreshSharedUiState", shellBridgeClassBlock);
        Assert.DoesNotContain("UpdateOverviewUi", shellBridgeClassBlock);
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

    private static string LoadTemplatesWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Templates", "TemplatesWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployOverviewWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployOverviewWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployOverviewWorkspaceViewModelSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployOverviewWorkspaceViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployFromTemplateWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployFromTemplateWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployFromTemplateWorkspaceViewModelSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployFromTemplateWorkspaceViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployOverviewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployOverviewView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTemplatesEditorWorkspaceSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Templates", "TemplatesEditorWorkspaceViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTemplatesEditorWorkspaceControllerSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Templates", "TemplatesEditorWorkspaceController.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTemplatesEditorWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Templates", "TemplatesEditorWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTemplatesLibraryWorkspaceSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Templates", "TemplatesLibraryWorkspaceViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTemplatesLibraryWorkspaceControllerSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Templates", "TemplatesLibraryWorkspaceController.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTemplatesLibraryWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Templates", "TemplatesLibraryWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTemplatesLibraryViewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Templates", "TemplatesLibraryView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTemplatesEditorViewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Templates", "TemplatesEditorView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsOverviewWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "AssetsOverviewWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsOverviewWorkspaceViewModelSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "AssetsOverviewWorkspaceViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsBaseDisksWorkspaceViewModelSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "AssetsBaseDisksWorkspaceViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsSwitchesWorkspaceViewModelSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "AssetsSwitchesWorkspaceViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsSwitchesWorkspaceControllerSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "AssetsSwitchesWorkspaceController.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsSwitchesWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "AssetsSwitchesWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsBaseDisksWorkspaceControllerSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "AssetsBaseDisksWorkspaceController.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsBaseDisksWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "AssetsBaseDisksWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsOverviewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsOverviewView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsBaseDisksViewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsBaseDisksView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XDocument LoadAssetsBaseDisksViewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsBaseDisksView.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static XDocument LoadAssetsSwitchesViewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsSwitchesView.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static string LoadAssetsSwitchesViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsSwitchesView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsSwitchesViewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsSwitchesView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XDocument LoadAssetsOverviewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsOverviewView.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static XDocument LoadDeployOverviewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployOverviewView.xaml");
        return XDocument.Load(Path.GetFullPath(path));
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
