using System.Collections.Specialized;
using System.ComponentModel;
using LabAssistant.WinUI.Shell;
using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.ViewModels.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace LabAssistant.WinUI.Views.Diagnostics;

/// <summary>
/// Capability page that owns the Diagnostics surface while it is the active shell content. Hosts
/// the Overview and Logs subviews in a tab view, forwards shell-driven subview changes, and
/// mediates the one genuine cross-subview link (the Overview logs summary reflects Logs load
/// state). It implements the view models' capability seam directly - there is no separate
/// capability-runtime or delegate-bag composition layer.
/// </summary>
public sealed partial class DiagnosticsPage : Page, ICapabilityPage, IDiagnosticsShell
{
    private IShellHost? _shellHost;
    private DiagnosticsLogsViewModel? _observedLogsViewModel;
    private bool _isUpdatingSubviewSelection;

    public DiagnosticsPage()
    {
        InitializeComponent();
    }

    private DiagnosticsOverviewViewModel OverviewViewModel => OverviewViewHost.ViewModel;

    private DiagnosticsLogsViewModel LogsViewModel => LogsViewHost.ViewModel;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var initialRoute = ShellRouteKeys.DiagnosticsOverview;
        if (e.Parameter is ShellNavigationRequest request)
        {
            _shellHost = request.Host;
            initialRoute = request.RouteKey;
        }

        OverviewViewModel.AttachShell(this);
        ObserveLogsWorkspace();
        SelectSubviewTab(initialRoute);
        RefreshLogsSummary();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        StopObservingLogsWorkspace();
        OverviewViewModel.DetachShell();
        _shellHost = null;
    }

    void ICapabilityPage.ShowSubview(string routeKey) => SelectSubviewTab(routeKey);

    void IDiagnosticsShell.ShowLogs() => _shellHost?.NavigateToRoute(ShellRouteKeys.DiagnosticsLogs);

    void IDiagnosticsShell.ReportSupportStatus(string statusText) => LogsViewModel.StatusText = statusText;

    private void SubviewTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSubviewSelection || SubviewTabView.SelectedItem is not TabViewItem selectedTab)
        {
            return;
        }

        var routeKey = ReferenceEquals(selectedTab, LogsTabViewItem)
            ? ShellRouteKeys.DiagnosticsLogs
            : ShellRouteKeys.DiagnosticsOverview;
        _shellHost?.ReportActiveSubview(routeKey);
    }

    private void SelectSubviewTab(string routeKey)
    {
        var targetTab = string.Equals(routeKey, ShellRouteKeys.DiagnosticsLogs, StringComparison.Ordinal)
            ? LogsTabViewItem
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

    private void ObserveLogsWorkspace()
    {
        _observedLogsViewModel = LogsViewModel;
        _observedLogsViewModel.PropertyChanged += OnLogsViewModelPropertyChanged;
        _observedLogsViewModel.Entries.CollectionChanged += OnLogsEntriesChanged;
    }

    private void StopObservingLogsWorkspace()
    {
        if (_observedLogsViewModel is null)
        {
            return;
        }

        _observedLogsViewModel.PropertyChanged -= OnLogsViewModelPropertyChanged;
        _observedLogsViewModel.Entries.CollectionChanged -= OnLogsEntriesChanged;
        _observedLogsViewModel = null;
    }

    private void OnLogsViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(DiagnosticsLogsViewModel.IsBusy), StringComparison.Ordinal))
        {
            RefreshLogsSummary();
        }
    }

    private void OnLogsEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshLogsSummary();

    private void RefreshLogsSummary() =>
        OverviewViewModel.RefreshLogsSummary(LogsViewModel.IsBusy, LogsViewModel.Entries.Count);
}
