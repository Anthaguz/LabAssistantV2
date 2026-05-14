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
    private readonly IAssetsBaseDisksCapabilityService _assetsBaseDisksCapabilityService;
    private readonly IAssetsSwitchesCapabilityService _assetsSwitchesCapabilityService;
    private readonly MachinesCapabilityRuntime _machinesCapabilityRuntime;
    private readonly AssetsCapabilityRuntime _assetsCapabilityRuntime;
    private readonly AssetsBaseDisksWorkspaceComposition _assetsBaseDisksWorkspaceComposition;
    private readonly AssetsSwitchesWorkspaceComposition _assetsSwitchesWorkspaceComposition;
    private readonly TemplatesCapabilityRuntime _templatesCapabilityRuntime;
    private readonly DeployCapabilityRuntime _deployCapabilityRuntime;
    private readonly DiagnosticsCapabilityRuntime _diagnosticsCapabilityRuntime;
    private ShellCapability _activeCapability;
    private ShellSubview _activeSubview;
    private string _activeRouteKey = string.Empty;
    private bool _isSavingDeletionPolicy;
    private bool _isUpdatingNavigationSelection;
    private bool _isShellRightPanelOpen;
    private bool _isShellRightPanelInCompactFallback;
    private bool _isDeployRightPanelAutoOpenSuppressed;
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
    private FrameworkElement AssetsLocalNavPanel => AssetsLocalNavigationPanel;
    private const string DeployCapabilityKey = "deploy";

    public MainWindow()
    {
        InitializeComponent();
        _machinesCapabilityService = App.Services.GetRequiredService<IMachinesCapabilityService>();
        _templatesCapabilityService = App.Services.GetRequiredService<ITemplatesCapabilityService>();
        _assetsBaseDisksCapabilityService = App.Services.GetRequiredService<IAssetsBaseDisksCapabilityService>();
        _assetsSwitchesCapabilityService = App.Services.GetRequiredService<IAssetsSwitchesCapabilityService>();
        _machinesCapabilityRuntime = CreateMachinesCapabilityRuntime();
        _assetsCapabilityRuntime = CreateAssetsCapabilityRuntime(
            out var assetsBaseDisksWorkspaceComposition,
            out var assetsSwitchesWorkspaceComposition);
        _assetsBaseDisksWorkspaceComposition = assetsBaseDisksWorkspaceComposition;
        _assetsSwitchesWorkspaceComposition = assetsSwitchesWorkspaceComposition;
        _templatesCapabilityRuntime = CreateTemplatesCapabilityRuntime();
        _deployCapabilityRuntime = CreateDeployCapabilityRuntime();
        _diagnosticsCapabilityRuntime = CreateDiagnosticsCapabilityRuntime();
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
            await _machinesCapabilityRuntime.EnsureInventoryAsync(forceRefresh: true);
            await _templatesCapabilityRuntime.EnsureEditorReferenceDataAsync(forceRefresh: true);
            await _templatesCapabilityRuntime.EnsureLibraryAsync(forceRefresh: true);
            await _assetsBaseDisksWorkspaceComposition.EnsureInventoryAsync(forceRefresh: true);
            await LoadMachinesDeletionPolicyAsync();
        };
        ApplyState();
    }

    private AssetsCapabilityRuntime CreateAssetsCapabilityRuntime(
        out AssetsBaseDisksWorkspaceComposition assetsBaseDisksWorkspaceComposition,
        out AssetsSwitchesWorkspaceComposition assetsSwitchesWorkspaceComposition)
    {
        var baseDisksCompositionHost = new AssetsBaseDisksCompositionHost(
            PickBaseDiskFilePath,
            ShowAssetsBaseDiskRemoveConfirmationDialogAsync);
        assetsBaseDisksWorkspaceComposition = new AssetsBaseDisksWorkspaceComposition(
            _assetsBaseDisksCapabilityService,
            AssetsBaseDisksViewHost,
            baseDisksCompositionHost);

        var switchesCompositionHost = new AssetsSwitchesCompositionHost(
            ShowAssetsSwitchDeleteConfirmationDialogAsync);
        assetsSwitchesWorkspaceComposition = new AssetsSwitchesWorkspaceComposition(
            _assetsSwitchesCapabilityService,
            AssetsSwitchesViewHost,
            switchesCompositionHost);

        var capabilityHost = new AssetsCapabilityHost();
        var capabilityShellBridge = new AssetsCapabilityShellBridge(
            () => IsAssetsCapabilityActive,
            () => IsAssetsOverviewActive,
            () => IsAssetsBaseDisksActive,
            () => IsAssetsSwitchesActive,
            NavigateToRoute);

        return new AssetsCapabilityRuntime(
            AssetsOverviewViewHost,
            assetsBaseDisksWorkspaceComposition,
            assetsSwitchesWorkspaceComposition,
            AssetsSubviewTabView,
            AssetsOverviewTabViewItem,
            AssetsBaseDisksTabViewItem,
            AssetsSwitchesTabViewItem,
            capabilityHost,
            capabilityShellBridge);
    }

    private MachinesCapabilityRuntime CreateMachinesCapabilityRuntime()
    {
        var capabilityShellBridge = new MachinesCapabilityShellBridge(
            () => IsMachinesOverviewActive,
            UpdateReadinessPollingState,
            () => RootLayout.XamlRoot);

        return new MachinesCapabilityRuntime(
            _machinesCapabilityService,
            MachinesOverviewViewHost,
            capabilityShellBridge);
    }

    private DiagnosticsCapabilityRuntime CreateDiagnosticsCapabilityRuntime()
    {
        var capabilityHost = new DiagnosticsCapabilityHost(App.Services.GetRequiredService<IStructuredLogViewerService>());
        var capabilityShellBridge = new DiagnosticsCapabilityShellBridge(
            () => IsDiagnosticsCapabilityActive,
            () => IsDiagnosticsOverviewActive,
            () => IsDiagnosticsLogsActive,
            NavigateToRoute,
            TryOpenStructuredLogLocation);

        return new DiagnosticsCapabilityRuntime(
            DiagnosticsLocalNavigationPanel,
            DiagnosticsOverviewViewHost,
            DiagnosticsLogsViewHost,
            DiagnosticsSubviewTabView,
            DiagnosticsOverviewTabViewItem,
            DiagnosticsLogsTabViewItem,
            capabilityHost,
            capabilityShellBridge);
    }

    private TemplatesCapabilityRuntime CreateTemplatesCapabilityRuntime()
    {
        var workspaceHost = TemplatesWorkspacePanel;
        TemplatesCapabilityRuntime? runtime = null;
        var libraryComposition = CreateTemplatesLibraryWorkspaceComposition(
            () => runtime?.IsLoading ?? false,
            isLoading => runtime?.SetLoading(isLoading),
            () => runtime?.ApplyUiState(),
            (document, statusText) => runtime?.ShowEditorDocumentAsync(document, statusText) ?? Task.CompletedTask,
            statusText => runtime?.SetEditorStatus(statusText),
            items => _deployCapabilityRuntime?.ReconcileTemplateSelection(items));
        var editorComposition = CreateTemplatesEditorWorkspaceComposition(
            () => runtime?.IsLoading ?? false,
            isLoading => runtime?.SetLoading(isLoading),
            () => runtime?.ApplyUiState(),
            forceRefresh => runtime?.EnsureLibraryAsync(forceRefresh) ?? Task.CompletedTask,
            forceRefresh => runtime?.LoadEditorReferenceDataAsync(forceRefresh)
                ?? Task.FromResult(new TemplatesEditorReferenceData(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>())));
        var shellBridge = CreateTemplatesWorkspaceShellBridge();

        async Task<IReadOnlyList<string>> loadAvailableVmSwitchesAsync()
        {
            try
            {
                var switches = await _machinesCapabilityService.LoadVirtualSwitchesAsync();
                return switches
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        async Task<IReadOnlyList<TemplateVhdxCatalogOption>> loadVhdxCatalogOptionsAsync()
        {
            var result = await _templatesCapabilityService.LoadVhdxCatalogOptionsAsync();
            return result.Items
                .Select(item => new TemplateVhdxCatalogOption(
                    item.Id,
                    item.Path,
                    item.OsName,
                    item.OsVersion,
                    item.Generation,
                    item.Signature))
                .ToList();
        }

        runtime = new TemplatesCapabilityRuntime(
            workspaceHost,
            libraryComposition,
            editorComposition,
            shellBridge,
            loadAvailableVmSwitchesAsync,
            loadVhdxCatalogOptionsAsync,
            () => _deployCapabilityRuntime?.RefreshTemplatesLoadingState());
        return runtime;
    }

    private TemplatesLibraryWorkspaceComposition CreateTemplatesLibraryWorkspaceComposition(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<TemplateEditorDocument, string, Task> showTemplateEditorAsync,
        Action<string> setTemplateEditorStatus,
        Action<IReadOnlyList<TemplateLibraryItem>> reconcileDeployTemplateSelection)
    {
        ITemplatesCapabilityService templatesCapabilityService = _templatesCapabilityService;
        var view = TemplatesLibraryViewHost;
        var host = CreateTemplatesLibraryWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            showTemplateEditorAsync,
            setTemplateEditorStatus,
            reconcileDeployTemplateSelection);

        return new TemplatesLibraryWorkspaceComposition(
            templatesCapabilityService,
            view,
            host);
    }

    private TemplatesLibraryWorkspaceHost CreateTemplatesLibraryWorkspaceHost(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<TemplateEditorDocument, string, Task> showTemplateEditorAsync,
        Action<string> setTemplateEditorStatus,
        Action<IReadOnlyList<TemplateLibraryItem>> reconcileDeployTemplateSelection)
    {
        Func<Task<string?>> pickTemplateFileForOpenAsync = PickTemplateFileForOpenAsync;
        Func<string, Task<string?>> pickTemplateFileForSaveAsync = PickTemplateFileForSaveAsync;
        Func<TemplateLibraryItem, Task<bool>> showDeleteTemplateConfirmationDialogAsync = ShowDeleteTemplateConfirmationDialogAsync;

        return new TemplatesLibraryWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            showTemplateEditorAsync,
            setTemplateEditorStatus,
            pickTemplateFileForOpenAsync,
            pickTemplateFileForSaveAsync,
            showDeleteTemplateConfirmationDialogAsync,
            reconcileDeployTemplateSelection);
    }

    private TemplatesEditorWorkspaceComposition CreateTemplatesEditorWorkspaceComposition(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<bool, Task<TemplatesEditorReferenceData>> loadReferenceDataAsync)
    {
        ITemplatesCapabilityService templatesCapabilityService = _templatesCapabilityService;
        var view = TemplatesEditorViewHost;
        var host = CreateTemplatesEditorWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            ensureTemplatesLibraryAsync,
            loadReferenceDataAsync);

        return new TemplatesEditorWorkspaceComposition(
            templatesCapabilityService,
            view,
            host);
    }

    private TemplatesEditorWorkspaceHost CreateTemplatesEditorWorkspaceHost(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<bool, Task<TemplatesEditorReferenceData>> loadReferenceDataAsync)
    {
        Func<string, Task<string?>> pickTemplateFileForSaveAsync = PickTemplateFileForSaveAsync;
        Func<string, Task<bool>> showRemoveTemplateVmConfirmationDialogAsync = ShowRemoveTemplateVmConfirmationDialogAsync;
        Action navigateToEditor = () => NavigateToRoute(ShellRouteKeys.TemplatesEditor);
        Action navigateToLibrary = () => NavigateToRoute(ShellRouteKeys.TemplatesLibrary);

        return new TemplatesEditorWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            ensureTemplatesLibraryAsync,
            loadReferenceDataAsync,
            pickTemplateFileForSaveAsync,
            showRemoveTemplateVmConfirmationDialogAsync,
            navigateToEditor,
            navigateToLibrary);
    }

    private TemplatesWorkspaceShellBridge CreateTemplatesWorkspaceShellBridge()
    {
        Func<bool> isTemplatesCapabilityActive = () => IsTemplatesCapabilityActive;
        Func<bool> isTemplatesLibraryActive = () => IsTemplatesLibraryActive;
        Func<bool> isTemplatesEditorActive = () => IsTemplatesEditorActive;

        return new TemplatesWorkspaceShellBridge(
            isTemplatesCapabilityActive,
            isTemplatesLibraryActive,
            isTemplatesEditorActive);
    }

    private DeployCapabilityRuntime CreateDeployCapabilityRuntime()
    {
        var shellBridge = CreateDeployCapabilityShellBridge();
        var templatesShellAdapter = CreateDeployTemplatesShellAdapter();
        var overviewView = DeployOverviewViewHost;
        var localNavigationHost = DeployLocalNavigationPanel;
        var subviewTabView = DeploySubviewTabView;
        var overviewTabViewItem = DeployOverviewTabViewItem;
        var quickDeployTabViewItem = DeployQuickDeployTabViewItem;
        var fromTemplateTabViewItem = DeployFromTemplateTabViewItem;
        var quickDeployView = DeployOnTheFlyViewHost;
        var quickDeployRightPanelView = DeployOnTheFlyRightPanelViewHost;
        var fromTemplateView = DeployFromTemplateViewHost;
        var fromTemplateRightPanelView = DeployFromTemplateRightPanelViewHost;

        var deploymentPreflightService = App.Services.GetRequiredService<IDeploymentPreflightService>();
        var deploymentCoordinator = App.Services.GetRequiredService<IDeploymentCoordinator>();
        var deploymentOutcomeSummaryBuilder = App.Services.GetRequiredService<IDeploymentOutcomeSummaryBuilder>();
        var settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
        var vhdxCatalogStore = App.Services.GetRequiredService<IVhdxCatalogStore>();

        var referenceDataService = new DeployReferenceDataService(
            settingsStore,
            vhdxCatalogStore,
            _machinesCapabilityService,
            _templatesCapabilityService);
        var resolveSuggestionsService = new DeployResolveSuggestionsService();
        var templateEditorLauncher = new DeployTemplateEditorLauncher(templatesShellAdapter);

        var quickDeployLane = new DeployOnTheFlyWorkspaceOwner(
            quickDeployView,
            quickDeployRightPanelView,
            referenceDataService,
            resolveSuggestionsService,
            templateEditorLauncher,
            new DeployOnTheFlyWorkspaceShellBridge(
                shellBridge.DispatcherQueue,
                () => shellBridge.XamlRoot,
                shellBridge.RequestResultsPanelToggle,
                shellBridge.RefreshResultsPanelState),
            deploymentPreflightService,
            deploymentCoordinator,
            deploymentOutcomeSummaryBuilder);

        DeployWorkspaceComposition? workspaceComposition = null;
        Action refreshSharedUiState = () => workspaceComposition?.RefreshSharedUiState();

        var fromTemplateLane = new DeployFromTemplateWorkspaceComposition(
            fromTemplateView,
            fromTemplateRightPanelView,
            templatesShellAdapter.ItemsSource,
            new DeployFromTemplateWorkspaceHost(
                referenceDataService,
                resolveSuggestionsService,
                templatesShellAdapter,
                refreshSharedUiState,
                shellBridge.RefreshResultsPanelState,
                (deploymentContext, mode) => deploymentPreflightService.RunAsync(deploymentContext, mode),
                async deploymentContext =>
                {
                    await deploymentCoordinator.DeployAllAsync(deploymentContext);
                    return deploymentOutcomeSummaryBuilder.Build(deploymentContext);
                },
                shellBridge.AttachProgressCallbacks,
                shellBridge.RequestResultsPanelToggle));

        workspaceComposition = new DeployWorkspaceComposition(
            localNavigationHost,
            overviewView,
            quickDeployLane,
            subviewTabView,
            overviewTabViewItem,
            quickDeployTabViewItem,
            fromTemplateTabViewItem,
            () => new DeployWorkspaceUiState(
                QuickDeployDraftCount: quickDeployLane.DraftCount,
                IsLoadingTemplates: fromTemplateLane.IsLoadingTemplates,
                AvailableTemplateCount: templatesShellAdapter.GetLibraryItems().Count),
            fromTemplateLane,
            new DeployWorkspaceShellBridge(
                () => shellBridge.IsDeployCapabilityActive,
                () => shellBridge.IsDeployOverviewActive,
                () => shellBridge.IsDeployOnTheFlyActive,
                () => shellBridge.IsDeployFromTemplateActive,
                shellBridge.NavigateToRoute));

        var resultsPanelCoordinator = new DeployResultsPanelCoordinator(
            quickDeployLane,
            fromTemplateLane,
            () => shellBridge.IsDeployOverviewActive,
            () => shellBridge.IsDeployOnTheFlyActive,
            () => shellBridge.IsDeployFromTemplateActive);

        return new DeployCapabilityRuntime(
            shellBridge,
            fromTemplateLane,
            workspaceComposition,
            resultsPanelCoordinator);
    }

    private DeployCapabilityShellBridge CreateDeployCapabilityShellBridge()
    {
        var dispatcherQueue = DispatcherQueue;
        Func<XamlRoot?> getXamlRoot = () => RootLayout.XamlRoot;
        Func<bool> isDeployCapabilityActive = () => IsDeployCapabilityActive;
        Func<bool> isDeployOverviewActive = () => IsDeployOverviewActive;
        Func<bool> isDeployOnTheFlyActive = () => IsDeployOnTheFlyActive;
        Func<bool> isDeployFromTemplateActive = () => IsDeployFromTemplateActive;
        Action<string> navigateToRoute = NavigateToRoute;
        Action requestResultsPanelToggle = RequestDeployResultsPanelToggle;
        Action refreshResultsPanelState = ApplyRightPanelState;

        return new DeployCapabilityShellBridge(
            dispatcherQueue,
            getXamlRoot,
            isDeployCapabilityActive,
            isDeployOverviewActive,
            isDeployOnTheFlyActive,
            isDeployFromTemplateActive,
            navigateToRoute,
            requestResultsPanelToggle,
            refreshResultsPanelState);
    }

    private DeployTemplatesShellAdapter CreateDeployTemplatesShellAdapter()
    {
        var itemsSource = _templatesCapabilityRuntime.LibraryItems;
        Func<bool> isTemplatesLoading = () => _templatesCapabilityRuntime.IsLoading;
        Func<IReadOnlyList<TemplateLibraryItem>> getLibraryItems = () => _templatesCapabilityRuntime.LibraryItems.ToList();
        Func<bool, Task> ensureLibraryAsync = forceRefresh => _templatesCapabilityRuntime.EnsureLibraryAsync(forceRefresh);
        Func<string, Task<TemplateEditorDocument>> loadTemplateForEditorAsync =
            filePath => _templatesCapabilityService.LoadForEditorAsync(filePath);
        Func<TemplateEditorDocument, string, Task> showTemplateEditorAsync =
            (document, statusText) => _templatesCapabilityRuntime.ShowEditorDocumentAsync(document, statusText);

        return new DeployTemplatesShellAdapter(
            itemsSource,
            isTemplatesLoading,
            getLibraryItems,
            ensureLibraryAsync,
            loadTemplateForEditorAsync,
            showTemplateEditorAsync);
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
        _templatesCapabilityRuntime.ApplyUiState();
        SettingsMachinesPanel.Visibility = IsSettingsMachinesActive ? Visibility.Visible : Visibility.Collapsed;
        NonMachinesPlaceholderTextBlock.Visibility = (IsMachinesOverviewActive || IsDeployCapabilityActive || IsAssetsCapabilityActive || IsTemplatesCapabilityActive || IsSettingsMachinesActive || IsDiagnosticsCapabilityActive) ? Visibility.Collapsed : Visibility.Visible;

        QueueNavigationSelectionUpdate();

        _machinesCapabilityRuntime.ApplyShellState();
        _deployCapabilityRuntime.ApplyShellState();
        _assetsCapabilityRuntime.ApplyShellState();
        _templatesCapabilityRuntime.ApplyShellState();
        _diagnosticsCapabilityRuntime.ApplyShellState();
        if (IsSettingsMachinesActive)
        {
            _ = LoadMachinesDeletionPolicyAsync();
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
        _isDeployRightPanelAutoOpenSuppressed = false;
        _deployCapabilityRuntime.ResetRightPanelBehavior();
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

        var shouldAutoOpenDeployRightPanel = CanActiveCapabilityOwnRightPanel() && _deployCapabilityRuntime.ShouldAutoOpenRightPanel();
        if (!shouldAutoOpenDeployRightPanel)
        {
            _isDeployRightPanelAutoOpenSuppressed = false;
        }

        if (shouldAutoOpenDeployRightPanel && !_isDeployRightPanelAutoOpenSuppressed)
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
        RightPanelTitleTextBlock.Text = _deployCapabilityRuntime.GetRightPanelTitleText();
        RightPanelEmptyStateBorder.Visibility = _deployCapabilityRuntime.ShouldShowRightPanelEmptyState(showPanel)
            ? Visibility.Visible
            : Visibility.Collapsed;
        _deployCapabilityRuntime.ApplyRightPanelState(showPanel, _isShellRightPanelInCompactFallback);
        IssueBadge.Visibility = Visibility.Collapsed;
        IssueBadgeTextBlock.Text = string.Empty;
    }

    private void RequestDeployResultsPanelToggle()
    {
        _shellRightPanelOwnerCapabilityKey = ResolveRightPanelOwnerCapabilityKey(_activeCapability.Key);
        if (_isShellRightPanelInCompactFallback ||
            !CanActiveCapabilityOwnRightPanel() ||
            (!IsDeployOnTheFlyActive && !IsDeployFromTemplateActive))
        {
            return;
        }

        SetRightPanelOpenFromUserToggle(!_isShellRightPanelOpen);
    }

    private void SetRightPanelOpenFromUserToggle(bool isOpen)
    {
        _isShellRightPanelOpen = isOpen;
        _isDeployRightPanelAutoOpenSuppressed = !isOpen &&
            CanActiveCapabilityOwnRightPanel() &&
            _deployCapabilityRuntime.ShouldAutoOpenRightPanel();
        ApplyRightPanelState();
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
            _machinesCapabilityRuntime.DiscardEditDraft();
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
            _ = _machinesCapabilityRuntime.EnsureInventoryAsync(forceRefresh: false);
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

        SetRightPanelOpenFromUserToggle(!_isShellRightPanelOpen);
    }

    private void CloseRightPanelButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isShellRightPanelOpen)
        {
            return;
        }

        SetRightPanelOpenFromUserToggle(false);
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

    private void InitializeRdpReadinessTimer()
    {
        _rdpReadinessTimer = DispatcherQueue.CreateTimer();
        _rdpReadinessTimer.Interval = TimeSpan.FromMinutes(5);
        _rdpReadinessTimer.Tick += async (_, _) => await _machinesCapabilityRuntime.RefreshRdpReadinessAsync(selectedOnly: false);
    }

    private void UpdateReadinessPollingState()
    {
        if (_rdpReadinessTimer is null)
        {
            return;
        }

        if (IsMachinesOverviewActive && _machinesCapabilityRuntime.HasInventory)
        {
            if (!_rdpReadinessTimer.IsRunning)
            {
                _rdpReadinessTimer.Start();

                // Run one pass when Machines becomes active, then fall back to periodic checks.
                if (DateTimeOffset.UtcNow - _machinesCapabilityRuntime.LastRdpReadinessRefreshUtc >= _rdpReadinessTimer.Interval)
                {
                    _ = _machinesCapabilityRuntime.RefreshRdpReadinessAsync(selectedOnly: false);
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
