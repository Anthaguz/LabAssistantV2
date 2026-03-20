using LabAssistant.WinUI.Views.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Diagnostics;

internal interface IDiagnosticsOverviewWorkspaceHost
{
    string GetStructuredLogFilePath();

    void ReportOpenSupportStatus(string statusText);
}

internal interface IDiagnosticsOverviewWorkspaceShellBridge
{
    bool IsDiagnosticsOverviewActive { get; }

    void NavigateToRoute(string routeKey);

    string? OpenStructuredLogLocation(string filePath);
}

internal sealed class DiagnosticsOverviewWorkspaceHost : IDiagnosticsOverviewWorkspaceHost
{
    private readonly Func<string> _getStructuredLogFilePath;
    private readonly Action<string> _reportOpenSupportStatus;

    public DiagnosticsOverviewWorkspaceHost(
        Func<string> getStructuredLogFilePath,
        Action<string> reportOpenSupportStatus)
    {
        _getStructuredLogFilePath = getStructuredLogFilePath;
        _reportOpenSupportStatus = reportOpenSupportStatus;
    }

    public string GetStructuredLogFilePath() => _getStructuredLogFilePath();

    public void ReportOpenSupportStatus(string statusText) => _reportOpenSupportStatus(statusText);
}

internal sealed class DiagnosticsOverviewWorkspaceShellBridge : IDiagnosticsOverviewWorkspaceShellBridge
{
    private readonly Func<bool> _isDiagnosticsOverviewActive;
    private readonly Action<string> _navigateToRoute;
    private readonly Func<string, string?> _openStructuredLogLocation;

    public DiagnosticsOverviewWorkspaceShellBridge(
        Func<bool> isDiagnosticsOverviewActive,
        Action<string> navigateToRoute,
        Func<string, string?> openStructuredLogLocation)
    {
        _isDiagnosticsOverviewActive = isDiagnosticsOverviewActive;
        _navigateToRoute = navigateToRoute;
        _openStructuredLogLocation = openStructuredLogLocation;
    }

    public bool IsDiagnosticsOverviewActive => _isDiagnosticsOverviewActive();

    public void NavigateToRoute(string routeKey) => _navigateToRoute(routeKey);

    public string? OpenStructuredLogLocation(string filePath) => _openStructuredLogLocation(filePath);
}

internal sealed class DiagnosticsOverviewWorkspaceComposition
{
    private readonly DiagnosticsOverviewView _view;
    private readonly DiagnosticsOverviewWorkspaceViewModel _workspace = new();
    private readonly IDiagnosticsOverviewWorkspaceHost _host;
    private readonly IDiagnosticsOverviewWorkspaceShellBridge _shellBridge;

    public DiagnosticsOverviewWorkspaceComposition(
        DiagnosticsOverviewView view,
        IDiagnosticsOverviewWorkspaceHost host,
        IDiagnosticsOverviewWorkspaceShellBridge shellBridge)
    {
        _view = view;
        _host = host;
        _shellBridge = shellBridge;
        WireHandlers();
        ApplyWorkspaceState();
    }

    public void RefreshSummary(bool isStructuredLogsLoading, int structuredLogEntryCount)
    {
        _workspace.RefreshSummary(isStructuredLogsLoading, structuredLogEntryCount);

        if (_shellBridge.IsDiagnosticsOverviewActive)
        {
            ApplyWorkspaceState();
        }
    }

    public void ApplyShellState()
    {
        _view.Visibility = _shellBridge.IsDiagnosticsOverviewActive ? Visibility.Visible : Visibility.Collapsed;

        if (_shellBridge.IsDiagnosticsOverviewActive)
        {
            ApplyWorkspaceState();
        }
    }

    private void WireHandlers()
    {
        _view.OpenLogsRequested += OpenLogsRequested;
        _view.OpenSupportExportRequested += OpenSupportExportRequested;
    }

    private void ApplyWorkspaceState()
    {
        _view.ApplyWorkspaceState(new DiagnosticsOverviewViewState(
            _workspace.LogsSummaryText,
            _workspace.SupportSummaryText));
    }

    private void OpenLogsRequested(object sender, RoutedEventArgs e)
    {
        _shellBridge.NavigateToRoute(ShellRouteKeys.DiagnosticsLogs);
    }

    private void OpenSupportExportRequested(object sender, RoutedEventArgs e)
    {
        var statusText = _shellBridge.OpenStructuredLogLocation(_host.GetStructuredLogFilePath());
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            _host.ReportOpenSupportStatus(statusText);
        }
    }
}
