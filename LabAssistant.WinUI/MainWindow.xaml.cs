using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using LabAssistant.Business.Deployment;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Business.Assets;
using LabAssistant.Business.Machines;
using LabAssistant.Services.Logging;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using LabAssistant.WinUI.Theming;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.Models.Assets;
using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.ViewModels.Assets;
using LabAssistant.WinUI.ViewModels.Diagnostics;
using LabAssistant.WinUI.ViewModels.Deploy;
using LabAssistant.WinUI.ViewModels.Machines;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.Views.Assets;
using LabAssistant.WinUI.Views.Deploy;
using LabAssistant.WinUI.Views.Machines;
using LabAssistant.WinUI.Views.Templates;
using LabAssistant.WinUI.Interop;
using Microsoft.UI.Dispatching;
using WinRT.Interop;

namespace LabAssistant.WinUI;

public sealed partial class MainWindow : Window, IDeployOnTheFlyWorkspaceControllerHost, IDeployOnTheFlyCompositionHost
{
    private readonly ShellViewModel _shellViewModel = new();
    private readonly Dictionary<string, NavigationViewItem> _routeToNavigationItem = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NavigationViewItem> _routeToCapabilityNavigationItem = new(StringComparer.Ordinal);
    private readonly IMachinesCapabilityService _machinesCapabilityService;
    private readonly ITemplatesCapabilityService _templatesCapabilityService;
    private readonly IDeploymentPreflightService _deploymentPreflightService;
    private readonly IDeploymentCoordinator _deploymentCoordinator;
    private readonly IDeploymentOutcomeSummaryBuilder _deploymentOutcomeSummaryBuilder;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IVhdxCatalogStore _vhdxCatalogStore;
    private readonly IAssetsBaseDisksCapabilityService _assetsBaseDisksCapabilityService;
    private readonly IAssetsSwitchesCapabilityService _assetsSwitchesCapabilityService;
    private readonly MachinesWorkspaceComposition _machinesWorkspaceComposition;
    private readonly AssetsWorkspaceComposition _assetsWorkspaceComposition;
    private readonly AssetsBaseDisksWorkspaceComposition _assetsBaseDisksWorkspaceComposition;
    private readonly AssetsSwitchesWorkspaceComposition _assetsSwitchesWorkspaceComposition;
    private readonly TemplatesWorkspaceComposition _templatesWorkspaceComposition;
    private readonly DeployFromTemplateWorkspaceComposition _deployFromTemplateWorkspaceComposition;
    private readonly DeployWorkspaceComposition _deployWorkspaceComposition;
    private readonly DiagnosticsWorkspaceComposition _diagnosticsWorkspaceComposition;
    private readonly DeployOnTheFlyWorkspaceViewModel _deployOnTheFlyWorkspace = new();
    private readonly DeployOnTheFlyWorkspaceController _deployOnTheFlyWorkspaceController;
    private readonly DeployOnTheFlyWorkspaceComposition _deployOnTheFlyWorkspaceComposition;
    private readonly List<TemplateVhdxCatalogOption> _templateVhdxCatalogOptions = [];
    private readonly List<DeployCompatibilityIssue> _deployCompatibilityIssues = [];
    private IReadOnlyList<string> _templateAvailableSwitches = Array.Empty<string>();
    private ShellCapability _activeCapability;
    private ShellSubview _activeSubview;
    private string _activeRouteKey = string.Empty;
    private DeploymentReadinessReport? _deployReadinessReport;
    private string _deployOnTheFlyStatusText = "Ready.";
    private bool _isSavingDeletionPolicy;
    private bool _isTemplatesLoading;
    private bool _isUpdatingNavigationSelection;
    private bool _isDeployLoadingTemplates;
    private int _deployOnTheFlyAutoEvaluateNonce;
    private bool _isShellRightPanelOpen;
    private bool _isShellRightPanelInCompactFallback;
    private string _shellRightPanelOwnerCapabilityKey = string.Empty;
    private const double ShellRightPanelCompactThreshold = 1200;
    private const double ShellRightPanelExpandedWidth = 380;
    private const double ShellNavigationDrawerThreshold = 1100;
    private ElementTheme _theme = ElementTheme.Light;
    private DispatcherQueueTimer? _rdpReadinessTimer;

    private DeployFromTemplateView DeployFromTemplateView => DeployFromTemplateViewHost;
    private AssetsOverviewView AssetsOverviewView => AssetsOverviewViewHost;
    private AssetsBaseDisksView AssetsBaseDisksView => AssetsBaseDisksViewHost;
    private AssetsSwitchesView AssetsSwitchesView => AssetsSwitchesViewHost;
    private DeployFromTemplateRightPanelView DeployFromTemplateRightPanelView => DeployFromTemplateRightPanelViewHost;
    private FrameworkElement MachinesOverviewPanel => MachinesOverviewViewHost;
    private FrameworkElement AssetsOverviewPanel => AssetsOverviewViewHost;
    private FrameworkElement AssetsBaseDisksPanel => AssetsBaseDisksViewHost;
    private FrameworkElement AssetsSwitchesPanel => AssetsSwitchesViewHost;
    private FrameworkElement TemplatesWorkspaceHost => TemplatesWorkspacePanel;
    private FrameworkElement AssetsLocalNavPanel => AssetsLocalNavigationPanel;
    private FrameworkElement DeployFromTemplateRightPanel => DeployFromTemplateRightPanelViewHost;
    private FrameworkElement DeployOnTheFlyRightPanel => DeployOnTheFlyRightPanelViewHost;
    private TemplatesLibraryView TemplatesLibraryView => TemplatesLibraryViewHost;
    private TemplatesEditorView TemplatesEditorView => TemplatesEditorViewHost;
    private IList<TemplateLibraryItem> TemplatesLibraryItems => _templatesWorkspaceComposition.LibraryItems;
    private const string DeployOnTheFlySwitchPlaceholder = "(No switch)";
    private const string DeployOnTheFlyVhdxPlaceholder = "(Select base disk)";
    private const string DeployCapabilityKey = "deploy";

    public MainWindow()
    {
        InitializeComponent();
        _machinesCapabilityService = App.Services.GetRequiredService<IMachinesCapabilityService>();
        _templatesCapabilityService = App.Services.GetRequiredService<ITemplatesCapabilityService>();
        _deploymentPreflightService = App.Services.GetRequiredService<IDeploymentPreflightService>();
        _deploymentCoordinator = App.Services.GetRequiredService<IDeploymentCoordinator>();
        _deploymentOutcomeSummaryBuilder = App.Services.GetRequiredService<IDeploymentOutcomeSummaryBuilder>();
        _settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
        _vhdxCatalogStore = App.Services.GetRequiredService<IVhdxCatalogStore>();
        _assetsBaseDisksCapabilityService = App.Services.GetRequiredService<IAssetsBaseDisksCapabilityService>();
        _assetsSwitchesCapabilityService = App.Services.GetRequiredService<IAssetsSwitchesCapabilityService>();
        _machinesWorkspaceComposition = new MachinesWorkspaceComposition(
            _machinesCapabilityService,
            MachinesOverviewViewHost,
            new MachinesWorkspaceShellBridge(
                () => IsMachinesOverviewActive,
                UpdateReadinessPollingState,
                () => RootLayout.XamlRoot));
        _assetsBaseDisksWorkspaceComposition = new AssetsBaseDisksWorkspaceComposition(
            _assetsBaseDisksCapabilityService,
            AssetsBaseDisksViewHost,
            new AssetsBaseDisksCompositionHost(
                PickBaseDiskFilePath,
                ShowAssetsBaseDiskRemoveConfirmationDialogAsync));
        _assetsSwitchesWorkspaceComposition = new AssetsSwitchesWorkspaceComposition(
            _assetsSwitchesCapabilityService,
            AssetsSwitchesViewHost,
            new AssetsSwitchesCompositionHost(
                ShowAssetsSwitchDeleteConfirmationDialogAsync));
        _assetsWorkspaceComposition = new AssetsWorkspaceComposition(
            AssetsOverviewViewHost,
            _assetsBaseDisksWorkspaceComposition,
            _assetsSwitchesWorkspaceComposition,
            AssetsSubviewTabView,
            AssetsOverviewTabViewItem,
            AssetsBaseDisksTabViewItem,
            AssetsSwitchesTabViewItem,
            new AssetsWorkspaceHost(),
            new AssetsWorkspaceShellBridge(
                () => IsAssetsCapabilityActive,
                () => IsAssetsOverviewActive,
                () => IsAssetsBaseDisksActive,
                () => IsAssetsSwitchesActive,
                NavigateToRoute));
        _templatesWorkspaceComposition = new TemplatesWorkspaceComposition(
            TemplatesWorkspacePanel,
            new TemplatesLibraryWorkspaceComposition(
                _templatesCapabilityService,
                TemplatesLibraryViewHost,
                new TemplatesLibraryWorkspaceHost(
                    () => _isTemplatesLoading,
                    SetTemplatesLoading,
                    ApplyTemplatesWorkspaceUiState,
                    ShowTemplateEditorAsync,
                    SetTemplateEditorStatus,
                    PickTemplateFileForOpenAsync,
                    PickTemplateFileForSaveAsync,
                    ShowDeleteTemplateConfirmationDialogAsync,
                    ReconcileDeployTemplateSelection)),
            new TemplatesEditorWorkspaceComposition(
                _templatesCapabilityService,
                TemplatesEditorViewHost,
                new TemplatesEditorWorkspaceHost(
                    () => _isTemplatesLoading,
                    SetTemplatesLoading,
                    ApplyTemplatesWorkspaceUiState,
                    EnsureTemplatesLibraryAsync,
                    LoadTemplateEditorReferenceDataAsync,
                    PickTemplateFileForSaveAsync,
                    ShowRemoveTemplateVmConfirmationDialogAsync,
                    NavigateToTemplatesEditor,
                    () => NavigateToRoute(ShellRouteKeys.TemplatesLibrary))),
            new TemplatesWorkspaceShellBridge(
                () => IsTemplatesCapabilityActive,
                () => IsTemplatesLibraryActive,
                () => IsTemplatesEditorActive));
        _deployFromTemplateWorkspaceComposition = new DeployFromTemplateWorkspaceComposition(
            DeployFromTemplateViewHost,
            DeployFromTemplateRightPanelViewHost,
            TemplatesLibraryItems,
            new DeployFromTemplateWorkspaceHost(
                () => _settingsStore.Settings,
                () => _templateAvailableSwitches,
                () => _deployFromTemplateWorkspaceComposition!.ActiveTemplateDocument,
                LoadDeployCatalogItems,
                EnsureTemplateSwitchesAsync,
                (context, mode) => _deploymentPreflightService.RunAsync(context, mode),
                issues =>
                {
                    _deployCompatibilityIssues.Clear();
                    _deployCompatibilityIssues.AddRange(issues);
                },
                () => _deployReadinessReport,
                report => _deployReadinessReport = report,
                async context =>
                {
                    await _deploymentCoordinator.DeployAllAsync(context);
                    return _deploymentOutcomeSummaryBuilder.Build(context);
                },
                AttachDeployProgressCallbacks));
        _deployOnTheFlyWorkspaceComposition = new DeployOnTheFlyWorkspaceComposition(
            DeployOnTheFlyViewHost,
            DeployOnTheFlyRightPanelViewHost,
            _deployOnTheFlyWorkspace,
            this);
        _deployWorkspaceComposition = new DeployWorkspaceComposition(
            DeployLocalNavigationPanel,
            DeployOverviewViewHost,
            _deployOnTheFlyWorkspaceComposition,
            DeploySubviewTabView,
            DeployOverviewTabViewItem,
            DeployQuickDeployTabViewItem,
            DeployFromTemplateTabViewItem,
            CreateDeployWorkspaceUiState,
            _deployFromTemplateWorkspaceComposition,
            new DeployWorkspaceShellBridge(
                () => IsDeployCapabilityActive,
                () => IsDeployOverviewActive,
                () => IsDeployOnTheFlyActive,
                () => IsDeployFromTemplateActive,
                NavigateToRoute));
        _diagnosticsWorkspaceComposition = new DiagnosticsWorkspaceComposition(
            DiagnosticsLocalNavigationPanel,
            DiagnosticsOverviewViewHost,
            DiagnosticsLogsViewHost,
            DiagnosticsSubviewTabView,
            DiagnosticsOverviewTabViewItem,
            DiagnosticsLogsTabViewItem,
            new DiagnosticsWorkspaceHost(App.Services.GetRequiredService<IStructuredLogViewerService>()),
            new DiagnosticsWorkspaceShellBridge(
                () => IsDiagnosticsCapabilityActive,
                () => IsDiagnosticsOverviewActive,
                () => IsDiagnosticsLogsActive,
                NavigateToRoute,
                TryOpenStructuredLogLocation));
        _deployOnTheFlyWorkspaceController = new DeployOnTheFlyWorkspaceController(_deployOnTheFlyWorkspace, this);
        _activeRouteKey = _shellViewModel.StartupRoute;
        _shellViewModel.TryResolveRoute(_activeRouteKey, out _activeCapability, out _activeSubview);
        WireDeployHandlers();
        ConfigureShellIcons();
        ConfigureNavigationView();
        ApplyShellNavigationMode(1280);
        Title = "LabAssistant.WinUI";
        SetInitialSize(1280, 800);
        RootLayout.KeyDown += RootLayout_KeyDown;
        RootLayout.SizeChanged += RootLayout_SizeChanged;
        InitializeRdpReadinessTimer();
        RootLayout.Loaded += async (_, _) =>
        {
            RootLayout.Focus(FocusState.Programmatic);
            await _machinesWorkspaceComposition.EnsureInventoryAsync(forceRefresh: true);
            await EnsureTemplateSwitchesAsync(forceRefresh: true);
            await EnsureTemplateVhdxCatalogOptionsAsync(forceRefresh: true);
            await _templatesWorkspaceComposition.EnsureLibraryAsync(forceRefresh: true);
            await _assetsBaseDisksWorkspaceComposition.EnsureInventoryAsync(forceRefresh: true);
            await LoadMachinesDeletionPolicyAsync();
        };
        ApplyState();
    }

    private void SetInitialSize(int width, int height)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow?.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    private void WireDeployHandlers()
    {
        DeployFromTemplateView.ReloadTemplatesRequested += DeployReloadTemplatesButton_Click;
        DeployFromTemplateView.EvaluateReadinessRequested += DeployEvaluateReadinessButton_Click;
        DeployFromTemplateView.ResolveSuggestionsRequested += DeployResolveSuggestionsButton_Click;
        DeployFromTemplateView.OpenTemplateEditorRequested += DeployOpenTemplateEditorButton_Click;
        DeployFromTemplateView.StartDeployRequested += DeployStartButton_Click;
        DeployFromTemplateView.TemplateSelectionChanged += DeployTemplateSelectorComboBox_SelectionChanged;
        DeployFromTemplateView.OpenResultsPanelRequested += DeployOpenResultsPanelButton_Click;
        UpdateDeployIssueRows();
    }

    private void ConfigureShellIcons()
    {
        HamburgerButton.Content = CreateIconGlyph(ShellIconToken.Menu);
        InsightsToggleButton.Content = CreateIconGlyph(ShellIconToken.Insights);
    }

    private void ConfigureNavigationView()
    {
        _routeToNavigationItem.Clear();
        _routeToCapabilityNavigationItem.Clear();
        GlobalNavigationView.MenuItems.Clear();
        GlobalNavigationView.FooterMenuItems.Clear();

        foreach (var capability in _shellViewModel.Capabilities)
        {
            var parentItem = new NavigationViewItem
            {
                Content = capability.DisplayName,
                Tag = capability.Key,
                Icon = new FontIcon { Glyph = capability.Glyph }
            };

            if (!capability.IsFooter)
            {
                foreach (var subview in capability.Subviews)
                {
                    if (!capability.ShowChildRoutesInShell)
                    {
                        _routeToNavigationItem[subview.RouteKey] = parentItem;
                        _routeToCapabilityNavigationItem[subview.RouteKey] = parentItem;
                        continue;
                    }

                    if (capability.HasOverview && string.Equals(subview.RouteKey, capability.DefaultSubview.RouteKey, StringComparison.Ordinal))
                    {
                        _routeToNavigationItem[subview.RouteKey] = parentItem;
                        _routeToCapabilityNavigationItem[subview.RouteKey] = parentItem;
                        continue;
                    }

                    var childItem = new NavigationViewItem
                    {
                        Content = subview.DisplayName,
                        Tag = subview.RouteKey
                    };
                    parentItem.MenuItems.Add(childItem);
                    _routeToNavigationItem[subview.RouteKey] = childItem;
                    _routeToCapabilityNavigationItem[subview.RouteKey] = parentItem;
                }
            }

            if (capability.IsFooter)
            {
                GlobalNavigationView.FooterMenuItems.Add(parentItem);
                _routeToNavigationItem[capability.DefaultSubview.RouteKey] = parentItem;
                _routeToCapabilityNavigationItem[capability.DefaultSubview.RouteKey] = parentItem;
            }
            else
            {
                GlobalNavigationView.MenuItems.Add(parentItem);
            }
        }
    }

    private TextBlock CreateIconGlyph(string token)
    {
        return new TextBlock
        {
            Text = ShellIconCatalog.GetGlyph(token),
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTopBarForegroundBrush"]
        };
    }

    private void ApplyState()
    {
        CurrentRouteTextBlock.Text = $"{_activeCapability.DisplayName} / {_activeSubview.DisplayName}";
        ContentTitleTextBlock.Text = _activeCapability.DisplayName;
        ContentDescriptionTextBlock.Text = IsMachinesOverviewActive
            ? "Manage host Hyper-V VMs. Start/stop/restart, open console, or delete with explicit scope."
            : IsDeployCapabilityActive
                ? "Configure and run deployment workflows from one capability surface with readiness, remediation, and results context."
            : IsAssetsCapabilityActive
                ? "Manage shared Hyper-V assets, inventory, and compatibility state from one capability surface."
            : IsTemplatesCapabilityActive
                ? "Browse templates and enter the editor through explicit create or edit workflows."
            : IsSettingsMachinesActive
                ? "Configure Machines policy defaults."
                : IsDiagnosticsCapabilityActive
                    ? "Inspect support-oriented diagnostics and structured log context from one capability surface."
                : $"Subview: {_activeSubview.DisplayName}. Placeholder content until capability migration lands.";
        ThemeToggleButton.Content = _theme == ElementTheme.Light ? "Switch to dark" : "Switch to light";
        RootLayout.RequestedTheme = _theme;
        ApplyRightPanelState();
        MachinesOverviewPanel.Visibility = IsMachinesOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        AssetsLocalNavPanel.Visibility = IsAssetsCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        AssetsOverviewPanel.Visibility = IsAssetsOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        AssetsBaseDisksPanel.Visibility = IsAssetsBaseDisksActive ? Visibility.Visible : Visibility.Collapsed;
        AssetsSwitchesPanel.Visibility = IsAssetsSwitchesActive ? Visibility.Visible : Visibility.Collapsed;
        ApplyTemplatesWorkspaceUiState();
        SettingsMachinesPanel.Visibility = IsSettingsMachinesActive ? Visibility.Visible : Visibility.Collapsed;
        NonMachinesPlaceholderTextBlock.Visibility = (IsMachinesOverviewActive || IsDeployCapabilityActive || IsAssetsCapabilityActive || IsTemplatesCapabilityActive || IsSettingsMachinesActive || IsDiagnosticsCapabilityActive) ? Visibility.Collapsed : Visibility.Visible;

        QueueNavigationSelectionUpdate();

        _machinesWorkspaceComposition.ApplyShellState();
        _deployWorkspaceComposition.ApplyShellState();
        _assetsWorkspaceComposition.ApplyShellState();
        _templatesWorkspaceComposition.ApplyShellState();
        _diagnosticsWorkspaceComposition.ApplyShellState();
        if (IsSettingsMachinesActive)
        {
            _ = LoadMachinesDeletionPolicyAsync();
        }

        if (IsDeployFromTemplateActive)
        {
            _ = EnsureDeployTemplatesLoadedAsync(forceRefresh: false);
            UpdateDeployUi();
        }

    }

    private void RootLayout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyShellNavigationMode(e.NewSize.Width);

        var isCompact = e.NewSize.Width < ShellRightPanelCompactThreshold;
        if (_isShellRightPanelInCompactFallback == isCompact)
        {
            return;
        }

        _isShellRightPanelInCompactFallback = isCompact;
        if (_isShellRightPanelInCompactFallback)
        {
            _isShellRightPanelOpen = false;
        }

        ApplyRightPanelState();
    }

    private void ApplyShellNavigationMode(double width)
    {
        var useDrawerMode = width < ShellNavigationDrawerThreshold;
        GlobalNavigationView.PaneDisplayMode = useDrawerMode
            ? NavigationViewPaneDisplayMode.LeftMinimal
            : NavigationViewPaneDisplayMode.LeftCompact;
        GlobalNavigationView.CompactPaneLength = useDrawerMode ? 0 : 56;

        if (useDrawerMode)
        {
            GlobalNavigationView.IsPaneOpen = false;
        }
    }

    private void ResetRightPanelForCapabilitySwitch(string incomingCapabilityKey)
    {
        _shellRightPanelOwnerCapabilityKey = ResolveRightPanelOwnerCapabilityKey(incomingCapabilityKey);
        _isShellRightPanelOpen = false;
        _deployFromTemplateWorkspaceComposition.ResetPanelState();
    }

    private string ResolveRightPanelOwnerCapabilityKey(string capabilityKey)
    {
        return string.Equals(capabilityKey, DeployCapabilityKey, StringComparison.Ordinal)
            ? DeployCapabilityKey
            : string.Empty;
    }

    private bool CanActiveCapabilityOwnRightPanel()
    {
        return string.Equals(_shellRightPanelOwnerCapabilityKey, DeployCapabilityKey, StringComparison.Ordinal);
    }

    private bool ShouldOwnerAutoOpenRightPanel()
    {
        if (!CanActiveCapabilityOwnRightPanel())
        {
            return false;
        }

        return _deployFromTemplateWorkspaceComposition.IsStarting ||
            _deployOnTheFlyWorkspace.IsStarting ||
            string.Equals(_deployFromTemplateWorkspaceComposition.LifecycleState, "Running", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_deployOnTheFlyWorkspace.LifecycleState, "Running", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyRightPanelState()
    {
        if (RootLayout.ActualWidth > 0)
        {
            _isShellRightPanelInCompactFallback = RootLayout.ActualWidth < ShellRightPanelCompactThreshold;
        }

        _shellRightPanelOwnerCapabilityKey = ResolveRightPanelOwnerCapabilityKey(_activeCapability.Key);
        if (_isShellRightPanelInCompactFallback && _isShellRightPanelOpen)
        {
            _isShellRightPanelOpen = false;
        }

        if (ShouldOwnerAutoOpenRightPanel())
        {
            _isShellRightPanelOpen = true;
        }

        var hasOwner = CanActiveCapabilityOwnRightPanel();
        var showPanel = hasOwner && _isShellRightPanelOpen && !_isShellRightPanelInCompactFallback;
        InsightsPanel.Visibility = showPanel ? Visibility.Visible : Visibility.Collapsed;
        ShellRightPanelColumn.Width = showPanel ? new GridLength(ShellRightPanelExpandedWidth) : new GridLength(0);
        InsightsToggleButton.IsEnabled = hasOwner && !_isShellRightPanelInCompactFallback;
        InsightsToggleButton.Opacity = InsightsToggleButton.IsEnabled ? 1.0 : 0.45;
        ToolTipService.SetToolTip(InsightsToggleButton, "Toggle progress and results panel");
        RightPanelTitleTextBlock.Text = IsDeployFromTemplateActive
            ? "From Template Progress / Results"
            : IsDeployOnTheFlyActive
                ? "Quick Deploy Progress / Results"
                : "Details";
        DeployFromTemplateRightPanel.Visibility = IsDeployFromTemplateActive && showPanel ? Visibility.Visible : Visibility.Collapsed;
        RightPanelEmptyStateBorder.Visibility = (!IsDeployFromTemplateActive && !IsDeployOnTheFlyActive && showPanel)
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateDeployRightPanelLaunchers(showPanel);
        IssueBadge.Visibility = Visibility.Collapsed;
        IssueBadgeTextBlock.Text = string.Empty;
    }

    private void UpdateDeployRightPanelLaunchers(bool showPanel)
    {
        var panelUnavailable = _isShellRightPanelInCompactFallback;
        var fromTemplateIsRunning = _deployFromTemplateWorkspaceComposition.IsStarting || string.Equals(_deployFromTemplateWorkspaceComposition.LifecycleState, "Running", StringComparison.OrdinalIgnoreCase);

        _deployFromTemplateWorkspaceComposition.SetResultsPanelLauncherState(
            showPanel && IsDeployFromTemplateActive ? "Hide Progress / Results" : "Open Progress / Results",
            IsDeployFromTemplateActive && !panelUnavailable,
            panelUnavailable
            ? "Expand the window to review the progress and results panel."
            : fromTemplateIsRunning
                ? "The panel auto-opens while deployment runs and stays available for result review."
                : _deployFromTemplateWorkspaceComposition.ResultRowCount > 0
                    ? $"{_deployFromTemplateWorkspaceComposition.ResultRowCount} VM result row(s) are available for review."
                    : "Use the side panel during or after deploy for progress, timeline, and results.");

        _deployOnTheFlyWorkspaceComposition.ApplyResultsPanelState(IsDeployOnTheFlyActive, showPanel, panelUnavailable);
    }

    private void NavigateToRoute(string routeKey)
    {
        if (!_shellViewModel.TryResolveRoute(routeKey, out var capability, out var subview))
        {
            return;
        }

        var changedCapability = !string.Equals(_activeCapability.Key, capability.Key, StringComparison.Ordinal);
        var changedSubview = !string.Equals(_activeSubview.RouteKey, subview.RouteKey, StringComparison.Ordinal);
        if (!changedCapability && !changedSubview)
        {
            return;
        }

        if (changedCapability || changedSubview)
        {
            _machinesWorkspaceComposition.DiscardEditDraft();
        }

        if (changedCapability)
        {
            ResetRightPanelForCapabilitySwitch(capability.Key);
        }

        _activeCapability = capability;
        _activeSubview = subview;
        _activeRouteKey = subview.RouteKey;
        ApplyState();

        if (IsMachinesOverviewActive)
        {
            _ = _machinesWorkspaceComposition.EnsureInventoryAsync(forceRefresh: false);
        }
    }

    private void HamburgerButton_Click(object sender, RoutedEventArgs e)
    {
        GlobalNavigationView.IsPaneOpen = !GlobalNavigationView.IsPaneOpen;
    }

    private void GlobalNavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (_isUpdatingNavigationSelection)
        {
            return;
        }

        if (args.InvokedItemContainer is not NavigationViewItem invokedItem)
        {
            return;
        }

        if (invokedItem.Tag is not string key)
        {
            return;
        }

        if (_shellViewModel.TryResolveCapability(key, out var capability))
        {
            NavigateToRoute(capability.DefaultSubview.RouteKey);
            return;
        }

        NavigateToRoute(key);
    }

    private void QueueNavigationSelectionUpdate()
    {
        if (!_routeToCapabilityNavigationItem.TryGetValue(_activeRouteKey, out var selectedNavigationItem))
        {
            return;
        }

        if (ReferenceEquals(GlobalNavigationView.SelectedItem, selectedNavigationItem))
        {
            return;
        }

        _isUpdatingNavigationSelection = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                GlobalNavigationView.SelectedItem = selectedNavigationItem;
            }
            finally
            {
                _isUpdatingNavigationSelection = false;
            }
        });
    }

    private void InsightsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanActiveCapabilityOwnRightPanel())
        {
            return;
        }

        _isShellRightPanelOpen = !_isShellRightPanelOpen;
        ApplyRightPanelState();
    }

    private void DeployOpenResultsPanelButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleDeployRightPanelFromWorkflow();
    }

    int IDeployOnTheFlyCompositionHost.VmEntryCount => _deployOnTheFlyWorkspace.VmEntryCount;

    DeploymentReadinessReport? IDeployOnTheFlyCompositionHost.ReadinessReport => _deployOnTheFlyWorkspace.ReadinessReport;

    bool IDeployOnTheFlyCompositionHost.IsEvaluatingReadiness => _deployOnTheFlyWorkspace.IsEvaluatingReadiness;

    bool IDeployOnTheFlyCompositionHost.IsStarting => _deployOnTheFlyWorkspace.IsStarting;

    string IDeployOnTheFlyCompositionHost.LifecycleState => _deployOnTheFlyWorkspace.LifecycleState;

    void IDeployOnTheFlyCompositionHost.EnsureSeeded() => EnsureDeployOnTheFlySeeded();

    Task IDeployOnTheFlyCompositionHost.EnsureReferenceDataAsync(bool forceRefresh) => EnsureDeployOnTheFlyReferenceDataAsync(forceRefresh);

    void IDeployOnTheFlyCompositionHost.UpdateUi() => UpdateDeployOnTheFlyUi();

    void IDeployOnTheFlyCompositionHost.ScheduleAutoEvaluate() => ScheduleDeployOnTheFlyAutoEvaluate();

    void IDeployOnTheFlyCompositionHost.OnVmEntriesSelectionChanged(DeployOnTheFlyVmEntryRow? selectedRow)
    {
        _deployOnTheFlyWorkspace.SetSelectedVmEntry(selectedRow?.VmEntry);
        UpdateDeployOnTheFlyEditorPanel();
        UpdateDeployOnTheFlyUi();
    }

    Task IDeployOnTheFlyCompositionHost.OnVmRemoveRequestedAsync(VmTemplate vmEntry) => RemoveDeployOnTheFlyVmEntryAsync(vmEntry);

    void IDeployOnTheFlyCompositionHost.OnAddVmRequested() => AddDeployOnTheFlyVmEntry();

    Task IDeployOnTheFlyCompositionHost.OnRemoveSelectedVmRequestedAsync() => RemoveDeployOnTheFlyVmEntryAsync(_deployOnTheFlyWorkspace.SelectedVmEntry);

    void IDeployOnTheFlyCompositionHost.OnApplyVmChangesRequested() => ApplyDeployOnTheFlyVmChanges();

    void IDeployOnTheFlyCompositionHost.OnEditorInteractionChanged(DeployOnTheFlyEditorInteractionState interactionState)
    {
        if (_deployOnTheFlyWorkspace.IsSynchronizingEditorDraft)
        {
            return;
        }

        SyncDeployOnTheFlyEditorDraft(interactionState);
        UpdateDeployOnTheFlyUi();
        ScheduleDeployOnTheFlyAutoEvaluate();
    }

    Task IDeployOnTheFlyCompositionHost.OnEvaluateRequestedAsync() => EvaluateDeployOnTheFlyReadinessAsync(DeploymentPreflightMode.Full);

    Task IDeployOnTheFlyCompositionHost.OnResolveSuggestionsRequestedAsync() => ResolveDeployOnTheFlySuggestionsAsync();

    Task IDeployOnTheFlyCompositionHost.OnOpenTemplateEditorRequestedAsync() => OpenDeployOnTheFlyTemplateEditorAsync();

    Task IDeployOnTheFlyCompositionHost.OnStartRequestedAsync() => _deployOnTheFlyWorkspaceController.StartDeployAsync();

    void IDeployOnTheFlyCompositionHost.OnOpenResultsPanelRequested() => ToggleDeployRightPanelFromWorkflow();

    private void ToggleDeployRightPanelFromWorkflow()
    {
        if (!CanActiveCapabilityOwnRightPanel() || _isShellRightPanelInCompactFallback)
        {
            return;
        }

        _isShellRightPanelOpen = !_isShellRightPanelOpen;
        ApplyRightPanelState();
    }

    private void CloseRightPanelButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isShellRightPanelOpen)
        {
            return;
        }

        _isShellRightPanelOpen = false;
        ApplyRightPanelState();
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _theme = _theme == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;
        ApplyState();
    }

    private async void SaveMachinesDeletionPolicyButton_Click(object sender, RoutedEventArgs e)
    {
        if (MachinesDeletionPolicyComboBox.SelectedItem is not ComboBoxItem selectedItem ||
            selectedItem.Tag is not string modeRaw ||
            !Enum.TryParse<MachineDeletionPolicyMode>(modeRaw, ignoreCase: true, out var mode))
        {
            MachinesDeletionPolicyStatusTextBlock.Text = "Select a deletion policy mode first.";
            return;
        }

        _isSavingDeletionPolicy = true;
        SaveMachinesDeletionPolicyButton.IsEnabled = false;
        try
        {
            await _machinesCapabilityService.SetDeletionPolicyAsync(mode);
            MachinesDeletionPolicyStatusTextBlock.Text = $"Saved: {selectedItem.Content}";
        }
        catch (Exception ex)
        {
            MachinesDeletionPolicyStatusTextBlock.Text = $"Failed to save policy. {ex.Message}";
        }
        finally
        {
            _isSavingDeletionPolicy = false;
            SaveMachinesDeletionPolicyButton.IsEnabled = true;
        }
    }

    private void RootLayout_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && GlobalNavigationView.IsPaneOpen)
        {
            GlobalNavigationView.IsPaneOpen = false;
            e.Handled = true;
        }
    }

    private void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (GlobalNavigationView.IsPaneOpen)
        {
            GlobalNavigationView.IsPaneOpen = false;
            args.Handled = true;
        }
    }

    private bool IsMachinesOverviewActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.MachinesOverview, StringComparison.Ordinal);

    private bool IsDeployOverviewActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.DeployOverview, StringComparison.Ordinal);

    private bool IsDeployFromTemplateActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.DeployFromTemplate, StringComparison.Ordinal);

    private bool IsDeployOnTheFlyActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.DeployOnTheFly, StringComparison.Ordinal);

    private bool IsDeployCapabilityActive =>
        IsDeployOverviewActive || IsDeployFromTemplateActive || IsDeployOnTheFlyActive;

    private void EnsureDeployOnTheFlySeeded()
    {
        _deployOnTheFlyWorkspace.EnsureSeeded(_deployOnTheFlyWorkspace.SelectedVmEntry?.VmId);
        _deployWorkspaceComposition.RefreshSharedUiState();
        SelectDeployOnTheFlyVmEntry(_deployOnTheFlyWorkspace.SelectedVmEntry);
    }

    private LabTemplate BuildOnTheFlyTemplate()
    {
        return new LabTemplate
        {
            Name = "Quick Deploy Draft",
            Description = "Generated quick deploy input.",
            VmTemplates = _deployOnTheFlyWorkspace.CreateTemplateSnapshot().ToList()
        };
    }

    private void ReplaceDeployOnTheFlyEntriesFromTemplate(LabTemplate template)
    {
        _deployOnTheFlyWorkspace.ReplaceEntriesFromTemplate(
            template,
            _deployOnTheFlyWorkspace.SelectedVmEntry?.VmId);
        _deployWorkspaceComposition.RefreshSharedUiState();
        SelectDeployOnTheFlyVmEntry(_deployOnTheFlyWorkspace.SelectedVmEntry);
        UpdateDeployOnTheFlyEditorPanel();
    }

    private void SelectDeployOnTheFlyVmEntry(VmTemplate? vmEntry)
    {
        if (vmEntry is null)
        {
            DeployOnTheFlyViewHost.SetVmEntrySelection(null);
            return;
        }

        var selectedRow = _deployOnTheFlyWorkspace.FindRow(vmEntry);
        DeployOnTheFlyViewHost.SetVmEntrySelection(selectedRow);
    }

    private void UpdateDeployOnTheFlyEditorPanel()
    {
        DeployOnTheFlyViewHost.ApplyEditorViewState(BuildDeployOnTheFlyEditorViewState());
    }

    private DeployOnTheFlyEditorViewState BuildDeployOnTheFlyEditorViewState()
    {
        var switchItems = new List<object> { DeployOnTheFlySwitchPlaceholder };
        switchItems.AddRange(_templateAvailableSwitches);

        var vhdItems = new List<object> { DeployOnTheFlyVhdxPlaceholder };
        vhdItems.AddRange(_templateVhdxCatalogOptions);

        if (_deployOnTheFlyWorkspace.SelectedVmEntry is null)
        {
            return new DeployOnTheFlyEditorViewState(
                _deployOnTheFlyWorkspace.EditorVmNameDraft,
                _deployOnTheFlyWorkspace.EditorVmMemoryDraft,
                _deployOnTheFlyWorkspace.EditorVmCpuDraft,
                switchItems,
                DeployOnTheFlySwitchPlaceholder,
                "Select a VM entry first.",
                vhdItems,
                DeployOnTheFlyVhdxPlaceholder,
                "Select a VM entry first.");
        }

        object selectedSwitchItem;
        string switchGuidanceText;
        var selectedSwitch = _deployOnTheFlyWorkspace.EditorSwitchNameDraft;
        if (!string.IsNullOrWhiteSpace(selectedSwitch) &&
            _templateAvailableSwitches.Contains(selectedSwitch, StringComparer.OrdinalIgnoreCase))
        {
            selectedSwitchItem = _templateAvailableSwitches.First(name =>
                string.Equals(name, selectedSwitch, StringComparison.OrdinalIgnoreCase));
            switchGuidanceText = "Switch selected from host inventory.";
        }
        else
        {
            selectedSwitchItem = DeployOnTheFlySwitchPlaceholder;
            switchGuidanceText = _templateAvailableSwitches.Count == 0
                ? "No host switches available. Add a switch in Assets first."
                : "Switch selection is optional.";
        }

        var vhdSelection = string.IsNullOrWhiteSpace(_deployOnTheFlyWorkspace.EditorVhdxIdDraft)
            ? null
            : _templateVhdxCatalogOptions.FirstOrDefault(option =>
                string.Equals(option.Id, _deployOnTheFlyWorkspace.EditorVhdxIdDraft, StringComparison.OrdinalIgnoreCase));
        if (vhdSelection is null && !string.IsNullOrWhiteSpace(_deployOnTheFlyWorkspace.EditorVhdPathDraft))
        {
            vhdSelection = _templateVhdxCatalogOptions.FirstOrDefault(option =>
                string.Equals(option.Path, _deployOnTheFlyWorkspace.EditorVhdPathDraft, StringComparison.OrdinalIgnoreCase));
        }

        object selectedVhdxCatalogItem;
        string vhdxGuidanceText;
        if (vhdSelection is not null)
        {
            selectedVhdxCatalogItem = vhdSelection;
            vhdxGuidanceText = $"Selected: {vhdSelection.DisplayLabel} ({vhdSelection.Id}).";
        }
        else
        {
            selectedVhdxCatalogItem = DeployOnTheFlyVhdxPlaceholder;
            vhdxGuidanceText = _templateVhdxCatalogOptions.Count == 0
                ? "No VHDX catalog entries available. Import base disks in Assets first."
                : "Select a base disk from catalog.";
        }

        return new DeployOnTheFlyEditorViewState(
            _deployOnTheFlyWorkspace.EditorVmNameDraft,
            _deployOnTheFlyWorkspace.EditorVmMemoryDraft,
            _deployOnTheFlyWorkspace.EditorVmCpuDraft,
            switchItems,
            selectedSwitchItem,
            switchGuidanceText,
            vhdItems,
            selectedVhdxCatalogItem,
            vhdxGuidanceText);
    }

    private void SyncDeployOnTheFlyEditorDraft(DeployOnTheFlyEditorInteractionState interactionState)
    {
        var selectedSwitch = interactionState.SelectedSwitchItem is string switchName &&
                             !string.Equals(switchName, DeployOnTheFlySwitchPlaceholder, StringComparison.Ordinal)
            ? switchName
            : null;

        var selectedCatalogOption = interactionState.SelectedVhdxCatalogOption;
        _deployOnTheFlyWorkspace.UpdateEditorDraft(
            interactionState.VmName,
            interactionState.VmMemoryText,
            interactionState.VmCpuText,
            selectedSwitch,
            selectedCatalogOption?.Id,
            selectedCatalogOption?.Path,
            selectedCatalogOption?.Signature);
    }

    private bool TryApplyDeployOnTheFlyVmFields(bool showSuccessStatus, bool showValidationErrors = true)
    {
        if (_deployOnTheFlyWorkspace.SelectedVmEntry is null || _deployOnTheFlyWorkspace.IsSynchronizingEditorDraft)
        {
            return false;
        }

        var vmName = _deployOnTheFlyWorkspace.EditorVmNameDraft.Trim();
        if (string.IsNullOrWhiteSpace(vmName))
        {
            if (showValidationErrors)
            {
                SetDeployOnTheFlyStatusText("VM name is required.");
            }
            return false;
        }

        if (!int.TryParse(_deployOnTheFlyWorkspace.EditorVmMemoryDraft, out var memoryMb) || memoryMb <= 0)
        {
            if (showValidationErrors)
            {
                SetDeployOnTheFlyStatusText("Memory must be a positive integer.");
            }
            return false;
        }

        if (!int.TryParse(_deployOnTheFlyWorkspace.EditorVmCpuDraft, out var cpuCount) || cpuCount <= 0)
        {
            if (showValidationErrors)
            {
                SetDeployOnTheFlyStatusText("CPU count must be a positive integer.");
            }
            return false;
        }

        var previousName = _deployOnTheFlyWorkspace.ApplyEditorDraftToSelectedVm();

        if (showSuccessStatus)
        {
            SetDeployOnTheFlyStatusText($"Updated '{vmName}'.");
        }

        if (!string.Equals(previousName, vmName, StringComparison.Ordinal))
        {
            RefreshDeployOnTheFlyVmEntriesList();
        }

        return true;
    }

    private void RefreshDeployOnTheFlyVmEntriesList()
    {
        _deployOnTheFlyWorkspace.RefreshVmEntryRows();
        SelectDeployOnTheFlyVmEntry(_deployOnTheFlyWorkspace.SelectedVmEntry);
    }

    private async Task EvaluateDeployOnTheFlyReadinessAsync(DeploymentPreflightMode mode)
    {
        if (!_deployOnTheFlyWorkspace.IsStarting)
        {
            _deployOnTheFlyWorkspace.SetShowAllVmRows(false);
        }
        if (!TryApplyDeployOnTheFlyVmFields(showSuccessStatus: false) && _deployOnTheFlyWorkspace.SelectedVmEntry is not null)
        {
            return;
        }

        if (_deployOnTheFlyWorkspace.VmEntryCount == 0)
        {
            SetDeployOnTheFlyStatusText("Add at least one VM entry first.");
            return;
        }

        _deployOnTheFlyWorkspace.BeginReadinessEvaluation();
        _deployOnTheFlyWorkspace.SetWorkflowState(
            lifecycleState: "Evaluating",
            progressPercent: mode == DeploymentPreflightMode.Full ? 18 : 12,
            progressSummary: mode == DeploymentPreflightMode.Full
                ? "Running full readiness checks..."
                : "Running quick readiness checks...");
        SetDeployOnTheFlyStatusText(
            mode == DeploymentPreflightMode.Full
                ? "Running full quick deploy readiness evaluation..."
                : "Running quick deploy readiness evaluation...");

        try
        {
            await EnsureTemplateSwitchesAsync(forceRefresh: false);
            var template = BuildOnTheFlyTemplate();
            var deployContext = DeployContextBuilder.Build(template, _settingsStore.Settings, LoadDeployCatalogItems(), _templateAvailableSwitches);
            var readinessReport = await _deploymentPreflightService.RunAsync(deployContext.MultiVmContext, mode);

            var blockingCount = deployContext.CompatibilityIssues.Count(issue => issue.IsBlocking) +
                                readinessReport.Results.Count(result => result.Status == DeploymentReadinessStatus.Fail);
            var warningCount = deployContext.CompatibilityIssues.Count(issue => !issue.IsBlocking) +
                               readinessReport.Results.Count(result => result.Status == DeploymentReadinessStatus.Warn);
            var readinessSummaryText = blockingCount > 0
                ? $"Readiness blocked ({blockingCount} fail, {warningCount} warn)."
                : warningCount > 0
                    ? $"Readiness passed with warnings ({warningCount})."
                    : "Readiness passed.";

            _deployOnTheFlyWorkspace.ApplyReadinessResult(
                deployContext.CompatibilityIssues,
                readinessReport,
                readinessSummaryText);

            _deployOnTheFlyWorkspace.SetWorkflowState(
                lifecycleState: blockingCount > 0 ? "Blocked" : warningCount > 0 ? "Warning" : "Ready",
                progressPercent: blockingCount > 0 ? 35 : warningCount > 0 ? 45 : 55,
                progressSummary: blockingCount > 0
                    ? "Readiness blocked."
                    : warningCount > 0
                        ? "Readiness passed with warnings."
                        : "Readiness passed.");
            SetDeployOnTheFlyStatusText(
                blockingCount > 0
                    ? "Deploy blocked by readiness failures. Resolve blocking items first."
                    : warningCount > 0
                        ? $"Readiness passed with {warningCount} warning(s)."
                        : "Readiness passed with no issues.");
        }
        catch (Exception ex)
        {
            _deployOnTheFlyWorkspace.SetReadinessEvaluationFailed("Readiness evaluation failed.");
            _deployOnTheFlyWorkspace.SetWorkflowState("Error", 0, "Readiness evaluation failed.");
            SetDeployOnTheFlyStatusText($"Readiness evaluation failed. {ex.Message}");
        }
        finally
        {
            UpdateDeployOnTheFlyUi();
        }
    }

    private void ScheduleDeployOnTheFlyAutoEvaluate()
    {
        if (_deployOnTheFlyWorkspace.IsSynchronizingEditorDraft || _deployOnTheFlyWorkspace.IsStarting)
        {
            return;
        }

        var nonce = Interlocked.Increment(ref _deployOnTheFlyAutoEvaluateNonce);
        _ = DebouncedDeployOnTheFlyAutoEvaluateAsync(nonce);
    }

    private async Task DebouncedDeployOnTheFlyAutoEvaluateAsync(int nonce)
    {
        await Task.Delay(350);
        if (nonce != _deployOnTheFlyAutoEvaluateNonce || _deployOnTheFlyWorkspace.IsStarting || _deployOnTheFlyWorkspace.IsEvaluatingReadiness)
        {
            return;
        }

        if (!TryApplyDeployOnTheFlyVmFields(showSuccessStatus: false, showValidationErrors: false))
        {
            return;
        }

        _deployOnTheFlyWorkspace.ClearReadinessState("Readiness has not been evaluated.");
        _deployOnTheFlyWorkspace.ResetProgressState();
        UpdateDeployOnTheFlyUi();

        await EvaluateDeployOnTheFlyReadinessAsync(DeploymentPreflightMode.Full);
    }

    private void UpdateDeployOnTheFlyVmEntryRows()
    {
        _deployOnTheFlyWorkspace.RefreshVmEntryRows();

        var compatibilityByVm = _deployOnTheFlyWorkspace.CompatibilityIssues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.VmName))
            .GroupBy(issue => issue.VmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var readinessByVm = (_deployOnTheFlyWorkspace.ReadinessReport?.Results ?? [])
            .SelectMany(result => result.AffectedVmNames.Select(vmName => (vmName, result)))
            .Where(tuple => !string.IsNullOrWhiteSpace(tuple.vmName))
            .GroupBy(tuple => tuple.vmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.result).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in _deployOnTheFlyWorkspace.VmEntryRows)
        {
            row.DisplayName = string.IsNullOrWhiteSpace(row.VmEntry.Name) ? "Unnamed VM" : row.VmEntry.Name.Trim();
            row.SecondaryText = BuildDeployOnTheFlyVmEntrySecondaryText(row.VmEntry);

            compatibilityByVm.TryGetValue(row.VmEntry.Name ?? string.Empty, out var compatibilityIssues);
            readinessByVm.TryGetValue(row.VmEntry.Name ?? string.Empty, out var readinessIssues);
            compatibilityIssues ??= [];
            readinessIssues ??= [];

            var draftIssues = ReferenceEquals(row.VmEntry, _deployOnTheFlyWorkspace.SelectedVmEntry)
                ? GetDeployOnTheFlyDraftIssues()
                : GetDeployOnTheFlyVmEntryIssues(row.VmEntry);

            var blockingMessages = new List<string>();
            var warningMessages = new List<string>();

            blockingMessages.AddRange(draftIssues.Where(issue => issue.IsBlocking).Select(issue => issue.Message));
            warningMessages.AddRange(draftIssues.Where(issue => !issue.IsBlocking).Select(issue => issue.Message));

            blockingMessages.AddRange(compatibilityIssues.Where(issue => issue.IsBlocking).Select(issue => FormatDeployOnTheFlyIssueMessage(issue.Message, issue.Guidance)));
            warningMessages.AddRange(compatibilityIssues.Where(issue => !issue.IsBlocking).Select(issue => FormatDeployOnTheFlyIssueMessage(issue.Message, issue.Guidance)));

            blockingMessages.AddRange(readinessIssues.Where(issue => issue.Status == DeploymentReadinessStatus.Fail).Select(issue => FormatDeployOnTheFlyIssueMessage(issue.Message, issue.ActionableGuidance)));
            warningMessages.AddRange(readinessIssues.Where(issue => issue.Status == DeploymentReadinessStatus.Warn).Select(issue => FormatDeployOnTheFlyIssueMessage(issue.Message, issue.ActionableGuidance)));

            if (blockingMessages.Count > 0)
            {
                row.IssueBadgeText = "Blocked";
                row.IssueSummary = blockingMessages[0];
                row.IssueBrush = Application.Current.Resources["ShellCriticalBrush"] as Microsoft.UI.Xaml.Media.Brush;
                row.IssueBadgeVisibility = Visibility.Visible;
                row.IssueSummaryVisibility = Visibility.Visible;
            }
            else if (warningMessages.Count > 0)
            {
                row.IssueBadgeText = "Warning";
                row.IssueSummary = warningMessages[0];
                row.IssueBrush = Application.Current.Resources["ShellWarnBrush"] as Microsoft.UI.Xaml.Media.Brush;
                row.IssueBadgeVisibility = Visibility.Visible;
                row.IssueSummaryVisibility = Visibility.Visible;
            }
            else
            {
                row.IssueBadgeText = string.Empty;
                row.IssueSummary = string.Empty;
                row.IssueBrush = Application.Current.Resources["ShellTextSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush;
                row.IssueBadgeVisibility = Visibility.Collapsed;
                row.IssueSummaryVisibility = Visibility.Collapsed;
            }
        }
    }

    private string BuildDeployOnTheFlyEditorIssueSummaryText()
    {
        if (_deployOnTheFlyWorkspace.SelectedVmEntry is null)
        {
            return "Select a VM entry to review its properties and resolve any issues inline.";
        }

        var draftIssues = GetDeployOnTheFlyDraftIssues();
        if (draftIssues.Count > 0)
        {
            var blockingCount = draftIssues.Count(issue => issue.IsBlocking);
            return blockingCount > 0
                ? $"Blocking issues in this VM: {string.Join(" ", draftIssues.Where(issue => issue.IsBlocking).Select(issue => issue.Message))}"
                : $"Warnings in this VM: {string.Join(" ", draftIssues.Select(issue => issue.Message))}";
        }

        var selectedRow = _deployOnTheFlyWorkspace.FindRow(_deployOnTheFlyWorkspace.SelectedVmEntry);
        if (selectedRow is not null && selectedRow.IssueSummaryVisibility == Visibility.Visible)
        {
            return $"{selectedRow.IssueBadgeText}: {selectedRow.IssueSummary}";
        }

        return "Ready. Changes validate while you edit. Row signals show which VM needs attention.";
    }

    private List<(bool IsBlocking, string Message)> GetDeployOnTheFlyDraftIssues()
    {
        if (_deployOnTheFlyWorkspace.SelectedVmEntry is null)
        {
            return [];
        }

        return GetDeployOnTheFlyDraftIssues(
            _deployOnTheFlyWorkspace.EditorVmNameDraft,
            _deployOnTheFlyWorkspace.EditorVmMemoryDraft,
            _deployOnTheFlyWorkspace.EditorVmCpuDraft,
            string.IsNullOrWhiteSpace(_deployOnTheFlyWorkspace.EditorVhdxIdDraft) &&
            string.IsNullOrWhiteSpace(_deployOnTheFlyWorkspace.EditorVhdPathDraft)
                ? null
                : new object(),
            _templateVhdxCatalogOptions.Count);
    }

    private static List<(bool IsBlocking, string Message)> GetDeployOnTheFlyVmEntryIssues(VmTemplate vmEntry)
    {
        var memoryText = vmEntry.MemoryMb.ToString(CultureInfo.InvariantCulture);
        var cpuText = vmEntry.CpuCount.ToString(CultureInfo.InvariantCulture);
        var selectedCatalog = string.IsNullOrWhiteSpace(vmEntry.VhdxId) && string.IsNullOrWhiteSpace(vmEntry.VhdPath)
            ? null
            : new object();

        return GetDeployOnTheFlyDraftIssues(vmEntry.Name, memoryText, cpuText, selectedCatalog, availableCatalogCount: 1);
    }

    private static List<(bool IsBlocking, string Message)> GetDeployOnTheFlyDraftIssues(
        string? vmName,
        string? memoryText,
        string? cpuText,
        object? selectedCatalogItem,
        int availableCatalogCount)
    {
        var issues = new List<(bool IsBlocking, string Message)>();
        if (string.IsNullOrWhiteSpace(vmName))
        {
            issues.Add((true, "VM name is required."));
        }

        if (!int.TryParse(memoryText, out var memoryMb) || memoryMb <= 0)
        {
            issues.Add((true, "Memory must be a positive integer."));
        }

        if (!int.TryParse(cpuText, out var cpuCount) || cpuCount <= 0)
        {
            issues.Add((true, "CPU count must be a positive integer."));
        }

        if (selectedCatalogItem is null)
        {
            issues.Add((true, availableCatalogCount == 0
                ? "Import a base disk in Assets before deploy."
                : "Select a base disk in VM Properties."));
        }

        return issues;
    }

    private static string BuildDeployOnTheFlyVmEntrySecondaryText(VmTemplate vmEntry)
    {
        var diskText = string.IsNullOrWhiteSpace(vmEntry.VhdxId) && string.IsNullOrWhiteSpace(vmEntry.VhdPath)
            ? "No base disk"
            : string.IsNullOrWhiteSpace(vmEntry.VhdxId)
                ? "Catalog disk selected"
                : $"Disk: {vmEntry.VhdxId}";
        var switchText = vmEntry.SwitchNames?.FirstOrDefault()
                         ?? vmEntry.SwitchName
                         ?? "No switch";
        return $"{vmEntry.MemoryMb} MB | {vmEntry.CpuCount} vCPU | {diskText} | Switch: {switchText}";
    }

    private static string FormatDeployOnTheFlyIssueMessage(string message, string? guidance)
    {
        return string.IsNullOrWhiteSpace(guidance) ? message.Trim() : $"{message} {guidance}".Trim();
    }

    private void SetDeployOnTheFlyStatusText(string statusText)
    {
        _deployOnTheFlyStatusText = statusText;

        if (Content is not null)
        {
            UpdateDeployOnTheFlyUi();
        }
    }

    private void UpdateDeployOnTheFlyUi()
    {
        var hasEntries = _deployOnTheFlyWorkspace.VmEntryCount > 0;
        var hasBlockingFailures = _deployOnTheFlyWorkspace.HasBlockingFailures;

        if (!hasEntries)
        {
            _deployOnTheFlyWorkspace.ClearReadinessState("Add at least one VM entry to evaluate readiness.");
            _deployOnTheFlyWorkspace.ResetProgressState();
        }

        UpdateDeployOnTheFlyResultRows();
        UpdateDeployOnTheFlyIssueRows();
        UpdateDeployOnTheFlyVmEntryRows();
        UpdateDeployOnTheFlyEditorPanel();

        var blockingIssueCount = _deployOnTheFlyWorkspace.IssueRows.Count(issue => string.Equals(issue.Severity, "Block", StringComparison.OrdinalIgnoreCase));
        var warningIssueCount = _deployOnTheFlyWorkspace.IssueRows.Count(issue => !string.Equals(issue.Severity, "Block", StringComparison.OrdinalIgnoreCase));
        var shouldShowInlineGuidance = hasEntries &&
                                       !_deployOnTheFlyWorkspace.IsStarting &&
                                       _deployOnTheFlyWorkspace.LiveProgressVmCount == 0;
        DeployOnTheFlyViewHost.ApplyWorkspaceState(new DeployOnTheFlyWorkspaceViewState(
            CanAddVm: !_deployOnTheFlyWorkspace.IsEvaluatingReadiness && !_deployOnTheFlyWorkspace.IsStarting,
            CanRemoveVm: _deployOnTheFlyWorkspace.SelectedVmEntry is not null &&
                         !_deployOnTheFlyWorkspace.IsEvaluatingReadiness &&
                         !_deployOnTheFlyWorkspace.IsStarting,
            CanApplyVmChanges: _deployOnTheFlyWorkspace.SelectedVmEntry is not null &&
                               !_deployOnTheFlyWorkspace.IsEvaluatingReadiness &&
                               !_deployOnTheFlyWorkspace.IsStarting,
            CanEvaluate: false,
            CanResolveSuggestions: hasEntries && !_deployOnTheFlyWorkspace.IsEvaluatingReadiness && !_deployOnTheFlyWorkspace.IsStarting,
            CanOpenTemplateEditor: hasEntries && !_deployOnTheFlyWorkspace.IsStarting,
            CanStartDeploy: hasEntries && !hasBlockingFailures && !_deployOnTheFlyWorkspace.IsEvaluatingReadiness && !_deployOnTheFlyWorkspace.IsStarting,
            EditorIssueSummaryText: BuildDeployOnTheFlyEditorIssueSummaryText(),
            OverallStateText: _deployOnTheFlyWorkspace.LifecycleState,
            ProgressPercent: _deployOnTheFlyWorkspace.ProgressPercent,
            ProgressSummaryText: _deployOnTheFlyWorkspace.ProgressSummary,
            GlobalIssuesBadgeText: $"Blocking: {blockingIssueCount} | Warnings: {warningIssueCount}",
            ReadinessSummaryText: shouldShowInlineGuidance
                ? $"{_deployOnTheFlyWorkspace.ReadinessSummaryText} Review VM row badges and the selected VM details to fix blockers here before deploy."
                : _deployOnTheFlyWorkspace.ProgressSummary,
            StatusText: _deployOnTheFlyStatusText));
        ApplyRightPanelState();
    }

    private string? PickBaseDiskFilePath()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var selectedPath = NativeFileDialogs.ShowOpenVhdxDialog(hwnd);
        return string.IsNullOrWhiteSpace(selectedPath) ? null : selectedPath;
    }

    private async Task<bool> ShowAssetsBaseDiskRemoveConfirmationDialogAsync(
        AssetsBaseDiskListRow row,
        AssetsBaseDiskRemovalAssessment assessment)
    {
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to remove '{row.DisplayName}' from the registry."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = "This removes the base disk from the LabAssistant registry only. It does not delete the underlying VHDX file.",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = assessment.ReferenceSignalSummary,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });

        foreach (var warning in assessment.WarningReasons)
        {
            content.Children.Add(new TextBlock
            {
                Text = "- " + warning,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellWarnBrush"]
            });
        }

        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Remove Base Disk",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = RootLayout.XamlRoot,
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> ShowAssetsSwitchDeleteConfirmationDialogAsync(
        AssetsSwitchListRow row,
        AssetsSwitchDeleteAssessment assessment)
    {
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to delete virtual switch '{row.Name}'."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = "Delete is allowed only when no Hyper-V VM is attached to the switch. VM power state does not make delete safe.",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = assessment.Summary,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });
        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Delete Virtual Switch",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = RootLayout.XamlRoot,
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
    private bool IsTemplatesLibraryActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.TemplatesLibrary, StringComparison.Ordinal);

    private bool IsTemplatesEditorActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.TemplatesEditor, StringComparison.Ordinal);

    private bool IsAssetsOverviewActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.AssetsOverview, StringComparison.Ordinal);

    private bool IsAssetsBaseDisksActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.AssetsBaseDisks, StringComparison.Ordinal);

    private bool IsAssetsSwitchesActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.AssetsSwitches, StringComparison.Ordinal);

    private bool IsAssetsCapabilityActive =>
        IsAssetsOverviewActive || IsAssetsBaseDisksActive || IsAssetsSwitchesActive;

    private bool IsTemplatesCapabilityActive =>
        IsTemplatesLibraryActive || IsTemplatesEditorActive;

    private bool IsSettingsMachinesActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.SettingsMachines, StringComparison.Ordinal);

    private bool IsDiagnosticsOverviewActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.DiagnosticsOverview, StringComparison.Ordinal);

    private bool IsDiagnosticsLogsActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.DiagnosticsLogs, StringComparison.Ordinal);

    private bool IsDiagnosticsCapabilityActive =>
        IsDiagnosticsOverviewActive || IsDiagnosticsLogsActive;

    private async void DeployReloadTemplatesButton_Click(object sender, RoutedEventArgs e)
    {
        await EnsureDeployTemplatesLoadedAsync(forceRefresh: true);
    }

    private async void DeployEvaluateReadinessButton_Click(object sender, RoutedEventArgs e)
    {
        await EvaluateDeployReadinessAsync(DeploymentPreflightMode.Quick);
    }

    private async void DeployResolveSuggestionsButton_Click(object sender, RoutedEventArgs e)
    {
        var activeTemplateDocument = _deployFromTemplateWorkspaceComposition.ActiveTemplateDocument;
        if (activeTemplateDocument is null)
        {
            _deployFromTemplateWorkspaceComposition.SetActionStatus("Select a template first.");
            return;
        }

        var applied = await ApplyDeployResolveSuggestionsAsync(activeTemplateDocument.Template);
        _deployFromTemplateWorkspaceComposition.SetActionStatus(
            applied == 0
                ? "No auto-resolve suggestions available for the current template state."
                : $"Applied {applied} auto-resolve suggestion(s). Re-evaluating readiness...");
        await EvaluateDeployReadinessAsync(DeploymentPreflightMode.Quick);
    }

    private async void DeployOpenTemplateEditorButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedTemplateLibraryItem = _deployFromTemplateWorkspaceComposition.SelectedTemplateLibraryItem;
        if (selectedTemplateLibraryItem is null)
        {
            _deployFromTemplateWorkspaceComposition.SetActionStatus("Select a template first.");
            return;
        }

        await OpenTemplateInEditorAsync(selectedTemplateLibraryItem, fromDeploy: true);
    }

    private async void DeployTemplateSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isDeployLoadingTemplates || _isTemplatesLoading)
        {
            return;
        }

        _deployFromTemplateWorkspaceComposition.SetSelectedTemplateLibraryItem(DeployFromTemplateView.SelectedTemplateLibraryItem);
        if (_deployFromTemplateWorkspaceComposition.SelectedTemplateLibraryItem is null)
        {
            _deployFromTemplateWorkspaceComposition.ClearSelection("No template selected.");
            _deployReadinessReport = null;
            _deployCompatibilityIssues.Clear();
            _deployFromTemplateWorkspaceComposition.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Idle",
                progressPercent: 0,
                progressSummary: "No template selected.");
            UpdateDeployUi();
            return;
        }

        try
        {
            var selectedTemplate = _deployFromTemplateWorkspaceComposition.SelectedTemplateLibraryItem!;
            var document = await _templatesCapabilityService.LoadForEditorAsync(selectedTemplate.FilePath);
            _deployFromTemplateWorkspaceComposition.SetLoadedTemplateDocument(
                document,
                $"Loaded '{selectedTemplate.Name}' for deploy readiness.");
            _deployFromTemplateWorkspaceComposition.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Ready",
                progressPercent: 0,
                progressSummary: $"Template '{selectedTemplate.Name}' loaded.");
            await EvaluateDeployReadinessAsync(DeploymentPreflightMode.Quick);
        }
        catch (Exception ex)
        {
            _deployFromTemplateWorkspaceComposition.SetSelectionLoadFailed($"Failed to load selected template. {ex.Message}");
            _deployReadinessReport = null;
            _deployCompatibilityIssues.Clear();
            _deployFromTemplateWorkspaceComposition.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Error",
                progressPercent: 0,
                progressSummary: "Template load failed.");
            UpdateDeployUi();
        }
    }

    private void AddDeployOnTheFlyVmEntry()
    {
        _deployOnTheFlyWorkspace.SetShowAllVmRows(false);
        var entry = _deployOnTheFlyWorkspace.AddVmEntry();
        _deployWorkspaceComposition.RefreshSharedUiState();
        SelectDeployOnTheFlyVmEntry(entry);
        _deployOnTheFlyWorkspace.ClearReadinessState("Readiness has not been evaluated.");
        _deployOnTheFlyWorkspace.ResetProgressState();
        SetDeployOnTheFlyStatusText($"Added VM entry '{entry.Name}'.");
        UpdateDeployOnTheFlyEditorPanel();
        UpdateDeployOnTheFlyUi();
    }

    private async Task RemoveDeployOnTheFlyVmEntryAsync(VmTemplate? vmEntry)
    {
        _deployOnTheFlyWorkspace.SetShowAllVmRows(false);
        if (vmEntry is null)
        {
            SetDeployOnTheFlyStatusText("Select a VM entry first.");
            return;
        }

        var vmName = vmEntry.Name;
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Remove VM Entry",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            Content = $"Remove '{vmName}' from quick deploy configuration?",
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _deployOnTheFlyWorkspace.RemoveVmEntry(vmEntry);
        _deployWorkspaceComposition.RefreshSharedUiState();
        SelectDeployOnTheFlyVmEntry(_deployOnTheFlyWorkspace.SelectedVmEntry);
        _deployOnTheFlyWorkspace.ClearReadinessState(
            _deployOnTheFlyWorkspace.VmEntryCount == 0
                ? "Add at least one VM entry to evaluate readiness."
                : "Readiness has not been evaluated.");
        _deployOnTheFlyWorkspace.ResetProgressState();
        SetDeployOnTheFlyStatusText($"Removed VM entry '{vmName}'.");
        UpdateDeployOnTheFlyEditorPanel();
        UpdateDeployOnTheFlyUi();
    }

    private void ApplyDeployOnTheFlyVmChanges()
    {
        _deployOnTheFlyWorkspace.SetShowAllVmRows(false);
        if (_deployOnTheFlyWorkspace.SelectedVmEntry is null)
        {
            SetDeployOnTheFlyStatusText("Select a VM entry first.");
            return;
        }

        if (!TryApplyDeployOnTheFlyVmFields(showSuccessStatus: true))
        {
            return;
        }

        _deployOnTheFlyWorkspace.ClearReadinessState("Readiness has not been evaluated.");
        _deployOnTheFlyWorkspace.ResetProgressState();
        UpdateDeployOnTheFlyUi();
        _ = EvaluateDeployOnTheFlyReadinessAsync(DeploymentPreflightMode.Full);
    }

    private async Task ResolveDeployOnTheFlySuggestionsAsync()
    {
        if (_deployOnTheFlyWorkspace.VmEntryCount == 0)
        {
            SetDeployOnTheFlyStatusText("Add at least one VM entry first.");
            return;
        }

        var template = BuildOnTheFlyTemplate();
        var applied = await ApplyDeployResolveSuggestionsAsync(template);
        if (applied > 0)
        {
            ReplaceDeployOnTheFlyEntriesFromTemplate(template);
        }

        SetDeployOnTheFlyStatusText(applied == 0
            ? "No auto-resolve suggestions available for the current quick deploy configuration."
            : $"Applied {applied} auto-resolve suggestion(s). Re-evaluating readiness...");
        await EvaluateDeployOnTheFlyReadinessAsync(DeploymentPreflightMode.Full);
    }

    private async Task OpenDeployOnTheFlyTemplateEditorAsync()
    {
        if (!TryApplyDeployOnTheFlyVmFields(showSuccessStatus: false) && _deployOnTheFlyWorkspace.SelectedVmEntry is not null)
        {
            return;
        }

        if (_deployOnTheFlyWorkspace.VmEntryCount == 0)
        {
            SetDeployOnTheFlyStatusText("Add at least one VM entry first.");
            return;
        }

        await _templatesWorkspaceComposition.ShowEditorDocumentAsync(new TemplateEditorDocument
        {
            Template = BuildOnTheFlyTemplate(),
            SourceFilePath = null
        }, "Opened quick deploy configuration in Templates editor.");
        SetDeployOnTheFlyStatusText("Opened quick deploy configuration in Templates editor.");
    }

    private void InitializeDeployOnTheFlyProgressRows(MultiVmDeploymentContext context)
    {
        _deployOnTheFlyWorkspace.InitializeProgressRows(context, BuildExpectedDeploySteps);
    }

    private void AttachDeployOnTheFlyProgressCallbacks(MultiVmDeploymentContext context)
    {
        AttachDeployProgressCallbacks(
            context,
            (vmName, message) =>
            {
                _deployOnTheFlyWorkspace.UpdateProgressMessage(vmName, message);
                UpdateDeployOnTheFlyUi();
            },
            (vmName, update) =>
            {
                _deployOnTheFlyWorkspace.ApplyProgressUpdate(vmName, update);
                UpdateDeployOnTheFlyUi();
            });
    }

    AppSettings IDeployOnTheFlyWorkspaceControllerHost.DeploymentSettings => _settingsStore.Settings;

    IReadOnlyList<string> IDeployOnTheFlyWorkspaceControllerHost.AvailableSwitches => _templateAvailableSwitches;

    IReadOnlyList<VhdxCatalogItem> IDeployOnTheFlyWorkspaceControllerHost.LoadCatalogItems() => LoadDeployCatalogItems();

    bool IDeployOnTheFlyWorkspaceControllerHost.TryApplyVmFields(bool showSuccessStatus, bool showValidationErrors) =>
        TryApplyDeployOnTheFlyVmFields(showSuccessStatus, showValidationErrors);

    void IDeployOnTheFlyWorkspaceControllerHost.SetActionStatus(string statusText)
    {
        SetDeployOnTheFlyStatusText(statusText);
    }

    Task IDeployOnTheFlyWorkspaceControllerHost.EvaluateReadinessAsync(DeploymentPreflightMode mode) =>
        EvaluateDeployOnTheFlyReadinessAsync(mode);

    LabTemplate IDeployOnTheFlyWorkspaceControllerHost.BuildTemplate() => BuildOnTheFlyTemplate();

    void IDeployOnTheFlyWorkspaceControllerHost.BeginDeployWorkflow()
    {
        _deployOnTheFlyWorkspace.SetShowAllVmRows(true);
        _deployOnTheFlyWorkspace.SetWorkflowState("Running", 15, "Preparing deployment...");
        UpdateDeployOnTheFlyUi();
    }

    void IDeployOnTheFlyWorkspaceControllerHost.PrepareDeployExecution(MultiVmDeploymentContext context)
    {
        InitializeDeployOnTheFlyProgressRows(context);
        AttachDeployOnTheFlyProgressCallbacks(context);
        _deployOnTheFlyWorkspace.SetWorkflowState("Running", 40, $"Deploying {context.VmContexts.Count} VM(s)...");
        SetDeployOnTheFlyStatusText("Starting quick deploy...");
        UpdateDeployOnTheFlyResultRows();
        UpdateDeployOnTheFlyUi();
    }

    async Task<DeploymentOutcomeSummary> IDeployOnTheFlyWorkspaceControllerHost.DeployAllAsync(MultiVmDeploymentContext context)
    {
        await _deploymentCoordinator.DeployAllAsync(context);
        return _deploymentOutcomeSummaryBuilder.Build(context);
    }

    void IDeployOnTheFlyWorkspaceControllerHost.SetDeployBlocked()
    {
        _deployOnTheFlyWorkspace.SetWorkflowState("Blocked", 35, "Deployment blocked by readiness failures.");
        SetDeployOnTheFlyStatusText("Deploy blocked by readiness failures. Resolve blocking items first.");
        UpdateDeployOnTheFlyUi();
    }

    void IDeployOnTheFlyWorkspaceControllerHost.ApplyDeploySummary(DeploymentOutcomeSummary summary)
    {
        UpdateDeployOnTheFlyRowsFromSummary(summary);
        _deployOnTheFlyWorkspace.SetWorkflowState(
            lifecycleState: summary.OperationState switch
            {
                DeploymentOperationState.Completed => "Completed",
                DeploymentOperationState.Cancelled or DeploymentOperationState.CancelledWithResiduals => "Cancelled",
                DeploymentOperationState.Failed or DeploymentOperationState.FailedWithResiduals => "Failed",
                _ => "Completed"
            },
            progressPercent: 100,
            progressSummary: $"Completed. Success={summary.SucceededVmCount}, Failed={summary.FailedVmCount}, Cancelled={summary.CancelledVmCount}.");
        SetDeployOnTheFlyStatusText(
            $"Deployment finished: {summary.OperationState}. Total={summary.TotalVmCount}, Succeeded={summary.SucceededVmCount}, Failed={summary.FailedVmCount}.");
        UpdateDeployOnTheFlyUi();
    }

    void IDeployOnTheFlyWorkspaceControllerHost.SetDeployFailed(string errorMessage)
    {
        _deployOnTheFlyWorkspace.SetWorkflowState("Failed", 100, "Deployment failed.");
        SetDeployOnTheFlyStatusText($"Deploy failed. {errorMessage}");
        UpdateDeployOnTheFlyUi();
    }

    void IDeployOnTheFlyWorkspaceControllerHost.FinalizeDeployWorkflow()
    {
        _deployOnTheFlyWorkspace.SetShowAllVmRows(true);
        UpdateDeployOnTheFlyUi();
    }

    private void AttachDeployProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated)
    {
        foreach (var vmContext in context.VmContexts)
        {
            var vmName = string.IsNullOrWhiteSpace(vmContext.VmName) ? "Unnamed-VM" : vmContext.VmName.Trim();
            vmContext.LogCallback = message => DispatcherQueue.TryEnqueue(() => onLogMessage(vmName, message));
            vmContext.StepStateEmitter = update => DispatcherQueue.TryEnqueue(() => onStepStateUpdated(vmName, update));
        }
    }

    private static IReadOnlyList<DeployTimelineStepDefinition> BuildExpectedDeploySteps(VmDeploymentContext context)
    {
        var steps = new List<DeployTimelineStepDefinition>
        {
            new(DeploymentStepKeys.CheckHyperV, "Check Hyper-V"),
            new(DeploymentStepKeys.CreateVmFolder, "Create VM folder"),
            new(DeploymentStepKeys.CreateVhd, "Create differencing disk"),
            new(DeploymentStepKeys.CreateVm, "Create VM"),
            new(DeploymentStepKeys.AddNicToVm, "Add network adapter"),
            new(DeploymentStepKeys.ConfigureVm, "Configure VM"),
            new(DeploymentStepKeys.EnableGuestServices, "Enable guest services"),
            new(DeploymentStepKeys.DisableVmCheckpoints, "Disable VM checkpoints"),
            new(DeploymentStepKeys.StartVm, "Start VM")
        };

        if (context.ConfigureTimeZone)
        {
            steps.Add(new DeployTimelineStepDefinition(DeploymentStepKeys.SetTimeZone, "Set Time Zone"));
        }

        if (context.InstallSoftware)
        {
            steps.Add(new DeployTimelineStepDefinition(DeploymentStepKeys.InstallSoftware, "Install Software"));
        }

        if (context.InstallRole)
        {
            steps.Add(new DeployTimelineStepDefinition(DeploymentStepKeys.InstallRole, "Install Role"));
        }

        if (context.ConfigureNetworkInformation)
        {
            steps.Add(new DeployTimelineStepDefinition(DeploymentStepKeys.ConfigureNetworkInformation, "Configure Network Information"));
        }

        return steps;
    }

    private void ApplyTemplatesWorkspaceUiState()
    {
        _templatesWorkspaceComposition.ApplyUiState(CreateTemplatesWorkspaceUiState());
    }

    private TemplatesWorkspaceUiState CreateTemplatesWorkspaceUiState()
    {
        return new TemplatesWorkspaceUiState(
            IsLoading: _isTemplatesLoading,
            HasSelectedLibraryItem: _templatesWorkspaceComposition.SelectedLibraryItem is not null);
    }

    private void UpdateDeployUi()
    {
        var activeTemplateDocument = _deployFromTemplateWorkspaceComposition.ActiveTemplateDocument;
        var hasTemplate = activeTemplateDocument is not null;
        var hasBlockingFailures = _deployCompatibilityIssues.Any(issue => issue.IsBlocking) ||
                                  (_deployReadinessReport?.HasBlockingFailures ?? false);

        _deployFromTemplateWorkspaceComposition.SetInteractionState(_isDeployLoadingTemplates, hasBlockingFailures);
        _deployFromTemplateWorkspaceComposition.RefreshReviewState(hasBlockingFailures);

        if (activeTemplateDocument is null)
        {
            _deployFromTemplateWorkspaceComposition.SetReadinessSummary("Select a template to evaluate readiness and run deploy.");
            _deployFromTemplateWorkspaceComposition.ClearGroupedIssueState();
            _deployFromTemplateWorkspaceComposition.RefreshResultRows(_deployCompatibilityIssues, _deployReadinessReport);
            UpdateDeployIssueRows();
            ApplyRightPanelState();
            return;
        }

        var failCount = _deployCompatibilityIssues.Count(issue => issue.IsBlocking) +
                        (_deployReadinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Fail) ?? 0);
        var warnCount = _deployCompatibilityIssues.Count(issue => !issue.IsBlocking) +
                        (_deployReadinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Warn) ?? 0);
        var passCount = _deployReadinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Pass) ?? 0;
        var deployState = hasBlockingFailures ? "Blocked" : "Ready";
        _deployFromTemplateWorkspaceComposition.SetReadinessSummary(
            $"{deployState}. Pass={passCount}, Warn={warnCount}, Fail={failCount}. " +
            $"Template: {activeTemplateDocument.Template.Name} ({activeTemplateDocument.Template.VmTemplates.Count} VMs).");

        _deployFromTemplateWorkspaceComposition.RefreshResultRows(_deployCompatibilityIssues, _deployReadinessReport);
        UpdateDeployIssueRows();
        ApplyRightPanelState();
    }

    private void UpdateDeployIssueRows()
    {
        var issueRows = new List<DeployIssueRow>();

        foreach (var issue in _deployCompatibilityIssues)
        {
            var scope = string.IsNullOrWhiteSpace(issue.VmName) ? "Global" : issue.VmName;
            issueRows.Add(new DeployIssueRow(
                Scope: scope,
                Severity: issue.IsBlocking ? "Block" : "Warn",
                Message: $"{issue.Message} {issue.Guidance}".Trim()));
        }

        if (_deployReadinessReport is not null)
        {
            foreach (var result in _deployReadinessReport.Results.Where(result => result.Status is DeploymentReadinessStatus.Fail or DeploymentReadinessStatus.Warn))
            {
                var scope = result.AffectedVmNames.Count == 0 ? "Global" : string.Join(", ", result.AffectedVmNames);
                issueRows.Add(new DeployIssueRow(
                    Scope: scope,
                    Severity: result.Status == DeploymentReadinessStatus.Fail ? "Block" : "Warn",
                    Message: $"{result.Message} {result.ActionableGuidance}".Trim()));
            }
        }

        _deployFromTemplateWorkspaceComposition.ReplaceIssueRows(issueRows);
    }

    private void UpdateDeployOnTheFlyResultRows()
    {
        _deployOnTheFlyWorkspace.RefreshResultRows();
    }

    private void UpdateDeployOnTheFlyIssueRows()
    {
        _deployOnTheFlyWorkspace.RefreshIssueRows();
    }

    private void UpdateDeployOnTheFlyRowsFromSummary(DeploymentOutcomeSummary summary)
    {
        _deployOnTheFlyWorkspace.ApplyOutcomeSummary(summary);
    }

    private async Task EnsureDeployTemplatesLoadedAsync(bool forceRefresh)
    {
        if (!forceRefresh && TemplatesLibraryItems.Count > 0)
        {
            _deployWorkspaceComposition.RefreshSharedUiState();
            UpdateDeployUi();
            return;
        }

        _isDeployLoadingTemplates = true;
        _deployFromTemplateWorkspaceComposition.SetWorkflowState(
            isEvaluatingReadiness: false,
            isStarting: false,
            lifecycleState: "Loading",
            progressPercent: 0,
            progressSummary: "Loading templates...");
        _deployWorkspaceComposition.RefreshSharedUiState();
        UpdateDeployUi();
        _deployFromTemplateWorkspaceComposition.SetActionStatus("Loading templates for deploy...");

        try
        {
            await EnsureTemplatesLibraryAsync(forceRefresh: forceRefresh);

            if (TemplatesLibraryItems.Count == 0)
            {
                _deployFromTemplateWorkspaceComposition.ClearSelection("No templates available for deploy.");
                _deployReadinessReport = null;
                _deployCompatibilityIssues.Clear();
                _deployFromTemplateWorkspaceComposition.SetWorkflowState(
                    isEvaluatingReadiness: false,
                    isStarting: false,
                    lifecycleState: "Idle",
                    progressPercent: 0,
                    progressSummary: "No templates available.");
            }
            else
            {
                if (_deployFromTemplateWorkspaceComposition.SelectedTemplateLibraryItem is null)
                {
                    _deployFromTemplateWorkspaceComposition.SetSelectedTemplateLibraryItem(TemplatesLibraryItems[0]);
                }

                _deployFromTemplateWorkspaceComposition.SetWorkflowState(
                    isEvaluatingReadiness: false,
                    isStarting: false,
                    lifecycleState: "Idle",
                    progressPercent: 0,
                    progressSummary: "Template list loaded.");
                _deployFromTemplateWorkspaceComposition.SetActionStatus($"Loaded {TemplatesLibraryItems.Count} template(s) for deploy.");
            }
        }
        catch (Exception ex)
        {
            _deployFromTemplateWorkspaceComposition.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Error",
                progressPercent: 0,
                progressSummary: "Template load failed.");
            _deployFromTemplateWorkspaceComposition.SetActionStatus($"Failed to load deploy templates. {ex.Message}");
        }
        finally
        {
            _isDeployLoadingTemplates = false;
            _deployWorkspaceComposition.RefreshSharedUiState();
            UpdateDeployUi();
        }
    }

    private Task EvaluateDeployReadinessAsync(DeploymentPreflightMode mode) =>
        _deployFromTemplateWorkspaceComposition.EvaluateReadinessAsync(mode);

    private async void DeployStartButton_Click(object sender, RoutedEventArgs e)
    {
        await _deployFromTemplateWorkspaceComposition.StartDeployAsync();
    }

    private async Task<int> ApplyDeployResolveSuggestionsAsync(LabTemplate template)
    {
        var catalogResult = _vhdxCatalogStore.Load(_settingsStore.Settings.CatalogPath);
        var catalogItems = catalogResult.Items;
        var templateSwitches = _templateAvailableSwitches.Count > 0
            ? _templateAvailableSwitches
            : await _machinesCapabilityService.LoadVirtualSwitchesAsync();
        var availableSwitches = templateSwitches
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var applied = 0;
        foreach (var vm in template.VmTemplates)
        {
            var switchNames = vm.SwitchNames?.Where(name => !string.IsNullOrWhiteSpace(name)).ToList() ?? [];
            if (switchNames.Count == 0 && !string.IsNullOrWhiteSpace(vm.SwitchName))
            {
                switchNames.Add(vm.SwitchName);
            }

            if (switchNames.Count > 0)
            {
                var normalized = switchNames
                    .Where(name => availableSwitches.Contains(name, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (normalized.Count != switchNames.Count)
                {
                    applied++;
                }

                vm.SwitchNames = normalized.Count > 0 ? normalized : null;
                vm.SwitchName = normalized.Count > 0 ? normalized[0] : null;
            }

            if (!string.IsNullOrWhiteSpace(vm.VhdxId))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(vm.VhdxSignature))
            {
                var signatureMatches = VhdxSignature.FindMatches(vm.VhdxSignature, catalogItems);
                if (signatureMatches.Count == 1)
                {
                    var match = signatureMatches[0];
                    vm.VhdxId = match.Id;
                    vm.VhdPath = match.Path;
                    vm.VhdxSignature = match.Signature;
                    applied++;
                    continue;
                }
            }

            if (!string.IsNullOrWhiteSpace(vm.VhdPath))
            {
                var pathMatch = catalogItems.FirstOrDefault(item =>
                    string.Equals(item.Path, vm.VhdPath, StringComparison.OrdinalIgnoreCase));
                if (pathMatch is not null)
                {
                    vm.VhdxId = pathMatch.Id;
                    vm.VhdxSignature = pathMatch.Signature;
                    vm.VhdPath = pathMatch.Path;
                    applied++;
                }
            }
        }

        return applied;
    }

    private async Task OpenTemplateInEditorAsync(TemplateLibraryItem templateItem, bool fromDeploy)
    {
        _isTemplatesLoading = true;
        ApplyTemplatesWorkspaceUiState();
        try
        {
            var document = await _templatesCapabilityService.LoadForEditorAsync(templateItem.FilePath);
            await ShowTemplateEditorAsync(document, "Template loaded.");
            if (fromDeploy)
            {
                _deployFromTemplateWorkspaceComposition.SetActionStatus($"Opened '{templateItem.Name}' in Templates editor.");
            }
        }
        catch (Exception ex)
        {
            SetTemplateEditorStatus($"Failed to open template. {ex.Message}");
            if (fromDeploy)
            {
                _deployFromTemplateWorkspaceComposition.SetActionStatus($"Failed to open template in editor. {ex.Message}");
            }
        }
        finally
        {
            _isTemplatesLoading = false;
            ApplyTemplatesWorkspaceUiState();
            UpdateDeployUi();
        }
    }

    private async Task EnsureTemplatesLibraryAsync(bool forceRefresh)
    {
        await _templatesWorkspaceComposition.EnsureLibraryAsync(forceRefresh);
    }

    private async Task EnsureTemplateSwitchesAsync(bool forceRefresh)
    {
        if (!forceRefresh && _templateAvailableSwitches.Count > 0)
        {
            return;
        }

        try
        {
            var switches = await _machinesCapabilityService.LoadVirtualSwitchesAsync();
            _templateAvailableSwitches = switches
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            _templateAvailableSwitches = Array.Empty<string>();
        }

        _templatesWorkspaceComposition.SetEditorVmReferenceData(_templateAvailableSwitches, _templateVhdxCatalogOptions);
    }

    private async Task<TemplatesEditorReferenceData> LoadTemplateEditorReferenceDataAsync(bool forceRefresh)
    {
        await EnsureTemplateSwitchesAsync(forceRefresh);
        await EnsureTemplateVhdxCatalogOptionsAsync(forceRefresh);
        return new TemplatesEditorReferenceData(_templateAvailableSwitches, _templateVhdxCatalogOptions);
    }

    private async Task EnsureTemplateVhdxCatalogOptionsAsync(bool forceRefresh)
    {
        if (!forceRefresh && _templateVhdxCatalogOptions.Count > 0)
        {
            return;
        }

        _templateVhdxCatalogOptions.Clear();
        var result = await _templatesCapabilityService.LoadVhdxCatalogOptionsAsync();
        foreach (var item in result.Items)
        {
            _templateVhdxCatalogOptions.Add(new TemplateVhdxCatalogOption(
                item.Id,
                item.Path,
                item.OsName,
                item.OsVersion,
                item.Generation,
                item.Signature));
        }

        _templatesWorkspaceComposition.SetEditorVmReferenceData(_templateAvailableSwitches, _templateVhdxCatalogOptions);
    }

    private IReadOnlyList<VhdxCatalogItem> LoadDeployCatalogItems()
    {
        var catalogResult = _vhdxCatalogStore.Load(_settingsStore.Settings.CatalogPath);
        return catalogResult.Items;
    }

    private async Task EnsureDeployOnTheFlyReferenceDataAsync(bool forceRefresh)
    {
        await EnsureTemplateSwitchesAsync(forceRefresh);
        await EnsureTemplateVhdxCatalogOptionsAsync(forceRefresh);
        UpdateDeployOnTheFlyEditorPanel();
        UpdateDeployOnTheFlyUi();
    }

    private void SyncTemplateVmEntriesToDocument()
    {
        _templatesWorkspaceComposition.SyncEditorVmEntriesToDocument();
    }

    private void RefreshTemplateVmListView()
    {
        _templatesWorkspaceComposition.RefreshEditorVmEntries();
    }


    private Task<string?> PickTemplateFileForOpenAsync()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var selectedPath = NativeFileDialogs.ShowOpenJsonDialog(hwnd);
        return Task.FromResult(selectedPath);
    }

    private Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var selectedPath = NativeFileDialogs.ShowSaveJsonDialog(hwnd, suggestedFileName);
        return Task.FromResult(selectedPath);
    }

    private async Task<bool> ShowDeleteTemplateConfirmationDialogAsync(TemplateLibraryItem selectedTemplate)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Delete Template",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            Content = $"Delete '{selectedTemplate.Name}'? This removes the template file.",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> ShowRemoveTemplateVmConfirmationDialogAsync(string vmName)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Remove VM Entry",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            Content = $"Remove VM entry '{vmName}' from this template draft?",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void SetTemplatesLoading(bool isLoading)
    {
        _isTemplatesLoading = isLoading;
        ApplyTemplatesWorkspaceUiState();
    }

    private void NavigateToTemplatesEditor()
    {
        NavigateToRoute(ShellRouteKeys.TemplatesEditor);
    }

    private async Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText)
    {
        await _templatesWorkspaceComposition.ShowEditorDocumentAsync(document, statusText);
    }

    private void SetTemplateEditorStatus(string statusText)
    {
        _templatesWorkspaceComposition.SetEditorStatus(statusText);
    }

    private void ReconcileDeployTemplateSelection(IReadOnlyList<TemplateLibraryItem> items)
    {
        _deployFromTemplateWorkspaceComposition.ReconcileSelection(items);
        _deployWorkspaceComposition.RefreshSharedUiState();
    }

    private DeployWorkspaceUiState CreateDeployWorkspaceUiState()
    {
        return new DeployWorkspaceUiState(
            QuickDeployDraftCount: _deployOnTheFlyWorkspace.VmEntryCount,
            IsLoadingTemplates: _isDeployLoadingTemplates,
            AvailableTemplateCount: TemplatesLibraryItems.Count);
    }

    private void InitializeRdpReadinessTimer()
    {
        _rdpReadinessTimer = DispatcherQueue.CreateTimer();
        _rdpReadinessTimer.Interval = TimeSpan.FromMinutes(5);
        _rdpReadinessTimer.Tick += async (_, _) => await _machinesWorkspaceComposition.RefreshRdpReadinessAsync(selectedOnly: false);
    }

    private void UpdateReadinessPollingState()
    {
        if (_rdpReadinessTimer is null)
        {
            return;
        }

        if (IsMachinesOverviewActive && _machinesWorkspaceComposition.HasInventory)
        {
            if (!_rdpReadinessTimer.IsRunning)
            {
                _rdpReadinessTimer.Start();

                // Run one pass when Machines becomes active, then fall back to periodic checks.
                if (DateTimeOffset.UtcNow - _machinesWorkspaceComposition.LastRdpReadinessRefreshUtc >= _rdpReadinessTimer.Interval)
                {
                    _ = _machinesWorkspaceComposition.RefreshRdpReadinessAsync(selectedOnly: false);
                }
            }

            return;
        }

        if (_rdpReadinessTimer.IsRunning)
        {
            _rdpReadinessTimer.Stop();
        }

    }

    private string? TryOpenStructuredLogLocation(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"")
                {
                    UseShellExecute = true
                });
                return null;
            }

            var folderPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folderPath}\"")
                {
                    UseShellExecute = true
                });
                return $"Active structured log file not found. Opened log folder: {folderPath}";
            }

            return $"Structured log path does not exist yet: {filePath}";
        }
        catch (Exception ex)
        {
            return $"Failed to open structured log location. {ex.Message}";
        }
    }

    private async Task LoadMachinesDeletionPolicyAsync()
    {
        if (!IsSettingsMachinesActive || _isSavingDeletionPolicy)
        {
            return;
        }

        try
        {
            var mode = await _machinesCapabilityService.GetDeletionPolicyAsync();
            var item = MachinesDeletionPolicyComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(candidate => string.Equals(candidate.Tag?.ToString(), mode.ToString(), StringComparison.Ordinal));
            MachinesDeletionPolicyComboBox.SelectedItem = item;
            MachinesDeletionPolicyStatusTextBlock.Text = $"Current: {item?.Content ?? mode.ToString()}";
        }
        catch (Exception ex)
        {
            MachinesDeletionPolicyStatusTextBlock.Text = $"Failed to load policy. {ex.Message}";
        }
    }

    /* obsolete_machines_delete_scope_dialog_signature
    {
        var vmOnlyRadio = new RadioButton
        {
            Content = "VM registration only",
            IsChecked = preview.DefaultScope == MachineDeleteScope.VmRegistrationOnly
        };
        var vmAndStorageRadio = new RadioButton
        {
            IsChecked = preview.DefaultScope == MachineDeleteScope.VmAndStorage,
            Content = "VM + associated disks/files"
        };
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to delete '{vm.VmName}'."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = "Choose delete scope. This action is destructive.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Policy: {preview.PolicyMode} — {preview.PolicyMessage}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });

        foreach (var disk in preview.DiskClassifications)
        {
            content.Children.Add(new TextBlock
            {
                Text = $"Disk: {disk.DiskPath} | {disk.Classification} ({disk.Reason})",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
            });
        }

        content.Children.Add(vmOnlyRadio);
        content.Children.Add(vmAndStorageRadio);
        content.Children.Add(new TextBlock
        {
            Text = "If deleting with storage, associated disks/files will be removed where possible.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Delete VM",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = RootLayout.XamlRoot,
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return vmAndStorageRadio.IsChecked == true
            ? MachineDeleteScope.VmAndStorage
            : MachineDeleteScope.VmRegistrationOnly;
    }

    obsolete_machines_delete_confirmation_dialog_signature(
        MachineInventoryItem vm,
        MachineDeletePreview preview,
        MachineDeleteScope effectiveScope)
    {
        var scopeText = effectiveScope == MachineDeleteScope.VmAndStorage
            ? "VM + associated disks/files"
            : "VM registration only";
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to delete '{vm.VmName}'."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = $"Effective delete scope: {scopeText}",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Policy: {preview.PolicyMode} — {preview.PolicyMessage}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });
        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Delete VM",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = RootLayout.XamlRoot,
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    */
}
