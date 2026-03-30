using System.Collections.ObjectModel;
using System.Diagnostics;
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
using LabAssistant.WinUI.Views.Deploy;
using LabAssistant.WinUI.Views.Machines;
using LabAssistant.WinUI.Interop;
using Microsoft.UI.Dispatching;
using WinRT.Interop;

namespace LabAssistant.WinUI;

public sealed partial class MainWindow : Window
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
    private readonly DeployOnTheFlyWorkspaceComposition _deployOnTheFlyWorkspaceComposition;
    private readonly List<TemplateVhdxCatalogOption> _templateVhdxCatalogOptions = [];
    private IReadOnlyList<string> _templateAvailableSwitches = Array.Empty<string>();
    private ShellCapability _activeCapability;
    private ShellSubview _activeSubview;
    private string _activeRouteKey = string.Empty;
    private bool _isSavingDeletionPolicy;
    private bool _isTemplatesLoading;
    private bool _isUpdatingNavigationSelection;
    private bool _isShellRightPanelOpen;
    private bool _isShellRightPanelInCompactFallback;
    private string _shellRightPanelOwnerCapabilityKey = string.Empty;
    private const double ShellRightPanelCompactThreshold = 1200;
    private const double ShellRightPanelExpandedWidth = 380;
    private const double ShellNavigationDrawerThreshold = 1100;
    private ElementTheme _theme = ElementTheme.Light;
    private DispatcherQueueTimer? _rdpReadinessTimer;

    private FrameworkElement MachinesOverviewPanel => MachinesOverviewViewHost;
    private FrameworkElement AssetsOverviewPanel => AssetsOverviewViewHost;
    private FrameworkElement AssetsBaseDisksPanel => AssetsBaseDisksViewHost;
    private FrameworkElement AssetsSwitchesPanel => AssetsSwitchesViewHost;
    private FrameworkElement TemplatesWorkspaceHost => TemplatesWorkspacePanel;
    private FrameworkElement AssetsLocalNavPanel => AssetsLocalNavigationPanel;
    private IList<TemplateLibraryItem> TemplatesLibraryItems => _templatesWorkspaceComposition.LibraryItems;
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
                    items => _deployWorkspaceComposition.ReconcileFromTemplateSelection(items))),
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
                () => _isTemplatesLoading,
                () => TemplatesLibraryItems.ToList(),
                LoadDeployCatalogItems,
                EnsureTemplateSwitchesAsync,
                EnsureTemplatesLibraryAsync,
                filePath => _templatesCapabilityService.LoadForEditorAsync(filePath),
                template => _deployWorkspaceComposition.ApplyResolveSuggestionsAsync(template),
                ShowTemplateEditorAsync,
                (context, mode) => _deploymentPreflightService.RunAsync(context, mode),
                () => _deployWorkspaceComposition.RefreshSharedUiState(),
                ApplyRightPanelState,
                async context =>
                {
                    await _deploymentCoordinator.DeployAllAsync(context);
                    return _deploymentOutcomeSummaryBuilder.Build(context);
                },
                AttachDeployProgressCallbacks,
                () =>
                {
                    if (_deployWorkspaceComposition.TryToggleRightPanelFromWorkflow(
                            _isShellRightPanelInCompactFallback,
                            _isShellRightPanelOpen,
                            out var nextOpenState))
                    {
                        _isShellRightPanelOpen = nextOpenState;
                        ApplyRightPanelState();
                    }
                }));
        var deployOnTheFlyWorkspaceHost = new DeployOnTheFlyWorkspaceHost(
            _deployOnTheFlyWorkspace,
            getDeploymentSettings: () => _settingsStore.Settings,
            getAvailableSwitches: () => _templateAvailableSwitches,
            loadCatalogItems: LoadDeployCatalogItems,
            refreshSharedUiState: () => _deployWorkspaceComposition.RefreshSharedUiState(),
            ensureReferenceDataAsync: EnsureDeployOnTheFlyReferenceDataAsync,
            applyResolveSuggestionsAsync: template => _deployWorkspaceComposition.ApplyResolveSuggestionsAsync(template),
            showTemplateEditorAsync: ShowTemplateEditorAsync,
            showRemoveVmEntryConfirmationDialogAsync: async vmName =>
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = RootLayout.XamlRoot,
                    Title = "Remove VM Entry",
                    PrimaryButtonText = "Remove",
                    CloseButtonText = "Cancel",
                    Content = $"Remove '{vmName}' from quick deploy configuration?",
                    DefaultButton = ContentDialogButton.Close
                };

                return await dialog.ShowAsync() == ContentDialogResult.Primary;
            },
            runReadinessChecksAsync: (context, mode) => _deploymentPreflightService.RunAsync(context, mode),
            deployAllAsync: async context =>
            {
                await _deploymentCoordinator.DeployAllAsync(context);
                return _deploymentOutcomeSummaryBuilder.Build(context);
            },
            enqueueUiUpdate: updateAction => DispatcherQueue.TryEnqueue(() => updateAction()),
            onOpenResultsPanelRequested: () =>
            {
                if (_deployWorkspaceComposition.TryToggleRightPanelFromWorkflow(
                        _isShellRightPanelInCompactFallback,
                        _isShellRightPanelOpen,
                        out var nextOpenState))
                {
                    _isShellRightPanelOpen = nextOpenState;
                    ApplyRightPanelState();
                }
            });
        _deployOnTheFlyWorkspaceComposition = new DeployOnTheFlyWorkspaceComposition(
            DeployOnTheFlyViewHost,
            DeployOnTheFlyRightPanelViewHost,
            _deployOnTheFlyWorkspace,
            deployOnTheFlyWorkspaceHost);
        deployOnTheFlyWorkspaceHost.AttachComposition(_deployOnTheFlyWorkspaceComposition);
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
            LoadDeployCatalogItems,
            () => _templateAvailableSwitches,
            async () => await _machinesCapabilityService.LoadVirtualSwitchesAsync(),
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
        _activeRouteKey = _shellViewModel.StartupRoute;
        _shellViewModel.TryResolveRoute(_activeRouteKey, out _activeCapability, out _activeSubview);
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
            _ = _deployFromTemplateWorkspaceComposition.EnsureTemplatesLoadedAsync(forceRefresh: false);
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
        _deployWorkspaceComposition.ResetRightPanelBehavior();
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

        if (CanActiveCapabilityOwnRightPanel() && _deployWorkspaceComposition.ShouldAutoOpenRightPanel())
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
        RightPanelTitleTextBlock.Text = _deployWorkspaceComposition.GetRightPanelTitleText();
        RightPanelEmptyStateBorder.Visibility = _deployWorkspaceComposition.ShouldShowRightPanelEmptyState(showPanel)
            ? Visibility.Visible
            : Visibility.Collapsed;
        _deployWorkspaceComposition.ApplyRightPanelState(showPanel, _isShellRightPanelInCompactFallback);
        IssueBadge.Visibility = Visibility.Collapsed;
        IssueBadgeTextBlock.Text = string.Empty;
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
        _deployOnTheFlyWorkspaceComposition.SetEditorReferenceData(_templateAvailableSwitches, _templateVhdxCatalogOptions);
        _deployOnTheFlyWorkspaceComposition.UpdateEditorPanel();
        _deployOnTheFlyWorkspaceComposition.UpdateUi();
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
        _deployFromTemplateWorkspaceComposition.RefreshUi();
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

    private DeployWorkspaceUiState CreateDeployWorkspaceUiState()
    {
        return new DeployWorkspaceUiState(
            QuickDeployDraftCount: _deployOnTheFlyWorkspace.VmEntryCount,
            IsLoadingTemplates: _deployFromTemplateWorkspaceComposition.IsLoadingTemplates,
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
}
