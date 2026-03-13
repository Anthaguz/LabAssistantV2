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

    Task EnsureAssetsBaseDisksAsync(bool forceRefresh);

    Task EnsureAssetsSwitchesAsync(bool forceRefresh);

    void UpdateAssetsOverviewUi();

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
    private readonly Func<bool, Task> _ensureAssetsBaseDisksAsync;
    private readonly Func<bool, Task> _ensureAssetsSwitchesAsync;
    private readonly Action _updateAssetsOverviewUi;
    private readonly Action _updateAssetsBaseDisksUi;
    private readonly Action _updateAssetsSwitchesUi;

    public AssetsWorkspaceShellBridge(
        Func<bool> isAssetsCapabilityActive,
        Func<bool> isAssetsOverviewActive,
        Func<bool> isAssetsBaseDisksActive,
        Func<bool> isAssetsSwitchesActive,
        Action<string> navigateToRoute,
        Func<bool, Task> ensureAssetsBaseDisksAsync,
        Func<bool, Task> ensureAssetsSwitchesAsync,
        Action updateAssetsOverviewUi,
        Action updateAssetsBaseDisksUi,
        Action updateAssetsSwitchesUi)
    {
        _isAssetsCapabilityActive = isAssetsCapabilityActive;
        _isAssetsOverviewActive = isAssetsOverviewActive;
        _isAssetsBaseDisksActive = isAssetsBaseDisksActive;
        _isAssetsSwitchesActive = isAssetsSwitchesActive;
        _navigateToRoute = navigateToRoute;
        _ensureAssetsBaseDisksAsync = ensureAssetsBaseDisksAsync;
        _ensureAssetsSwitchesAsync = ensureAssetsSwitchesAsync;
        _updateAssetsOverviewUi = updateAssetsOverviewUi;
        _updateAssetsBaseDisksUi = updateAssetsBaseDisksUi;
        _updateAssetsSwitchesUi = updateAssetsSwitchesUi;
    }

    public bool IsAssetsCapabilityActive => _isAssetsCapabilityActive();

    public bool IsAssetsOverviewActive => _isAssetsOverviewActive();

    public bool IsAssetsBaseDisksActive => _isAssetsBaseDisksActive();

    public bool IsAssetsSwitchesActive => _isAssetsSwitchesActive();

    public void NavigateToRoute(string routeKey) => _navigateToRoute(routeKey);

    public Task EnsureAssetsBaseDisksAsync(bool forceRefresh) => _ensureAssetsBaseDisksAsync(forceRefresh);

    public Task EnsureAssetsSwitchesAsync(bool forceRefresh) => _ensureAssetsSwitchesAsync(forceRefresh);

    public void UpdateAssetsOverviewUi() => _updateAssetsOverviewUi();

    public void UpdateAssetsBaseDisksUi() => _updateAssetsBaseDisksUi();

    public void UpdateAssetsSwitchesUi() => _updateAssetsSwitchesUi();
}

internal sealed class AssetsWorkspaceComposition
{
    private readonly AssetsOverviewView _overviewView;
    private readonly AssetsBaseDisksView _baseDisksView;
    private readonly AssetsSwitchesView _switchesView;
    private readonly TabView _subviewTabView;
    private readonly TabViewItem _overviewTabViewItem;
    private readonly TabViewItem _baseDisksTabViewItem;
    private readonly TabViewItem _switchesTabViewItem;
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
        ObservableCollection<AssetsBaseDiskListRow> baseDiskRows,
        ObservableCollection<AssetsSwitchListRow> switchRows,
        ObservableCollection<string> attachedVmNames,
        IAssetsWorkspaceShellBridge shellBridge)
    {
        _overviewView = overviewView;
        _baseDisksView = baseDisksView;
        _switchesView = switchesView;
        _subviewTabView = subviewTabView;
        _overviewTabViewItem = overviewTabViewItem;
        _baseDisksTabViewItem = baseDisksTabViewItem;
        _switchesTabViewItem = switchesTabViewItem;
        _shellBridge = shellBridge;

        _baseDisksView.AssetsBaseDisksListViewControl.ItemsSource = baseDiskRows;
        _switchesView.AssetsSwitchesListViewControl.ItemsSource = switchRows;
        _switchesView.AssetsSwitchesAttachedVmsListViewControl.ItemsSource = attachedVmNames;
        WireSharedHandlers();
    }

    public void ApplyShellState()
    {
        SyncAssetsSubviewSelection();

        if (_shellBridge.IsAssetsOverviewActive)
        {
            _shellBridge.UpdateAssetsOverviewUi();
        }

        if (_shellBridge.IsAssetsBaseDisksActive)
        {
            _ = _shellBridge.EnsureAssetsBaseDisksAsync(forceRefresh: false);
            _shellBridge.UpdateAssetsBaseDisksUi();
        }

        if (_shellBridge.IsAssetsSwitchesActive)
        {
            _ = _shellBridge.EnsureAssetsSwitchesAsync(forceRefresh: false);
            _shellBridge.UpdateAssetsSwitchesUi();
        }
    }

    private void WireSharedHandlers()
    {
        _overviewView.AssetsOverviewOpenBaseDisksButtonControl.Click += OpenBaseDisksRequested;
        _overviewView.AssetsOverviewOpenSwitchesButtonControl.Click += OpenSwitchesRequested;
        _subviewTabView.SelectionChanged += AssetsSubviewTabView_SelectionChanged;
    }

    private void OpenBaseDisksRequested(object? sender, object e)
    {
        _shellBridge.NavigateToRoute(ShellRouteKeys.AssetsBaseDisks);
    }

    private void OpenSwitchesRequested(object? sender, object e)
    {
        _shellBridge.NavigateToRoute(ShellRouteKeys.AssetsSwitches);
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
