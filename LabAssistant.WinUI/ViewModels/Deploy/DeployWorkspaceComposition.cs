using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.Views.Deploy;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployWorkspaceComposition
{
    private readonly FrameworkElement _localNavigationHost;
    private readonly FrameworkElement _overviewHost;
    private readonly IDeployQuickDeployLane _quickDeployLane;
    private readonly TabView _subviewTabView;
    private readonly TabViewItem _overviewTabViewItem;
    private readonly TabViewItem _quickDeployTabViewItem;
    private readonly TabViewItem _fromTemplateTabViewItem;
    private readonly DeployOverviewWorkspaceComposition _overviewWorkspaceComposition;
    private readonly IDeployFromTemplateLane _fromTemplateLane;
    private readonly IDeployWorkspaceShellBridge _shellBridge;
    private bool _isUpdatingDeploySubviewSelection;

    public DeployWorkspaceComposition(
        FrameworkElement localNavigationHost,
        DeployOverviewView overviewView,
        IDeployQuickDeployLane quickDeployLane,
        TabView subviewTabView,
        TabViewItem overviewTabViewItem,
        TabViewItem quickDeployTabViewItem,
        TabViewItem fromTemplateTabViewItem,
        Func<DeployWorkspaceUiState> getUiState,
        IDeployFromTemplateLane fromTemplateLane,
        IDeployWorkspaceShellBridge shellBridge)
    {
        _localNavigationHost = localNavigationHost;
        _overviewHost = overviewView;
        _quickDeployLane = quickDeployLane;
        _subviewTabView = subviewTabView;
        _overviewTabViewItem = overviewTabViewItem;
        _quickDeployTabViewItem = quickDeployTabViewItem;
        _fromTemplateTabViewItem = fromTemplateTabViewItem;
        _fromTemplateLane = fromTemplateLane;
        _shellBridge = shellBridge;
        _overviewWorkspaceComposition = new DeployOverviewWorkspaceComposition(
            overviewView,
            new DeployOverviewWorkspaceHost(
                () => getUiState().QuickDeployDraftCount,
                () => getUiState().IsLoadingTemplates,
                () => getUiState().AvailableTemplateCount),
            new DeployOverviewWorkspaceShellBridge(
                () => _shellBridge.IsDeployOverviewActive,
                _shellBridge.NavigateToRoute));
        _quickDeployLane.SharedUiStateChanged += OnQuickDeployLane_SharedUiStateChanged;
        WireSharedHandlers();
    }

    public void RefreshSharedUiState() => _overviewWorkspaceComposition.RefreshUiState();

    public void ApplyShellState()
    {
        _localNavigationHost.Visibility = _shellBridge.IsDeployCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        _overviewHost.Visibility = _shellBridge.IsDeployOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        _quickDeployLane.ApplyShellState(_shellBridge.IsDeployOnTheFlyActive);

        SyncDeploySubviewSelection();

        if (_shellBridge.IsDeployOverviewActive)
        {
            _overviewWorkspaceComposition.ApplyShellState();
        }

        _fromTemplateLane.ApplyShellState(_shellBridge.IsDeployFromTemplateActive);
    }

    private void WireSharedHandlers()
    {
        _subviewTabView.SelectionChanged += DeploySubviewTabView_SelectionChanged;
    }

    private void OnQuickDeployLane_SharedUiStateChanged(object? sender, EventArgs e)
    {
        RefreshSharedUiState();
    }

    private void DeploySubviewTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingDeploySubviewSelection || _subviewTabView.SelectedItem is not TabViewItem selectedTab)
        {
            return;
        }

        if (ReferenceEquals(selectedTab, _overviewTabViewItem))
        {
            _shellBridge.NavigateToRoute(ShellRouteKeys.DeployOverview);
        }
        else if (ReferenceEquals(selectedTab, _quickDeployTabViewItem))
        {
            _shellBridge.NavigateToRoute(ShellRouteKeys.DeployQuickDeploy);
        }
        else if (ReferenceEquals(selectedTab, _fromTemplateTabViewItem))
        {
            _shellBridge.NavigateToRoute(ShellRouteKeys.DeployFromTemplate);
        }
    }

    private void SyncDeploySubviewSelection()
    {
        if (!_shellBridge.IsDeployCapabilityActive)
        {
            return;
        }

        var selectedTab = _shellBridge.IsDeployOverviewActive
            ? _overviewTabViewItem
            : _shellBridge.IsDeployOnTheFlyActive
                ? _quickDeployTabViewItem
                : _fromTemplateTabViewItem;

        if (ReferenceEquals(_subviewTabView.SelectedItem, selectedTab))
        {
            return;
        }

        _isUpdatingDeploySubviewSelection = true;
        try
        {
            _subviewTabView.SelectedItem = selectedTab;
        }
        finally
        {
            _isUpdatingDeploySubviewSelection = false;
        }
    }
}
