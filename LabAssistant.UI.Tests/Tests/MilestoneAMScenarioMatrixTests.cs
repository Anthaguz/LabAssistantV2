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
        Assert.Contains("_assetsWorkspaceComposition = new AssetsWorkspaceComposition(", mainWindowSource);
        Assert.Contains("_assetsBaseDisksWorkspaceComposition,", mainWindowSource);
        Assert.Contains("_assetsBaseDisksWorkspaceComposition = new AssetsBaseDisksWorkspaceComposition(", mainWindowSource);
        Assert.Contains("new AssetsBaseDisksCompositionHost(", mainWindowSource);
        Assert.Contains("new AssetsWorkspaceHost(", mainWindowSource);
        Assert.Contains("EnsureAssetsSwitchesAsync,", mainWindowSource);
        Assert.Contains("UpdateAssetsSwitchesUi),", mainWindowSource);
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
        var overviewWorkspaceSource = LoadAssetsOverviewWorkspaceViewModelSource();
        var overviewXaml = LoadAssetsOverviewXaml();
        var overviewCodeBehindSource = LoadAssetsOverviewCodeBehindSource();

        Assert.Contains("internal sealed class AssetsWorkspaceComposition", compositionSource);
        Assert.Contains("private readonly AssetsBaseDisksWorkspaceComposition _baseDisksWorkspaceComposition;", compositionSource);
        Assert.Contains("private readonly AssetsSwitchesView _switchesView;", compositionSource);
        Assert.Contains("private readonly TabView _subviewTabView;", compositionSource);
        Assert.Contains("private readonly AssetsOverviewWorkspaceComposition _overviewWorkspaceComposition;", compositionSource);
        Assert.Contains("private readonly IAssetsWorkspaceHost _host;", compositionSource);
        Assert.Contains("private readonly IAssetsWorkspaceShellBridge _shellBridge;", compositionSource);
        Assert.Contains("private bool _isUpdatingAssetsSubviewSelection;", compositionSource);
        Assert.Contains("AssetsBaseDisksWorkspaceComposition baseDisksWorkspaceComposition,", compositionSource);
        Assert.Contains("_overviewWorkspaceComposition = new AssetsOverviewWorkspaceComposition(", compositionSource);
        Assert.Contains("new AssetsOverviewWorkspaceHost(", compositionSource);
        Assert.Contains("new AssetsOverviewWorkspaceShellBridge(", compositionSource);
        Assert.Contains("() => _baseDisksWorkspaceComposition.IsLoading,", compositionSource);
        Assert.Contains("() => _baseDisksWorkspaceComposition.InventoryCount,", compositionSource);
        Assert.Contains("_switchesView.AssetsSwitchesListViewControl.ItemsSource = switchRows;", compositionSource);
        Assert.Contains("_switchesView.AssetsSwitchesAttachedVmsListViewControl.ItemsSource = attachedVmNames;", compositionSource);
        Assert.Contains("_subviewTabView.SelectionChanged += AssetsSubviewTabView_SelectionChanged;", compositionSource);
        Assert.Contains("public void ApplyShellState()", compositionSource);
        Assert.Contains("SyncAssetsSubviewSelection();", compositionSource);
        Assert.Contains("_overviewWorkspaceComposition.ApplyShellState();", compositionSource);
        Assert.Contains("_baseDisksWorkspaceComposition.ApplyShellState();", compositionSource);
        Assert.Contains("_ = _host.EnsureAssetsSwitchesAsync(forceRefresh: false);", compositionSource);
        Assert.Contains("_host.UpdateAssetsSwitchesUi();", compositionSource);
        Assert.DoesNotContain("private readonly AssetsOverviewView _overviewView;", compositionSource);
        Assert.DoesNotContain("private void UpdateAssetsOverviewUi()", compositionSource);
        Assert.DoesNotContain("OpenBaseDisksRequested", compositionSource);
        Assert.DoesNotContain("OpenSwitchesRequested", compositionSource);
        Assert.DoesNotContain("ObservableCollection<AssetsBaseDiskListRow> baseDiskRows", compositionSource);
        Assert.DoesNotContain("_host.EnsureAssetsBaseDisksAsync", compositionSource);
        Assert.DoesNotContain("_host.UpdateAssetsBaseDisksUi()", compositionSource);
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
    public void AssetsShellBridge_RemainsNarrowAndShellOwned()
    {
        var compositionSource = LoadAssetsWorkspaceCompositionSource();
        var baseDisksCompositionSource = LoadAssetsBaseDisksWorkspaceCompositionSource();
        var controllerSource = LoadAssetsBaseDisksWorkspaceControllerSource();
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

        Assert.Contains("Task EnsureAssetsSwitchesAsync(bool forceRefresh);", hostInterfaceBlock);
        Assert.Contains("bool IsAssetsSwitchesLoading { get; }", hostInterfaceBlock);
        Assert.Contains("int AssetsSwitchCount { get; }", hostInterfaceBlock);
        Assert.DoesNotContain("UpdateAssetsOverviewUi", hostInterfaceBlock);
        Assert.Contains("void UpdateAssetsSwitchesUi();", hostInterfaceBlock);
        Assert.DoesNotContain("Task EnsureAssetsBaseDisksAsync(bool forceRefresh);", hostInterfaceBlock);
        Assert.DoesNotContain("void UpdateAssetsBaseDisksUi();", hostInterfaceBlock);
        Assert.DoesNotContain("bool IsAssetsBaseDisksLoading { get; }", hostInterfaceBlock);
        Assert.DoesNotContain("int AssetsBaseDiskCount { get; }", hostInterfaceBlock);

        Assert.Contains("internal sealed class AssetsWorkspaceHost : IAssetsWorkspaceHost", hostClassBlock);
        Assert.Contains("public bool IsAssetsSwitchesLoading =>", hostClassBlock);
        Assert.Contains("public int AssetsSwitchCount =>", hostClassBlock);
        Assert.Contains("public Task EnsureAssetsSwitchesAsync(bool forceRefresh) =>", hostClassBlock);
        Assert.DoesNotContain("_updateAssetsOverviewUi", hostClassBlock);
        Assert.Contains("public void UpdateAssetsSwitchesUi() =>", hostClassBlock);
        Assert.DoesNotContain("public Task EnsureAssetsBaseDisksAsync(bool forceRefresh) =>", hostClassBlock);
        Assert.DoesNotContain("public void UpdateAssetsBaseDisksUi() =>", hostClassBlock);
        Assert.DoesNotContain("public bool IsAssetsBaseDisksLoading =>", hostClassBlock);
        Assert.DoesNotContain("public int AssetsBaseDiskCount =>", hostClassBlock);

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

    private static XDocument LoadAssetsOverviewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsOverviewView.xaml");
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
