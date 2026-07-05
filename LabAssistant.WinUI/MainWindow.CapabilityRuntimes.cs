using LabAssistant.Business.Assets;
using LabAssistant.Business.Deployment;
using LabAssistant.Business.Machines;
using LabAssistant.Business.Planning;
using LabAssistant.Business.Runtime;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Services.HyperV;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.Theming;
using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.ViewModels.Assets;
using LabAssistant.WinUI.ViewModels.Deploy;
using LabAssistant.WinUI.ViewModels.Diagnostics;
using LabAssistant.WinUI.ViewModels.Machines;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using LabAssistant.Services.Logging;
using WinRT.Interop;

namespace LabAssistant.WinUI;

public sealed partial class MainWindow
{
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

        var switchesCompositionHost = new AssetsSwitchesCompositionHost(ShowAssetsSwitchDeleteConfirmationDialogAsync);
        assetsSwitchesWorkspaceComposition = new AssetsSwitchesWorkspaceComposition(
            _assetsSwitchesCapabilityService,
            AssetsSwitchesViewHost,
            switchesCompositionHost);

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
            new AssetsCapabilityHost(),
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
        var builderComposition = CreateTemplatesBuilderWorkspaceComposition(
            () => runtime?.IsLoading ?? false,
            isLoading => runtime?.SetLoading(isLoading),
            () => runtime?.ApplyUiState(),
            forceRefresh => runtime?.EnsureLibraryAsync(forceRefresh) ?? Task.CompletedTask,
            forceRefresh => runtime?.LoadBuilderReferenceDataAsync(forceRefresh)
                ?? Task.FromResult(new TemplatesBuilderReferenceData(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>())));
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

        async Task<IReadOnlyList<V2AvailableSwitchInfo>> loadAvailableVmSwitchInfoAsync()
        {
            try
            {
                var result = await _assetsSwitchesCapabilityService.LoadAsync();
                return result.Items
                    .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                    .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                    {
                        var item = group.First();
                        return new V2AvailableSwitchInfo
                        {
                            Name = item.Name.Trim(),
                            SwitchType = string.IsNullOrWhiteSpace(item.SwitchType) ? "Unknown" : item.SwitchType.Trim()
                        };
                    })
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception)
            {
                return Array.Empty<V2AvailableSwitchInfo>();
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
            builderComposition,
            shellBridge,
            loadAvailableVmSwitchesAsync,
            loadAvailableVmSwitchInfoAsync,
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
        var host = CreateTemplatesLibraryWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            showTemplateEditorAsync,
            document => _templatesCapabilityRuntime?.ShowBuilderDocumentAsync(document) ?? Task.CompletedTask,
            () => _templatesCapabilityRuntime?.CreateBuilderDraftAsync() ?? Task.CompletedTask,
            setTemplateEditorStatus,
            reconcileDeployTemplateSelection);

        return new TemplatesLibraryWorkspaceComposition(_templatesCapabilityService, TemplatesLibraryViewHost, host);
    }

    private TemplatesLibraryWorkspaceHost CreateTemplatesLibraryWorkspaceHost(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<TemplateEditorDocument, string, Task> showTemplateEditorAsync,
        Func<TemplateEditorDocument, Task> showTemplateBuilderAsync,
        Func<Task> createTemplateBuilderDraftAsync,
        Action<string> setTemplateEditorStatus,
        Action<IReadOnlyList<TemplateLibraryItem>> reconcileDeployTemplateSelection)
    {
        return new TemplatesLibraryWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            showTemplateEditorAsync,
            showTemplateBuilderAsync,
            createTemplateBuilderDraftAsync,
            setTemplateEditorStatus,
            PickTemplateFileForOpenAsync,
            PickTemplateFileForSaveAsync,
            ShowDeleteTemplateConfirmationDialogAsync,
            reconcileDeployTemplateSelection);
    }

    private TemplatesEditorWorkspaceComposition CreateTemplatesEditorWorkspaceComposition(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<bool, Task<TemplatesEditorReferenceData>> loadReferenceDataAsync)
    {
        var host = CreateTemplatesEditorWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            ensureTemplatesLibraryAsync,
            loadReferenceDataAsync);

        return new TemplatesEditorWorkspaceComposition(_templatesCapabilityService, TemplatesEditorViewHost, host);
    }

    private TemplatesEditorWorkspaceHost CreateTemplatesEditorWorkspaceHost(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<bool, Task<TemplatesEditorReferenceData>> loadReferenceDataAsync)
    {
        return new TemplatesEditorWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            ensureTemplatesLibraryAsync,
            loadReferenceDataAsync,
            PickTemplateFileForSaveAsync,
            ShowRemoveTemplateVmConfirmationDialogAsync,
            () => NavigateToRoute(ShellRouteKeys.TemplatesEditor),
            () => NavigateToRoute(ShellRouteKeys.TemplatesLibrary));
    }

    private TemplatesBuilderWorkspaceComposition CreateTemplatesBuilderWorkspaceComposition(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<bool, Task<TemplatesBuilderReferenceData>> loadReferenceDataAsync)
    {
        var host = CreateTemplatesBuilderWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            ensureTemplatesLibraryAsync,
            loadReferenceDataAsync);

        return new TemplatesBuilderWorkspaceComposition(_templatesCapabilityService, TemplatesBuilderViewHost, host);
    }

    private TemplatesBuilderWorkspaceHost CreateTemplatesBuilderWorkspaceHost(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<bool, Task<TemplatesBuilderReferenceData>> loadReferenceDataAsync)
    {
        return new TemplatesBuilderWorkspaceHost(
            isTemplatesLoading,
            setTemplatesLoading,
            applyTemplatesWorkspaceUiState,
            ensureTemplatesLibraryAsync,
            loadReferenceDataAsync,
            PickTemplateFileForSaveAsync,
            () => NavigateToRoute(ShellRouteKeys.TemplatesBuilder),
            () => NavigateToRoute(ShellRouteKeys.TemplatesLibrary));
    }

    private TemplatesWorkspaceShellBridge CreateTemplatesWorkspaceShellBridge()
    {
        return new TemplatesWorkspaceShellBridge(
            () => IsTemplatesCapabilityActive,
            () => IsTemplatesLibraryActive,
            () => IsTemplatesEditorActive,
            () => IsTemplatesBuilderActive);
    }

    private DeployCapabilityRuntime CreateDeployCapabilityRuntime()
    {
        var shellBridge = CreateDeployCapabilityShellBridge();
        var templatesShellAdapter = CreateDeployTemplatesShellAdapter();
        var deploymentPreflightService = App.Services.GetRequiredService<IDeploymentPreflightService>();
        var deploymentCoordinator = App.Services.GetRequiredService<IDeploymentCoordinator>();
        var deploymentOutcomeSummaryBuilder = App.Services.GetRequiredService<IDeploymentOutcomeSummaryBuilder>();
        var settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
        var localCredentialSlotStore = App.Services.GetRequiredService<ILocalCredentialSlotStore>();
        var vhdxCatalogStore = App.Services.GetRequiredService<IVhdxCatalogStore>();
        var v2PlanningCapabilityService = App.Services.GetRequiredService<IV2PlanningCapabilityService>();
        var v2RuntimeCapabilityService = App.Services.GetRequiredService<IV2RuntimeCapabilityService>();
        var hyperVMachineAdminService = App.Services.GetRequiredService<IHyperVMachineAdminService>();

        var referenceDataService = new DeployReferenceDataService(
            settingsStore,
            vhdxCatalogStore,
            _machinesCapabilityService,
            _templatesCapabilityService,
            hyperVMachineAdminService);
        var resolveSuggestionsService = new DeployResolveSuggestionsService();
        var templateEditorLauncher = new DeployTemplateEditorLauncher(templatesShellAdapter);

        var quickDeployLane = new DeployOnTheFlyWorkspaceOwner(
            DeployOnTheFlyViewHost,
            DeployOnTheFlyRightPanelViewHost,
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
            DeployFromTemplateViewHost,
            DeployFromTemplateRightPanelViewHost,
            templatesShellAdapter.ItemsSource,
            new DeployFromTemplateWorkspaceHost(
                referenceDataService,
                resolveSuggestionsService,
                templatesShellAdapter,
                v2PlanningCapabilityService,
                v2RuntimeCapabilityService,
                localCredentialSlotStore,
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
            DeployLocalNavigationPanel,
            DeployOverviewViewHost,
            quickDeployLane,
            DeploySubviewTabView,
            DeployOverviewTabViewItem,
            DeployQuickDeployTabViewItem,
            DeployFromTemplateTabViewItem,
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

        return new DeployCapabilityRuntime(shellBridge, fromTemplateLane, workspaceComposition, resultsPanelCoordinator);
    }

    private DeployCapabilityShellBridge CreateDeployCapabilityShellBridge()
    {
        return new DeployCapabilityShellBridge(
            DispatcherQueue,
            () => RootLayout.XamlRoot,
            () => IsDeployCapabilityActive,
            () => IsDeployOverviewActive,
            () => IsDeployOnTheFlyActive,
            () => IsDeployFromTemplateActive,
            NavigateToRoute,
            RequestDeployResultsPanelToggle,
            ApplyRightPanelState);
    }

    private DeployTemplatesShellAdapter CreateDeployTemplatesShellAdapter()
    {
        return new DeployTemplatesShellAdapter(
            _templatesCapabilityRuntime.LibraryItems,
            () => _templatesCapabilityRuntime.IsLoading,
            () => _templatesCapabilityRuntime.LibraryItems.ToList(),
            forceRefresh => _templatesCapabilityRuntime.EnsureLibraryAsync(forceRefresh),
            filePath => _templatesCapabilityService.LoadForEditorAsync(filePath),
            (document, statusText) => _templatesCapabilityRuntime.ShowEditorDocumentAsync(document, statusText));
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

    private TextBlock CreateIconGlyph(string token)
    {
        return new TextBlock
        {
            Text = ShellIconCatalog.GetGlyph(token),
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTopBarForegroundBrush"]
        };
    }
}
