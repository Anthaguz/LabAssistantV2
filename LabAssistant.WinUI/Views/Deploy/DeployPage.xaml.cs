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
/// The Quick Deploy and From Template lanes are both full x:Bind MVVM: their view models
/// (<see cref="DeployQuickDeployViewModel"/> and <see cref="DeployFromTemplateViewModel"/>) own their
/// state, commands, and workflow (via preserved controllers) and are bound directly by the subviews.
/// This page composes their dependency graph from DI and reconciles the shell right panel for them.
/// </remarks>
public sealed partial class DeployPage : Page, ICapabilityPage
{
    private readonly ObservableCollection<TemplateLibraryItem> _templateItems = new();

    private IShellHost? _shellHost;
    private ITemplatesCapabilityService? _templatesCapabilityService;
    private DeployTemplatesShellAdapter? _templatesShellAdapter;
    private DeployQuickDeployViewModel? _quickDeployLane;
    private DeployFromTemplateViewModel? _fromTemplateLane;
    private DeployQuickDeployRightPanelView? _quickDeployRightPanel;
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
        if (_quickDeployLane is not null)
        {
            _quickDeployLane.SharedUiStateChanged -= OnLaneSharedUiStateChanged;
            _quickDeployLane.ResultsPanelStateChanged -= OnQuickDeployResultsPanelStateChanged;
        }

        if (_fromTemplateLane is not null)
        {
            _fromTemplateLane.ResultsPanelStateChanged -= OnFromTemplateResultsPanelStateChanged;
        }

        QuickDeployViewHost.ViewModel = null;
        FromTemplateViewHost.ViewModel = null;
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

        var templateEditorHandoff = services.GetRequiredService<ViewModels.Templates.ITemplateEditorHandoff>();
        _templatesShellAdapter = new DeployTemplatesShellAdapter(
            _templateItems,
            () => _isTemplatesLoading,
            () => _templateItems.ToList(),
            forceRefresh => ReloadTemplatesAsync(forceRefresh),
            filePath => _templatesCapabilityService!.LoadForEditorAsync(filePath),
            (document, statusText) => templateEditorHandoff.ShowInEditorAsync(document, statusText));

        var templateEditorLauncher = new DeployTemplateEditorLauncher(_templatesShellAdapter);

        _quickDeployRightPanel = new DeployQuickDeployRightPanelView();
        _fromTemplateRightPanel = new DeployFromTemplateRightPanelView();

        _quickDeployLane = new DeployQuickDeployViewModel(
            referenceDataService,
            resolveSuggestionsService,
            templateEditorLauncher.ShowEditorAsync,
            deploymentPreflightService,
            deploymentCoordinator,
            deploymentOutcomeSummaryBuilder,
            action => DispatcherQueue.TryEnqueue(() => action()),
            ShowRemoveVmEntryConfirmationDialogAsync,
            () => _shellHost?.RightPanel.Toggle());
        _quickDeployLane.SharedUiStateChanged += OnLaneSharedUiStateChanged;
        _quickDeployLane.ResultsPanelStateChanged += OnQuickDeployResultsPanelStateChanged;
        QuickDeployViewHost.ViewModel = _quickDeployLane;

        _fromTemplateLane = new DeployFromTemplateViewModel(
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
                () => _shellHost?.RightPanel.Toggle()),
            action => DispatcherQueue.TryEnqueue(() => action()),
            _templatesShellAdapter.ItemsSource);
        _fromTemplateLane.ResultsPanelStateChanged += OnFromTemplateResultsPanelStateChanged;
        FromTemplateViewHost.ViewModel = _fromTemplateLane;

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

    private void OnQuickDeployResultsPanelStateChanged(object? sender, EventArgs e)
    {
        if (_quickDeployLane is not null && _quickDeployRightPanel is not null)
        {
            _quickDeployRightPanel.ViewModel.UpdateState(
                _quickDeployLane.LifecycleState,
                _quickDeployLane.ProgressPercent,
                _quickDeployLane.ProgressSummary,
                _quickDeployLane.ResultRows.ToList());
        }

        UpdateRightPanelForActiveLane();
    }

    /// <summary>
    /// Projects the From Template lane's credential-slot state onto the shell right panel. Progress and
    /// results for this lane render in-tab, so the right panel only mirrors the credential-slot surface.
    /// </summary>
    private void OnFromTemplateResultsPanelStateChanged(object? sender, EventArgs e)
    {
        if (_fromTemplateLane is not null && _fromTemplateRightPanel is not null)
        {
            var projection = _fromTemplateLane.ProjectCredentialPanel();
            var panelViewModel = _fromTemplateRightPanel.ViewModel;
            switch (projection.Kind)
            {
                case DeployFromTemplateCredentialPanelKind.Reset:
                    panelViewModel.Reset(projection.StatusMessage);
                    break;
                case DeployFromTemplateCredentialPanelKind.Loading:
                    panelViewModel.ShowLoading(projection.StatusMessage);
                    break;
                case DeployFromTemplateCredentialPanelKind.Slots:
                    panelViewModel.UpdateSlots(projection.Slots, projection.AllSlotsResolved, projection.StatusMessage);
                    break;
            }
        }

        UpdateRightPanelForActiveLane();
    }

    /// <summary>
    /// Shows the Quick Deploy remove-entry confirmation. Replaces the identical prompt that lived on
    /// the deleted Quick Deploy shell bridge; the lane view model now depends only on this injected
    /// confirmation seam.
    /// </summary>
    private async Task<bool> ShowRemoveVmEntryConfirmationDialogAsync(string vmName)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = _shellHost?.XamlRoot,
            Title = "Remove VM Entry",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            Content = $"Remove '{vmName}' from quick deploy configuration?",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

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
}
