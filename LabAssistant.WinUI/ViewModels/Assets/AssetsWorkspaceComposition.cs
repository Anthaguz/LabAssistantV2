using LabAssistant.WinUI.Views.Assets;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Assets;

internal interface IAssetsWorkspaceShellBridge
{
    bool IsAssetsCapabilityActive { get; }

    bool IsAssetsOverviewActive { get; }

    bool IsAssetsBaseDisksActive { get; }

    bool IsAssetsSwitchesActive { get; }

    void NavigateToRoute(string routeKey);
}

internal interface IAssetsWorkspaceHost
{
}

internal sealed class AssetsWorkspaceShellBridge : IAssetsWorkspaceShellBridge
{
    private readonly Func<bool> _isAssetsCapabilityActive;
    private readonly Func<bool> _isAssetsOverviewActive;
    private readonly Func<bool> _isAssetsBaseDisksActive;
    private readonly Func<bool> _isAssetsSwitchesActive;
    private readonly Action<string> _navigateToRoute;

    public AssetsWorkspaceShellBridge(
        Func<bool> isAssetsCapabilityActive,
        Func<bool> isAssetsOverviewActive,
        Func<bool> isAssetsBaseDisksActive,
        Func<bool> isAssetsSwitchesActive,
        Action<string> navigateToRoute)
    {
        _isAssetsCapabilityActive = isAssetsCapabilityActive;
        _isAssetsOverviewActive = isAssetsOverviewActive;
        _isAssetsBaseDisksActive = isAssetsBaseDisksActive;
        _isAssetsSwitchesActive = isAssetsSwitchesActive;
        _navigateToRoute = navigateToRoute;
    }

    public bool IsAssetsCapabilityActive => _isAssetsCapabilityActive();

    public bool IsAssetsOverviewActive => _isAssetsOverviewActive();

    public bool IsAssetsBaseDisksActive => _isAssetsBaseDisksActive();

    public bool IsAssetsSwitchesActive => _isAssetsSwitchesActive();

    public void NavigateToRoute(string routeKey) => _navigateToRoute(routeKey);
}

internal sealed class AssetsWorkspaceHost : IAssetsWorkspaceHost
{
}

internal sealed class AssetsWorkspaceComposition
{
    private readonly AssetsBaseDisksWorkspaceComposition _baseDisksWorkspaceComposition;
    private readonly AssetsSwitchesWorkspaceComposition _switchesWorkspaceComposition;
    private readonly TabView _subviewTabView;
    private readonly TabViewItem _overviewTabViewItem;
    private readonly TabViewItem _baseDisksTabViewItem;
    private readonly TabViewItem _switchesTabViewItem;
    private readonly AssetsOverviewWorkspaceComposition _overviewWorkspaceComposition;
    private readonly IAssetsWorkspaceHost _host;
    private readonly IAssetsWorkspaceShellBridge _shellBridge;
    private bool _isUpdatingAssetsSubviewSelection;

    public AssetsWorkspaceComposition(
        AssetsOverviewView overviewView,
        AssetsBaseDisksWorkspaceComposition baseDisksWorkspaceComposition,
        AssetsSwitchesWorkspaceComposition switchesWorkspaceComposition,
        TabView subviewTabView,
        TabViewItem overviewTabViewItem,
        TabViewItem baseDisksTabViewItem,
        TabViewItem switchesTabViewItem,
        IAssetsWorkspaceHost host,
        IAssetsWorkspaceShellBridge shellBridge)
    {
        _baseDisksWorkspaceComposition = baseDisksWorkspaceComposition;
        _switchesWorkspaceComposition = switchesWorkspaceComposition;
        _subviewTabView = subviewTabView;
        _overviewTabViewItem = overviewTabViewItem;
        _baseDisksTabViewItem = baseDisksTabViewItem;
        _switchesTabViewItem = switchesTabViewItem;
        _host = host;
        _shellBridge = shellBridge;
        _overviewWorkspaceComposition = new AssetsOverviewWorkspaceComposition(
            overviewView,
            new AssetsOverviewWorkspaceHost(
                () => _baseDisksWorkspaceComposition.IsLoading,
                () => _switchesWorkspaceComposition.IsLoading,
                () => _baseDisksWorkspaceComposition.InventoryCount,
                () => _switchesWorkspaceComposition.InventoryCount),
            new AssetsOverviewWorkspaceShellBridge(
                () => _shellBridge.IsAssetsOverviewActive,
                _shellBridge.NavigateToRoute));
        WireSharedHandlers();
    }

    public void ApplyShellState()
    {
        SyncAssetsSubviewSelection();

        if (_shellBridge.IsAssetsOverviewActive)
        {
            _overviewWorkspaceComposition.ApplyShellState();
        }

        if (_shellBridge.IsAssetsBaseDisksActive)
        {
            _baseDisksWorkspaceComposition.ApplyShellState();
        }

        if (_shellBridge.IsAssetsSwitchesActive)
        {
            _switchesWorkspaceComposition.ApplyShellState();
        }
    }

    private void WireSharedHandlers()
    {
        _subviewTabView.SelectionChanged += AssetsSubviewTabView_SelectionChanged;
    }

    private void AssetsSubviewTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingAssetsSubviewSelection || _subviewTabView.SelectedItem is not TabViewItem selectedTab)
        {
            return;
        }

        if (ReferenceEquals(selectedTab, _overviewTabViewItem))
        {
            _shellBridge.NavigateToRoute(ShellRouteKeys.AssetsOverview);
        }
        else if (ReferenceEquals(selectedTab, _baseDisksTabViewItem))
        {
            _shellBridge.NavigateToRoute(ShellRouteKeys.AssetsBaseDisks);
        }
        else if (ReferenceEquals(selectedTab, _switchesTabViewItem))
        {
            _shellBridge.NavigateToRoute(ShellRouteKeys.AssetsSwitches);
        }
    }

    private void SyncAssetsSubviewSelection()
    {
        if (!_shellBridge.IsAssetsCapabilityActive)
        {
            return;
        }

        var selectedTab = _shellBridge.IsAssetsOverviewActive
            ? _overviewTabViewItem
            : _shellBridge.IsAssetsBaseDisksActive
                ? _baseDisksTabViewItem
                : _switchesTabViewItem;

        if (ReferenceEquals(_subviewTabView.SelectedItem, selectedTab))
        {
            return;
        }

        _isUpdatingAssetsSubviewSelection = true;
        try
        {
            _subviewTabView.SelectedItem = selectedTab;
        }
        finally
        {
            _isUpdatingAssetsSubviewSelection = false;
        }
    }
}
