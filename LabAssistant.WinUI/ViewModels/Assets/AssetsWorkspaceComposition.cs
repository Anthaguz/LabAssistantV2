using System.Collections.ObjectModel;
using LabAssistant.WinUI.Models.Assets;
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
    bool IsAssetsSwitchesLoading { get; }

    int AssetsSwitchCount { get; }

    Task EnsureAssetsBaseDisksAsync(bool forceRefresh);

    Task EnsureAssetsSwitchesAsync(bool forceRefresh);

    void UpdateAssetsBaseDisksUi();

    void UpdateAssetsSwitchesUi();
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
    private readonly Func<bool> _isAssetsSwitchesLoading;
    private readonly Func<int> _getAssetsSwitchCount;
    private readonly Func<bool, Task> _ensureAssetsBaseDisksAsync;
    private readonly Func<bool, Task> _ensureAssetsSwitchesAsync;
    private readonly Action _updateAssetsBaseDisksUi;
    private readonly Action _updateAssetsSwitchesUi;

    public AssetsWorkspaceHost(
        Func<bool> isAssetsSwitchesLoading,
        Func<int> getAssetsSwitchCount,
        Func<bool, Task> ensureAssetsBaseDisksAsync,
        Func<bool, Task> ensureAssetsSwitchesAsync,
        Action updateAssetsBaseDisksUi,
        Action updateAssetsSwitchesUi)
    {
        _isAssetsSwitchesLoading = isAssetsSwitchesLoading;
        _getAssetsSwitchCount = getAssetsSwitchCount;
        _ensureAssetsBaseDisksAsync = ensureAssetsBaseDisksAsync;
        _ensureAssetsSwitchesAsync = ensureAssetsSwitchesAsync;
        _updateAssetsBaseDisksUi = updateAssetsBaseDisksUi;
        _updateAssetsSwitchesUi = updateAssetsSwitchesUi;
    }

    public bool IsAssetsSwitchesLoading => _isAssetsSwitchesLoading();

    public int AssetsSwitchCount => _getAssetsSwitchCount();

    public Task EnsureAssetsBaseDisksAsync(bool forceRefresh) => _ensureAssetsBaseDisksAsync(forceRefresh);

    public Task EnsureAssetsSwitchesAsync(bool forceRefresh) => _ensureAssetsSwitchesAsync(forceRefresh);

    public void UpdateAssetsBaseDisksUi() => _updateAssetsBaseDisksUi();

    public void UpdateAssetsSwitchesUi() => _updateAssetsSwitchesUi();
}

internal sealed class AssetsWorkspaceComposition
{
    private readonly AssetsBaseDisksView _baseDisksView;
    private readonly AssetsSwitchesView _switchesView;
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
        AssetsBaseDisksView baseDisksView,
        AssetsSwitchesView switchesView,
        TabView subviewTabView,
        TabViewItem overviewTabViewItem,
        TabViewItem baseDisksTabViewItem,
        TabViewItem switchesTabViewItem,
        AssetsBaseDisksWorkspaceViewModel baseDisksWorkspace,
        ObservableCollection<AssetsSwitchListRow> switchRows,
        ObservableCollection<string> attachedVmNames,
        IAssetsWorkspaceHost host,
        IAssetsWorkspaceShellBridge shellBridge)
    {
        _baseDisksView = baseDisksView;
        _switchesView = switchesView;
        _subviewTabView = subviewTabView;
        _overviewTabViewItem = overviewTabViewItem;
        _baseDisksTabViewItem = baseDisksTabViewItem;
        _switchesTabViewItem = switchesTabViewItem;
        _host = host;
        _shellBridge = shellBridge;
        _overviewWorkspaceComposition = new AssetsOverviewWorkspaceComposition(
            overviewView,
            new AssetsOverviewWorkspaceHost(
                () => baseDisksWorkspace.IsLoading,
                () => _host.IsAssetsSwitchesLoading,
                () => baseDisksWorkspace.Inventory.Count,
                () => switchRows.Count),
            new AssetsOverviewWorkspaceShellBridge(
                () => _shellBridge.IsAssetsOverviewActive,
                _shellBridge.NavigateToRoute));

        _baseDisksView.AssetsBaseDisksListViewControl.ItemsSource = baseDisksWorkspace.Inventory;
        _switchesView.AssetsSwitchesListViewControl.ItemsSource = switchRows;
        _switchesView.AssetsSwitchesAttachedVmsListViewControl.ItemsSource = attachedVmNames;
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
            _ = _host.EnsureAssetsBaseDisksAsync(forceRefresh: false);
            _host.UpdateAssetsBaseDisksUi();
        }

        if (_shellBridge.IsAssetsSwitchesActive)
        {
            _ = _host.EnsureAssetsSwitchesAsync(forceRefresh: false);
            _host.UpdateAssetsSwitchesUi();
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
