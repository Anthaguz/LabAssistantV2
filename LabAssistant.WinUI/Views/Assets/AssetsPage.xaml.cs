using System.Collections.Specialized;
using System.ComponentModel;
using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Models.Assets;
using LabAssistant.WinUI.Shell;
using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.ViewModels.Assets;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace LabAssistant.WinUI.Views.Assets;

/// <summary>
/// Capability page that owns the Assets surface while it is the active shell content. Hosts the
/// Overview, Base Disks, and Switches subviews in a tab view, forwards shell-driven subview
/// changes, mediates the overview inventory summary (which reflects the base-disk and switch load
/// state), and wires the shell-owned file/confirmation dialogs into the subview view models. It
/// replaces the former capability-runtime plus per-subview workspace-composition delegate layer.
/// </summary>
public sealed partial class AssetsPage : Page, ICapabilityPage
{
    private IShellHost? _shellHost;
    private AssetsBaseDisksViewModel? _observedBaseDisks;
    private AssetsSwitchesViewModel? _observedSwitches;
    private bool _isUpdatingSubviewSelection;

    public AssetsPage()
    {
        InitializeComponent();
    }

    private AssetsOverviewViewModel OverviewViewModel => OverviewViewHost.ViewModel;

    private AssetsBaseDisksViewModel BaseDisksViewModel => BaseDisksViewHost.ViewModel;

    private AssetsSwitchesViewModel SwitchesViewModel => SwitchesViewHost.ViewModel;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var initialRoute = ShellRouteKeys.AssetsOverview;
        if (e.Parameter is ShellNavigationRequest request)
        {
            _shellHost = request.Host;
            initialRoute = request.RouteKey;
        }

        OverviewViewModel.ConfigureNavigation(NavigateToRoute);
        WireBaseDisksCallbacks();
        WireSwitchesCallbacks();
        ObserveInventoryWorkspaces();
        SelectSubviewTab(initialRoute);

        // Load both inventories on entry so the Overview summary reflects real counts even before
        // its sibling tabs are realized. Both loads are idempotent no-ops when already loaded.
        _ = BaseDisksViewModel.EnsureInventoryAsync(forceRefresh: false);
        _ = SwitchesViewModel.EnsureInventoryAsync(forceRefresh: false);
        RefreshOverviewSummary();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        StopObservingInventoryWorkspaces();
        OverviewViewModel.ConfigureNavigation(static _ => { });
        BaseDisksViewModel.PickBaseDiskFilePath = null;
        BaseDisksViewModel.ConfirmRemoveAsync = null;
        SwitchesViewModel.ConfirmDeleteAsync = null;
        _shellHost = null;
    }

    void ICapabilityPage.ShowSubview(string routeKey) => SelectSubviewTab(routeKey);

    private void NavigateToRoute(string routeKey) => _shellHost?.NavigateToRoute(routeKey);

    private void WireBaseDisksCallbacks()
    {
        BaseDisksViewModel.PickBaseDiskFilePath = () => _shellHost?.Dialogs.PickBaseDiskFilePath();
        BaseDisksViewModel.ConfirmRemoveAsync = (item, assessment) =>
        {
            if (_shellHost is null)
            {
                return Task.FromResult(false);
            }

            var row = new AssetsBaseDiskListRow(new AssetsBaseDiskRecord
            {
                Id = item.Id,
                Path = item.Path,
                OsName = item.OsName,
                OsVersion = item.OsVersion,
                Generation = item.Generation,
                Notes = item.Notes
            });
            return _shellHost.Dialogs.ShowAssetsBaseDiskRemoveConfirmationDialogAsync(row, assessment);
        };
    }

    private void WireSwitchesCallbacks()
    {
        SwitchesViewModel.ConfirmDeleteAsync = (item, assessment) =>
        {
            if (_shellHost is null)
            {
                return Task.FromResult(false);
            }

            var row = new AssetsSwitchListRow(new AssetsSwitchRecord
            {
                Name = item.Name,
                SwitchType = item.Type,
                AdapterName = item.AdapterName
            });
            return _shellHost.Dialogs.ShowAssetsSwitchDeleteConfirmationDialogAsync(row, assessment);
        };
    }

    private void SubviewTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSubviewSelection || SubviewTabView.SelectedItem is not TabViewItem selectedTab)
        {
            return;
        }

        var routeKey = ReferenceEquals(selectedTab, BaseDisksTabViewItem)
            ? ShellRouteKeys.AssetsBaseDisks
            : ReferenceEquals(selectedTab, SwitchesTabViewItem)
                ? ShellRouteKeys.AssetsSwitches
                : ShellRouteKeys.AssetsOverview;
        _shellHost?.ReportActiveSubview(routeKey);
    }

    private void SelectSubviewTab(string routeKey)
    {
        var targetTab = string.Equals(routeKey, ShellRouteKeys.AssetsBaseDisks, StringComparison.Ordinal)
            ? BaseDisksTabViewItem
            : string.Equals(routeKey, ShellRouteKeys.AssetsSwitches, StringComparison.Ordinal)
                ? SwitchesTabViewItem
                : OverviewTabViewItem;

        if (ReferenceEquals(SubviewTabView.SelectedItem, targetTab))
        {
            return;
        }

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

    private void ObserveInventoryWorkspaces()
    {
        _observedBaseDisks = BaseDisksViewModel;
        _observedBaseDisks.PropertyChanged += OnInventoryViewModelPropertyChanged;
        _observedBaseDisks.BaseDisks.CollectionChanged += OnInventoryCollectionChanged;

        _observedSwitches = SwitchesViewModel;
        _observedSwitches.PropertyChanged += OnInventoryViewModelPropertyChanged;
        _observedSwitches.Switches.CollectionChanged += OnInventoryCollectionChanged;
    }

    private void StopObservingInventoryWorkspaces()
    {
        if (_observedBaseDisks is not null)
        {
            _observedBaseDisks.PropertyChanged -= OnInventoryViewModelPropertyChanged;
            _observedBaseDisks.BaseDisks.CollectionChanged -= OnInventoryCollectionChanged;
            _observedBaseDisks = null;
        }

        if (_observedSwitches is not null)
        {
            _observedSwitches.PropertyChanged -= OnInventoryViewModelPropertyChanged;
            _observedSwitches.Switches.CollectionChanged -= OnInventoryCollectionChanged;
            _observedSwitches = null;
        }
    }

    private void OnInventoryViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(AssetsBaseDisksViewModel.IsLoading), StringComparison.Ordinal))
        {
            RefreshOverviewSummary();
        }
    }

    private void OnInventoryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshOverviewSummary();

    private void RefreshOverviewSummary() =>
        OverviewViewModel.RefreshSummary(
            BaseDisksViewModel.IsLoading,
            SwitchesViewModel.IsLoading,
            BaseDisksViewModel.BaseDisks.Count,
            SwitchesViewModel.Switches.Count);
}
