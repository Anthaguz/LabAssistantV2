using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.Views.Deploy;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal interface IDeployWorkspaceShellBridge
{
    bool IsDeployCapabilityActive { get; }

    bool IsDeployOverviewActive { get; }

    bool IsDeployOnTheFlyActive { get; }

    bool IsDeployFromTemplateActive { get; }

    void NavigateToRoute(string routeKey);
}

internal interface IDeployWorkspaceHost
{
    int QuickDeployDraftCount { get; }

    bool IsLoadingTemplates { get; }

    int AvailableTemplateCount { get; }
}

internal sealed class DeployWorkspaceShellBridge : IDeployWorkspaceShellBridge
{
    private readonly Func<bool> _isDeployCapabilityActive;
    private readonly Func<bool> _isDeployOverviewActive;
    private readonly Func<bool> _isDeployOnTheFlyActive;
    private readonly Func<bool> _isDeployFromTemplateActive;
    private readonly Action<string> _navigateToRoute;

    public DeployWorkspaceShellBridge(
        Func<bool> isDeployCapabilityActive,
        Func<bool> isDeployOverviewActive,
        Func<bool> isDeployOnTheFlyActive,
        Func<bool> isDeployFromTemplateActive,
        Action<string> navigateToRoute)
    {
        _isDeployCapabilityActive = isDeployCapabilityActive;
        _isDeployOverviewActive = isDeployOverviewActive;
        _isDeployOnTheFlyActive = isDeployOnTheFlyActive;
        _isDeployFromTemplateActive = isDeployFromTemplateActive;
        _navigateToRoute = navigateToRoute;
    }

    public bool IsDeployCapabilityActive => _isDeployCapabilityActive();

    public bool IsDeployOverviewActive => _isDeployOverviewActive();

    public bool IsDeployOnTheFlyActive => _isDeployOnTheFlyActive();

    public bool IsDeployFromTemplateActive => _isDeployFromTemplateActive();

    public void NavigateToRoute(string routeKey) => _navigateToRoute(routeKey);
}

internal sealed class DeployWorkspaceHost : IDeployWorkspaceHost
{
    private readonly Func<int> _quickDeployDraftCount;
    private readonly Func<bool> _isLoadingTemplates;
    private readonly Func<int> _availableTemplateCount;

    public DeployWorkspaceHost(
        Func<int> quickDeployDraftCount,
        Func<bool> isLoadingTemplates,
        Func<int> availableTemplateCount)
    {
        _quickDeployDraftCount = quickDeployDraftCount;
        _isLoadingTemplates = isLoadingTemplates;
        _availableTemplateCount = availableTemplateCount;
    }

    public int QuickDeployDraftCount => _quickDeployDraftCount();

    public bool IsLoadingTemplates => _isLoadingTemplates();

    public int AvailableTemplateCount => _availableTemplateCount();
}

internal sealed class DeployWorkspaceComposition
{
    private readonly FrameworkElement _localNavigationHost;
    private readonly FrameworkElement _overviewHost;
    private readonly FrameworkElement _onTheFlyHost;
    private readonly FrameworkElement _fromTemplateHost;
    private readonly DeployOverviewView _overviewView;
    private readonly TabView _subviewTabView;
    private readonly TabViewItem _overviewTabViewItem;
    private readonly TabViewItem _quickDeployTabViewItem;
    private readonly TabViewItem _fromTemplateTabViewItem;
    private readonly IDeployWorkspaceHost _host;
    private readonly IDeployWorkspaceShellBridge _shellBridge;
    private bool _isUpdatingDeploySubviewSelection;

    public DeployWorkspaceComposition(
        FrameworkElement localNavigationHost,
        DeployOverviewView overviewView,
        FrameworkElement onTheFlyHost,
        FrameworkElement fromTemplateHost,
        TabView subviewTabView,
        TabViewItem overviewTabViewItem,
        TabViewItem quickDeployTabViewItem,
        TabViewItem fromTemplateTabViewItem,
        IDeployWorkspaceHost host,
        IDeployWorkspaceShellBridge shellBridge)
    {
        _localNavigationHost = localNavigationHost;
        _overviewHost = overviewView;
        _onTheFlyHost = onTheFlyHost;
        _fromTemplateHost = fromTemplateHost;
        _overviewView = overviewView;
        _subviewTabView = subviewTabView;
        _overviewTabViewItem = overviewTabViewItem;
        _quickDeployTabViewItem = quickDeployTabViewItem;
        _fromTemplateTabViewItem = fromTemplateTabViewItem;
        _host = host;
        _shellBridge = shellBridge;
        WireSharedHandlers();
    }

    public void ApplyShellState()
    {
        _localNavigationHost.Visibility = _shellBridge.IsDeployCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        _overviewHost.Visibility = _shellBridge.IsDeployOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        _onTheFlyHost.Visibility = _shellBridge.IsDeployOnTheFlyActive ? Visibility.Visible : Visibility.Collapsed;
        _fromTemplateHost.Visibility = _shellBridge.IsDeployFromTemplateActive ? Visibility.Visible : Visibility.Collapsed;

        SyncDeploySubviewSelection();

        if (_shellBridge.IsDeployOverviewActive)
        {
            UpdateOverviewUi();
        }
    }

    private void WireSharedHandlers()
    {
        _subviewTabView.SelectionChanged += DeploySubviewTabView_SelectionChanged;
        _overviewView.DeployOverviewOpenQuickDeployButtonControl.Click += (_, _) => _shellBridge.NavigateToRoute(ShellRouteKeys.DeployOnTheFly);
        _overviewView.DeployOverviewOpenFromTemplateButtonControl.Click += (_, _) => _shellBridge.NavigateToRoute(ShellRouteKeys.DeployFromTemplate);
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
            _shellBridge.NavigateToRoute(ShellRouteKeys.DeployOnTheFly);
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

    private void UpdateOverviewUi()
    {
        _overviewView.DeployOverviewQuickDeploySummaryTextBlockControl.Text = _host.QuickDeployDraftCount > 0
            ? $"{_host.QuickDeployDraftCount} VM entries currently staged in the Quick Deploy draft."
            : "Open Quick Deploy to configure VM entries and run deployment.";
        _overviewView.DeployOverviewFromTemplateSummaryTextBlockControl.Text = _host.IsLoadingTemplates
            ? "Template inventory is loading."
            : _host.AvailableTemplateCount > 0
                ? $"{_host.AvailableTemplateCount} templates currently available for From Template."
                : "Open From Template to load template inventory and review readiness.";
    }
}
