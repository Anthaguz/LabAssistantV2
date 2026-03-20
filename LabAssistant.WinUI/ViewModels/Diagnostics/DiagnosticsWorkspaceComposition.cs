using System.Collections.ObjectModel;
using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Views.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Diagnostics;

internal interface IDiagnosticsWorkspaceHost
{
    Task<StructuredLogViewerLoadResult> LoadStructuredLogsAsync(StructuredLogViewerFilter filter);

    string GetStructuredLogFilePath();
}

internal interface IDiagnosticsWorkspaceShellBridge
{
    bool IsDiagnosticsCapabilityActive { get; }

    bool IsDiagnosticsOverviewActive { get; }

    bool IsDiagnosticsLogsActive { get; }

    void NavigateToRoute(string routeKey);

    string? OpenStructuredLogLocation(string filePath);
}

internal sealed class DiagnosticsWorkspaceHost : IDiagnosticsWorkspaceHost
{
    private readonly IStructuredLogViewerService _structuredLogViewerService;

    public DiagnosticsWorkspaceHost(IStructuredLogViewerService structuredLogViewerService)
    {
        _structuredLogViewerService = structuredLogViewerService;
    }

    public Task<StructuredLogViewerLoadResult> LoadStructuredLogsAsync(StructuredLogViewerFilter filter) => _structuredLogViewerService.LoadAsync(filter);

    public string GetStructuredLogFilePath() => _structuredLogViewerService.GetStructuredLogFilePath();
}

internal sealed class DiagnosticsWorkspaceShellBridge : IDiagnosticsWorkspaceShellBridge
{
    private readonly Func<bool> _isDiagnosticsCapabilityActive;
    private readonly Func<bool> _isDiagnosticsOverviewActive;
    private readonly Func<bool> _isDiagnosticsLogsActive;
    private readonly Action<string> _navigateToRoute;
    private readonly Func<string, string?> _openStructuredLogLocation;

    public DiagnosticsWorkspaceShellBridge(
        Func<bool> isDiagnosticsCapabilityActive,
        Func<bool> isDiagnosticsOverviewActive,
        Func<bool> isDiagnosticsLogsActive,
        Action<string> navigateToRoute,
        Func<string, string?> openStructuredLogLocation)
    {
        _isDiagnosticsCapabilityActive = isDiagnosticsCapabilityActive;
        _isDiagnosticsOverviewActive = isDiagnosticsOverviewActive;
        _isDiagnosticsLogsActive = isDiagnosticsLogsActive;
        _navigateToRoute = navigateToRoute;
        _openStructuredLogLocation = openStructuredLogLocation;
    }

    public bool IsDiagnosticsCapabilityActive => _isDiagnosticsCapabilityActive();

    public bool IsDiagnosticsOverviewActive => _isDiagnosticsOverviewActive();

    public bool IsDiagnosticsLogsActive => _isDiagnosticsLogsActive();

    public void NavigateToRoute(string routeKey) => _navigateToRoute(routeKey);

    public string? OpenStructuredLogLocation(string filePath) => _openStructuredLogLocation(filePath);
}

internal sealed class DiagnosticsWorkspaceComposition
    : IDiagnosticsLogsWorkspaceControllerHost
{
    private readonly FrameworkElement _localNavigationHost;
    private readonly DiagnosticsOverviewWorkspaceComposition _overviewWorkspaceComposition;
    private readonly DiagnosticsLogsView _logsView;
    private readonly DiagnosticsLogsWorkspaceViewModel _logsWorkspace = new();
    private readonly DiagnosticsLogsWorkspaceController _logsController;
    private readonly TabView _subviewTabView;
    private readonly TabViewItem _overviewTabViewItem;
    private readonly TabViewItem _logsTabViewItem;
    private readonly IDiagnosticsWorkspaceHost _host;
    private readonly IDiagnosticsWorkspaceShellBridge _shellBridge;
    private readonly ObservableCollection<StructuredLogViewerEntry> _structuredLogEntries = [];
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
        _logsView = logsView;
        _subviewTabView = subviewTabView;
        _overviewTabViewItem = overviewTabViewItem;
        _logsTabViewItem = logsTabViewItem;
        _host = host;
        _shellBridge = shellBridge;
        _logsController = new DiagnosticsLogsWorkspaceController(_logsWorkspace, this);
        _overviewWorkspaceComposition = new DiagnosticsOverviewWorkspaceComposition(
            overviewView,
            new DiagnosticsOverviewWorkspaceHost(
                _host.GetStructuredLogFilePath,
                statusText => _logsView.LogsStatusTextBlock.Text = statusText),
            new DiagnosticsOverviewWorkspaceShellBridge(
                () => _shellBridge.IsDiagnosticsOverviewActive,
                _shellBridge.NavigateToRoute,
                _shellBridge.OpenStructuredLogLocation));
        _logsView.StructuredLogsListView.ItemsSource = _structuredLogEntries;
        WireSharedHandlers();
        ApplyLogsWorkspaceState(isLoading: false);
        RefreshOverviewSummary();
    }

    public void ApplyShellState()
    {
        _localNavigationHost.Visibility = _shellBridge.IsDiagnosticsCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        _logsView.Visibility = _shellBridge.IsDiagnosticsLogsActive ? Visibility.Visible : Visibility.Collapsed;
        _overviewWorkspaceComposition.ApplyShellState();

        SyncDiagnosticsSubviewSelection();

        if (_shellBridge.IsDiagnosticsLogsActive)
        {
            _ = _logsController.EnsureLogsLoadedAsync(forceReload: false);
        }
    }

    private void WireSharedHandlers()
    {
        _subviewTabView.SelectionChanged += DiagnosticsSubviewTabView_SelectionChanged;
        _logsView.FilterStateChanged += LogsView_FilterStateChanged;
        _logsView.ApplyFiltersRequested += ApplyLogFiltersButton_Click;
        _logsView.ClearFiltersRequested += ClearLogFiltersButton_Click;
        _logsView.ReloadLogsButton.Click += ReloadLogsButton_Click;
        _logsView.OpenRawJsonlButton.Click += OpenRawJsonlButton_Click;
        _logsView.SelectedLogChanged += LogsView_SelectedLogChanged;
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
        await _logsController.EnsureLogsLoadedAsync(forceReload: true);
    }

    private void OpenRawJsonlButton_Click(object sender, RoutedEventArgs e)
    {
        _logsController.OpenRawLogLocation();
    }

    private void RefreshOverviewSummary()
    {
        _overviewWorkspaceComposition.RefreshSummary(_logsController.IsLoading, _structuredLogEntries.Count);
    }

    private void LogsView_FilterStateChanged(object? sender, EventArgs e)
    {
        _logsController.HandleFilterStateChanged(_logsView.CaptureFilterState());
    }

    private async void ApplyLogFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        await _logsController.ApplyFiltersAsync(_logsView.CaptureFilterState());
    }

    private async void ClearLogFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        await _logsController.ClearFiltersAsync();
    }

    private void LogsView_SelectedLogChanged(object? sender, EventArgs e)
    {
        _logsController.HandleSelectionChanged(_logsView.CaptureSelectedLogEntry());
    }

    bool IDiagnosticsLogsWorkspaceControllerHost.IsLogsActive => _shellBridge.IsDiagnosticsLogsActive;

    int IDiagnosticsLogsWorkspaceControllerHost.StructuredLogEntryCount => _structuredLogEntries.Count;

    Task<StructuredLogViewerLoadResult> IDiagnosticsLogsWorkspaceControllerHost.LoadStructuredLogsAsync(StructuredLogViewerFilter filter)
        => _host.LoadStructuredLogsAsync(filter);

    string IDiagnosticsLogsWorkspaceControllerHost.GetStructuredLogFilePath()
        => _host.GetStructuredLogFilePath();

    string? IDiagnosticsLogsWorkspaceControllerHost.OpenStructuredLogLocation(string filePath)
        => _shellBridge.OpenStructuredLogLocation(filePath);

    void IDiagnosticsLogsWorkspaceControllerHost.ReplaceStructuredLogEntries(IReadOnlyList<StructuredLogViewerEntry> entries)
    {
        _structuredLogEntries.Clear();
        foreach (var entry in entries)
        {
            _structuredLogEntries.Add(entry);
        }
    }

    void IDiagnosticsLogsWorkspaceControllerHost.ApplyWorkspaceState(bool isLoading)
    {
        ApplyLogsWorkspaceState(isLoading);
        RefreshOverviewSummary();
    }

    private void ApplyLogsWorkspaceState(bool isLoading)
    {
        _logsView.ApplyFilterState(_logsWorkspace.BuildViewState());
        _logsView.ApplySelectionState(_logsWorkspace.BuildSelectionViewState());
        _logsView.ApplyLogFiltersButton.IsEnabled = !isLoading;
        _logsView.ClearLogFiltersButton.IsEnabled = !isLoading;
        _logsView.ReloadLogsButton.IsEnabled = !isLoading;
        _logsView.OpenRawJsonlButton.IsEnabled = !isLoading;
        _logsView.LogsStatusTextBlock.Text = _logsWorkspace.StatusText;
    }
}
