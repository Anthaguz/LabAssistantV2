using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using LabAssistant.Business.Assets;
using LabAssistant.Business.Deployment;
using LabAssistant.Business.Machines;
using LabAssistant.Business.Planning;
using LabAssistant.Business.Runtime;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.HyperV;
using LabAssistant.WinUI.Shell;
using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.ViewModels.Deploy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace LabAssistant.WinUI.Views.Deploy;

/// <summary>
/// Capability page that owns the Deploy surface while it is the active shell content. Hosts the
/// Overview, Quick Deploy, and From Template subviews in a tab view and is the shell right-panel
/// consumer (per-VM progress/results), driven through <see cref="IShellRightPanel"/>. It is the
/// composition root for the Deploy lanes: it builds their dependency graph from DI on navigation
/// and tears it down on leave, replacing the former long-lived <c>DeployCapabilityRuntime</c> plus
/// delegate-bag composition that lived in <c>MainWindow</c>.
/// </summary>
/// <remarks>
/// Interim stage: the Quick Deploy and From Template lanes still run through their preserved
/// controller/workspace layer hosted here (no longer through <c>MainWindow</c>). The full x:Bind
/// MVVM rewrite of those lanes and the removal of the remaining lane glue land in the follow-up.
/// </remarks>
public sealed partial class DeployPage : Page, ICapabilityPage
{
    private readonly ObservableCollection<TemplateLibraryItem> _templateItems = new();

    private IShellHost? _shellHost;
    private ITemplatesCapabilityService? _templatesCapabilityService;
    private DeployTemplatesShellAdapter? _templatesShellAdapter;
    private DeployOnTheFlyWorkspaceOwner? _quickDeployLane;
    private DeployFromTemplateWorkspaceComposition? _fromTemplateLane;
    private DeployOnTheFlyRightPanelView? _quickDeployRightPanel;
    private DeployFromTemplateRightPanelView? _fromTemplateRightPanel;

    private string _activeRouteKey = ShellRouteKeys.DeployOverview;
    private bool _isTemplatesLoading;
    private bool _isUpdatingSubviewSelection;

    public DeployPage()
    {
        InitializeComponent();
    }

    private DeployOverviewViewModel OverviewViewModel => OverviewViewHost.ViewModel;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var initialRoute = ShellRouteKeys.DeployOverview;
        if (e.Parameter is ShellNavigationRequest request)
        {
            _shellHost = request.Host;
            initialRoute = request.RouteKey;
        }

        BuildLanes();

        OverviewViewModel.OpenQuickDeployRequested += OnOpenQuickDeployRequested;
        OverviewViewModel.OpenFromTemplateRequested += OnOpenFromTemplateRequested;
        if (_shellHost is not null)
        {
            _shellHost.RightPanel.StateChanged += OnRightPanelStateChanged;
        }

        SelectSubviewTab(initialRoute);
        _ = ReloadTemplatesAsync(forceRefresh: false);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        OverviewViewModel.OpenQuickDeployRequested -= OnOpenQuickDeployRequested;
        OverviewViewModel.OpenFromTemplateRequested -= OnOpenFromTemplateRequested;
        if (_shellHost is not null)
        {
            _shellHost.RightPanel.StateChanged -= OnRightPanelStateChanged;
            _shellHost.RightPanel.SetContent(null);
        }

        _shellHost = null;
        _quickDeployLane = null;
        _fromTemplateLane = null;
        _quickDeployRightPanel = null;
        _fromTemplateRightPanel = null;
        _templatesShellAdapter = null;
    }

    void ICapabilityPage.ShowSubview(string routeKey) => SelectSubviewTab(routeKey);

    private void BuildLanes()
    {
        var services = App.Services;
        _templatesCapabilityService = services.GetRequiredService<ITemplatesCapabilityService>();
        var machinesCapabilityService = services.GetRequiredService<IMachinesCapabilityService>();
        var assetsSwitchesCapabilityService = services.GetRequiredService<IAssetsSwitchesCapabilityService>();
        var deploymentPreflightService = services.GetRequiredService<IDeploymentPreflightService>();
        var deploymentCoordinator = services.GetRequiredService<IDeploymentCoordinator>();
        var deploymentOutcomeSummaryBuilder = services.GetRequiredService<IDeploymentOutcomeSummaryBuilder>();
        var settingsStore = services.GetRequiredService<IAppSettingsStore>();
        var localCredentialSlotStore = services.GetRequiredService<ILocalCredentialSlotStore>();
        var vhdxCatalogStore = services.GetRequiredService<IVhdxCatalogStore>();
        var v2PlanningCapabilityService = services.GetRequiredService<IV2PlanningCapabilityService>();
        var v2RuntimeCapabilityService = services.GetRequiredService<IV2RuntimeCapabilityService>();
        var hyperVMachineAdminService = services.GetRequiredService<IHyperVMachineAdminService>();

        var referenceDataService = new DeployReferenceDataService(
            settingsStore,
            vhdxCatalogStore,
            machinesCapabilityService,
            _templatesCapabilityService,
            hyperVMachineAdminService);
        var resolveSuggestionsService = new DeployResolveSuggestionsService();

        _templatesShellAdapter = new DeployTemplatesShellAdapter(
            _templateItems,
            () => _isTemplatesLoading,
            () => _templateItems.ToList(),
            forceRefresh => ReloadTemplatesAsync(forceRefresh),
            filePath => _templatesCapabilityService!.LoadForEditorAsync(filePath),
            (document, statusText) => _shellHost is not null
                ? _shellHost.ShowTemplateInEditorAsync(document, statusText)
                : Task.CompletedTask);

        var templateEditorLauncher = new DeployTemplateEditorLauncher(_templatesShellAdapter);

        _quickDeployRightPanel = new DeployOnTheFlyRightPanelView();
        _fromTemplateRightPanel = new DeployFromTemplateRightPanelView();

        _quickDeployLane = new DeployOnTheFlyWorkspaceOwner(
            QuickDeployViewHost,
            _quickDeployRightPanel,
            referenceDataService,
            resolveSuggestionsService,
            templateEditorLauncher,
            new DeployOnTheFlyWorkspaceShellBridge(
                DispatcherQueue,
                () => _shellHost?.XamlRoot,
                () => _shellHost?.RightPanel.Toggle(),
                OnLaneResultsPanelStateChanged),
            deploymentPreflightService,
            deploymentCoordinator,
            deploymentOutcomeSummaryBuilder);
        _quickDeployLane.SharedUiStateChanged += OnLaneSharedUiStateChanged;

        _fromTemplateLane = new DeployFromTemplateWorkspaceComposition(
            FromTemplateViewHost,
            _fromTemplateRightPanel,
            _templatesShellAdapter.ItemsSource,
            new DeployFromTemplateWorkspaceHost(
                referenceDataService,
                resolveSuggestionsService,
                _templatesShellAdapter,
                v2PlanningCapabilityService,
                v2RuntimeCapabilityService,
                localCredentialSlotStore,
                RefreshOverviewSummary,
                OnLaneResultsPanelStateChanged,
                (deploymentContext, mode) => deploymentPreflightService.RunAsync(deploymentContext, mode),
                async deploymentContext =>
                {
                    await deploymentCoordinator.DeployAllAsync(deploymentContext);
                    return deploymentOutcomeSummaryBuilder.Build(deploymentContext);
                },
                AttachProgressCallbacks,
                () => _shellHost?.RightPanel.Toggle()));

        RefreshOverviewSummary();
    }

    private void OnOpenQuickDeployRequested(object? sender, EventArgs e) =>
        _shellHost?.NavigateToRoute(ShellRouteKeys.DeployQuickDeploy);

    private void OnOpenFromTemplateRequested(object? sender, EventArgs e) =>
        _shellHost?.NavigateToRoute(ShellRouteKeys.DeployFromTemplate);

    private void OnLaneSharedUiStateChanged(object? sender, EventArgs e) => RefreshOverviewSummary();

    private void SubviewTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSubviewSelection || SubviewTabView.SelectedItem is not TabViewItem selectedTab)
        {
            return;
        }

        var routeKey = ReferenceEquals(selectedTab, QuickDeployTabViewItem)
            ? ShellRouteKeys.DeployQuickDeploy
            : ReferenceEquals(selectedTab, FromTemplateTabViewItem)
                ? ShellRouteKeys.DeployFromTemplate
                : ShellRouteKeys.DeployOverview;

        _activeRouteKey = routeKey;
        ActivateLaneForRoute(routeKey);
        _shellHost?.ReportActiveSubview(routeKey);
    }

    private void SelectSubviewTab(string routeKey)
    {
        var targetTab = string.Equals(routeKey, ShellRouteKeys.DeployQuickDeploy, StringComparison.Ordinal)
            ? QuickDeployTabViewItem
            : string.Equals(routeKey, ShellRouteKeys.DeployFromTemplate, StringComparison.Ordinal)
                ? FromTemplateTabViewItem
                : OverviewTabViewItem;

        _activeRouteKey = routeKey;

        if (!ReferenceEquals(SubviewTabView.SelectedItem, targetTab))
        {
            _isUpdatingSubviewSelection = true;
            try
            {
                SubviewTabView.SelectedItem = targetTab;
            }
            finally
            {
                _isUpdatingSubviewSelection = false;
            }
        }

        ActivateLaneForRoute(routeKey);
    }

    /// <summary>
    /// Activates the lane matching the active subview and drives the shell right panel for it.
    /// Ordering matters: the outgoing lane is deactivated and the incoming lane activated (which
    /// seeds reference data and readiness) before the right panel is pointed at the active lane.
    /// </summary>
    private void ActivateLaneForRoute(string routeKey)
    {
        var isQuickDeploy = string.Equals(routeKey, ShellRouteKeys.DeployQuickDeploy, StringComparison.Ordinal);
        var isFromTemplate = string.Equals(routeKey, ShellRouteKeys.DeployFromTemplate, StringComparison.Ordinal);

        _quickDeployLane?.ApplyShellState(isQuickDeploy);
        _fromTemplateLane?.ApplyShellState(isFromTemplate);

        if (!isQuickDeploy && !isFromTemplate)
        {
            RefreshOverviewSummary();
        }

        UpdateRightPanelForActiveLane();
    }

    private IDeployResultsPanelParticipant? ActiveLane() =>
        string.Equals(_activeRouteKey, ShellRouteKeys.DeployQuickDeploy, StringComparison.Ordinal)
            ? _quickDeployLane
            : string.Equals(_activeRouteKey, ShellRouteKeys.DeployFromTemplate, StringComparison.Ordinal)
                ? _fromTemplateLane
                : null;

    private UIElement? ActiveRightPanelView() =>
        string.Equals(_activeRouteKey, ShellRouteKeys.DeployQuickDeploy, StringComparison.Ordinal)
            ? _quickDeployRightPanel
            : string.Equals(_activeRouteKey, ShellRouteKeys.DeployFromTemplate, StringComparison.Ordinal)
                ? _fromTemplateRightPanel
                : null;

    private void UpdateRightPanelForActiveLane()
    {
        if (_shellHost is null)
        {
            return;
        }

        var panel = _shellHost.RightPanel;
        var lane = ActiveLane();
        if (lane is null)
        {
            panel.SetContent(null);
            return;
        }

        panel.SetContent(ActiveRightPanelView());
        panel.SetTitle(lane.ResultsPanelTitle);
        if (lane.ShouldAutoOpenResultsPanel)
        {
            panel.RequestAutoOpen();
        }

        lane.ApplyResultsPanelState(isActive: true, panel.IsShown, panel.IsUnavailable);
    }

    private void OnLaneResultsPanelStateChanged() => UpdateRightPanelForActiveLane();

    private void OnRightPanelStateChanged()
    {
        if (_shellHost is null)
        {
            return;
        }

        var panel = _shellHost.RightPanel;
        ActiveLane()?.ApplyResultsPanelState(isActive: true, panel.IsShown, panel.IsUnavailable);
    }

    private void RefreshOverviewSummary() =>
        OverviewViewModel.RefreshSummary(_quickDeployLane?.DraftCount ?? 0, _isTemplatesLoading, _templateItems.Count);

    private async Task ReloadTemplatesAsync(bool forceRefresh)
    {
        _ = forceRefresh;
        if (_templatesCapabilityService is null)
        {
            return;
        }

        _isTemplatesLoading = true;
        RefreshOverviewSummary();
        try
        {
            var result = await _templatesCapabilityService.LoadLibraryAsync();
            _templateItems.Clear();
            foreach (var item in result.Items)
            {
                _templateItems.Add(item);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeployPage] Template library reload failed: {ex}");
        }
        finally
        {
            _isTemplatesLoading = false;
            RefreshOverviewSummary();
        }
    }

    /// <summary>
    /// Marshals per-VM deploy progress callbacks onto the UI thread. Replaces the identical wiring
    /// that lived on the deleted shared <c>DeployCapabilityShellBridge</c>.
    /// </summary>
    private void AttachProgressCallbacks(
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
}
