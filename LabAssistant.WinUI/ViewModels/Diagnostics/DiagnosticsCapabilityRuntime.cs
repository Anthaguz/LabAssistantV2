using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Views.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Diagnostics;

internal interface IDiagnosticsCapabilityHost
{
    Task<StructuredLogViewerLoadResult> LoadStructuredLogsAsync(StructuredLogViewerFilter filter);

    string GetStructuredLogFilePath();
}

internal interface IDiagnosticsCapabilityShellBridge
{
    bool IsDiagnosticsCapabilityActive { get; }

    bool IsDiagnosticsOverviewActive { get; }

    bool IsDiagnosticsLogsActive { get; }

    void NavigateToRoute(string routeKey);

    string? OpenStructuredLogLocation(string filePath);
}

internal sealed class DiagnosticsCapabilityHost : IDiagnosticsCapabilityHost
{
    private readonly IStructuredLogViewerService _structuredLogViewerService;

    public DiagnosticsCapabilityHost(IStructuredLogViewerService structuredLogViewerService)
    {
        _structuredLogViewerService = structuredLogViewerService;
    }

    public Task<StructuredLogViewerLoadResult> LoadStructuredLogsAsync(StructuredLogViewerFilter filter) => _structuredLogViewerService.LoadAsync(filter);

    public string GetStructuredLogFilePath() => _structuredLogViewerService.GetStructuredLogFilePath();
}

internal sealed class DiagnosticsCapabilityShellBridge : IDiagnosticsCapabilityShellBridge
{
    private readonly Func<bool> _isDiagnosticsCapabilityActive;
    private readonly Func<bool> _isDiagnosticsOverviewActive;
    private readonly Func<bool> _isDiagnosticsLogsActive;
    private readonly Action<string> _navigateToRoute;
    private readonly Func<string, string?> _openStructuredLogLocation;

    public DiagnosticsCapabilityShellBridge(
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

internal sealed class DiagnosticsCapabilityRuntime
{
    private readonly FrameworkElement _localNavigationHost;
    private readonly DiagnosticsOverviewWorkspaceComposition _overviewWorkspaceComposition;
    private readonly DiagnosticsLogsWorkspaceComposition _logsWorkspaceComposition;
    private readonly TabView _subviewTabView;
    private readonly TabViewItem _overviewTabViewItem;
    private readonly TabViewItem _logsTabViewItem;
    private readonly IDiagnosticsCapabilityHost _host;
    private readonly IDiagnosticsCapabilityShellBridge _shellBridge;
    private bool _isUpdatingDiagnosticsSubviewSelection;

    public DiagnosticsCapabilityRuntime(
        FrameworkElement localNavigationHost,
        DiagnosticsOverviewView overviewView,
        DiagnosticsLogsView logsView,
        TabView subviewTabView,
        TabViewItem overviewTabViewItem,
        TabViewItem logsTabViewItem,
        IDiagnosticsCapabilityHost host,
        IDiagnosticsCapabilityShellBridge shellBridge)
    {
        _localNavigationHost = localNavigationHost;
        _subviewTabView = subviewTabView;
        _overviewTabViewItem = overviewTabViewItem;
        _logsTabViewItem = logsTabViewItem;
        _host = host;
        _shellBridge = shellBridge;
        _logsWorkspaceComposition = new DiagnosticsLogsWorkspaceComposition(
            logsView,
            new DiagnosticsLogsWorkspaceHost(
                () => _shellBridge.IsDiagnosticsLogsActive,
                _host.LoadStructuredLogsAsync,
                _host.GetStructuredLogFilePath,
                _shellBridge.OpenStructuredLogLocation));
        _overviewWorkspaceComposition = new DiagnosticsOverviewWorkspaceComposition(
            overviewView,
            new DiagnosticsOverviewWorkspaceHost(
                _host.GetStructuredLogFilePath,
                statusText => _logsWorkspaceComposition.ReportStatusText(statusText)),
            new DiagnosticsOverviewWorkspaceShellBridge(
                () => _shellBridge.IsDiagnosticsOverviewActive,
                _shellBridge.NavigateToRoute,
                _shellBridge.OpenStructuredLogLocation));
        WireSharedHandlers();
        RefreshOverviewSummary();
    }

    public void ApplyShellState()
    {
        _localNavigationHost.Visibility = _shellBridge.IsDiagnosticsCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        _logsWorkspaceComposition.ApplyShellState();
        _overviewWorkspaceComposition.ApplyShellState();

        SyncDiagnosticsSubviewSelection();
    }

    private void WireSharedHandlers()
    {
        _subviewTabView.SelectionChanged += DiagnosticsSubviewTabView_SelectionChanged;
        _logsWorkspaceComposition.WorkspaceStateChanged += LogsWorkspaceComposition_WorkspaceStateChanged;
    }

    private void LogsWorkspaceComposition_WorkspaceStateChanged(object? sender, EventArgs e)
    {
        RefreshOverviewSummary();
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

    private void RefreshOverviewSummary()
    {
        _overviewWorkspaceComposition.RefreshSummary(_logsWorkspaceComposition.IsLoading, _logsWorkspaceComposition.StructuredLogEntryCount);
    }
}
