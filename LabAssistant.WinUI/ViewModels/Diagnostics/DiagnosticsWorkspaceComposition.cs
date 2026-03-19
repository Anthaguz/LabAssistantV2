using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Views.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Diagnostics;

internal interface IDiagnosticsWorkspaceHost
{
    bool IsStructuredLogsLoading { get; }

    int StructuredLogEntryCount { get; }

    Task EnsureStructuredLogsLoadedAsync(bool forceReload);

    void ClearStructuredLogFilters();

    void SetSelectedStructuredLogEntry(StructuredLogViewerEntry? selectedEntry);

    void OpenStructuredLogLocation();
}

internal interface IDiagnosticsWorkspaceShellBridge
{
    bool IsDiagnosticsCapabilityActive { get; }

    bool IsDiagnosticsOverviewActive { get; }

    bool IsDiagnosticsLogsActive { get; }

    void NavigateToRoute(string routeKey);
}

internal sealed class DiagnosticsWorkspaceShellBridge : IDiagnosticsWorkspaceShellBridge
{
    private readonly Func<bool> _isDiagnosticsCapabilityActive;
    private readonly Func<bool> _isDiagnosticsOverviewActive;
    private readonly Func<bool> _isDiagnosticsLogsActive;
    private readonly Action<string> _navigateToRoute;

    public DiagnosticsWorkspaceShellBridge(
        Func<bool> isDiagnosticsCapabilityActive,
        Func<bool> isDiagnosticsOverviewActive,
        Func<bool> isDiagnosticsLogsActive,
        Action<string> navigateToRoute)
    {
        _isDiagnosticsCapabilityActive = isDiagnosticsCapabilityActive;
        _isDiagnosticsOverviewActive = isDiagnosticsOverviewActive;
        _isDiagnosticsLogsActive = isDiagnosticsLogsActive;
        _navigateToRoute = navigateToRoute;
    }

    public bool IsDiagnosticsCapabilityActive => _isDiagnosticsCapabilityActive();

    public bool IsDiagnosticsOverviewActive => _isDiagnosticsOverviewActive();

    public bool IsDiagnosticsLogsActive => _isDiagnosticsLogsActive();

    public void NavigateToRoute(string routeKey) => _navigateToRoute(routeKey);
}

internal sealed class DiagnosticsWorkspaceComposition
{
    private const string SupportSummaryText = "Open the current structured log location for support export or manual diagnostics collection.";

    private readonly FrameworkElement _localNavigationHost;
    private readonly DiagnosticsOverviewView _overviewView;
    private readonly DiagnosticsLogsView _logsView;
    private readonly TabView _subviewTabView;
    private readonly TabViewItem _overviewTabViewItem;
    private readonly TabViewItem _logsTabViewItem;
    private readonly IDiagnosticsWorkspaceHost _host;
    private readonly IDiagnosticsWorkspaceShellBridge _shellBridge;
    private bool _isUpdatingDiagnosticsSubviewSelection;

    public DiagnosticsWorkspaceComposition(
        FrameworkElement localNavigationHost,
        DiagnosticsOverviewView overviewView,
        DiagnosticsLogsView logsView,
        TabView subviewTabView,
        TabViewItem overviewTabViewItem,
        TabViewItem logsTabViewItem,
        IDiagnosticsWorkspaceHost host,
        IDiagnosticsWorkspaceShellBridge shellBridge)
    {
        _localNavigationHost = localNavigationHost;
        _overviewView = overviewView;
        _logsView = logsView;
        _subviewTabView = subviewTabView;
        _overviewTabViewItem = overviewTabViewItem;
        _logsTabViewItem = logsTabViewItem;
        _host = host;
        _shellBridge = shellBridge;
        WireSharedHandlers();
    }

    public void RefreshSharedUiState()
    {
        var logsSummaryText = _host.IsStructuredLogsLoading
            ? "Structured logs are loading."
            : _host.StructuredLogEntryCount > 0
                ? $"{_host.StructuredLogEntryCount} structured log entries are currently loaded."
                : "Open Logs to inspect structured events and current support context.";
        _overviewView.UpdateSummary(logsSummaryText, SupportSummaryText);
    }

    public void ApplyShellState()
    {
        _localNavigationHost.Visibility = _shellBridge.IsDiagnosticsCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        _overviewView.Visibility = _shellBridge.IsDiagnosticsOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        _logsView.Visibility = _shellBridge.IsDiagnosticsLogsActive ? Visibility.Visible : Visibility.Collapsed;

        SyncDiagnosticsSubviewSelection();

        if (_shellBridge.IsDiagnosticsOverviewActive)
        {
            RefreshSharedUiState();
        }

        if (_shellBridge.IsDiagnosticsLogsActive)
        {
            _ = _host.EnsureStructuredLogsLoadedAsync(forceReload: false);
        }
    }

    private void WireSharedHandlers()
    {
        _subviewTabView.SelectionChanged += DiagnosticsSubviewTabView_SelectionChanged;
        _overviewView.OpenLogsRequested += (_, _) => _shellBridge.NavigateToRoute(ShellRouteKeys.DiagnosticsLogs);
        _overviewView.OpenSupportExportRequested += (_, _) => _host.OpenStructuredLogLocation();
        _logsView.ApplyLogFiltersButton.Click += ApplyLogFiltersButton_Click;
        _logsView.ClearLogFiltersButton.Click += ClearLogFiltersButton_Click;
        _logsView.ReloadLogsButton.Click += ReloadLogsButton_Click;
        _logsView.OpenRawJsonlButton.Click += (_, _) => _host.OpenStructuredLogLocation();
        _logsView.StructuredLogsListView.SelectionChanged += StructuredLogsListView_SelectionChanged;
    }

    private void DiagnosticsSubviewTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingDiagnosticsSubviewSelection || _subviewTabView.SelectedItem is not TabViewItem selectedTab)
        {
            return;
        }

        if (ReferenceEquals(selectedTab, _overviewTabViewItem))
        {
            _shellBridge.NavigateToRoute(ShellRouteKeys.DiagnosticsOverview);
        }
        else if (ReferenceEquals(selectedTab, _logsTabViewItem))
        {
            _shellBridge.NavigateToRoute(ShellRouteKeys.DiagnosticsLogs);
        }
    }

    private void SyncDiagnosticsSubviewSelection()
    {
        if (!_shellBridge.IsDiagnosticsCapabilityActive)
        {
            return;
        }

        var selectedTab = _shellBridge.IsDiagnosticsOverviewActive
            ? _overviewTabViewItem
            : _logsTabViewItem;

        if (ReferenceEquals(_subviewTabView.SelectedItem, selectedTab))
        {
            return;
        }

        _isUpdatingDiagnosticsSubviewSelection = true;
        try
        {
            _subviewTabView.SelectedItem = selectedTab;
        }
        finally
        {
            _isUpdatingDiagnosticsSubviewSelection = false;
        }
    }

    private async void ReloadLogsButton_Click(object sender, RoutedEventArgs e)
    {
        await _host.EnsureStructuredLogsLoadedAsync(forceReload: true);
    }

    private async void ApplyLogFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        await _host.EnsureStructuredLogsLoadedAsync(forceReload: true);
    }

    private async void ClearLogFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        _host.ClearStructuredLogFilters();
        await _host.EnsureStructuredLogsLoadedAsync(forceReload: true);
    }

    private void StructuredLogsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _host.SetSelectedStructuredLogEntry(_logsView.StructuredLogsListView.SelectedItem as StructuredLogViewerEntry);
    }
}
