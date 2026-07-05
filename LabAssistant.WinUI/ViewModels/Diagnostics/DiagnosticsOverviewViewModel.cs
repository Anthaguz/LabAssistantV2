using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Infrastructure;

namespace LabAssistant.WinUI.ViewModels.Diagnostics;

/// <summary>
/// View model for the Diagnostics Overview subview. Presents summary cards for structured logs and
/// support export, and delegates capability-level actions (open Logs, report support status)
/// through the <see cref="IDiagnosticsShell"/> seam its hosting page provides.
/// </summary>
public sealed partial class DiagnosticsOverviewViewModel : Infrastructure.ViewModelBase
{
    private const string DefaultLogsSummaryText = "Open Logs to inspect structured events and current support context.";
    private const string DefaultSupportSummaryText = "Open the current structured log location for support export or manual diagnostics collection.";

    private readonly IStructuredLogViewerService _logViewer;
    private readonly ILogLocationLauncher _logLocationLauncher;

    private IDiagnosticsShell? _shell;

    [ObservableProperty]
    private string _logsSummaryText = DefaultLogsSummaryText;

    [ObservableProperty]
    private string _supportSummaryText = DefaultSupportSummaryText;

    public DiagnosticsOverviewViewModel(IStructuredLogViewerService logViewer, ILogLocationLauncher logLocationLauncher)
    {
        _logViewer = logViewer;
        _logLocationLauncher = logLocationLauncher;
    }

    /// <summary>Attaches the capability page seam. Called by the page in its navigation lifecycle.</summary>
    internal void AttachShell(IDiagnosticsShell shell) => _shell = shell;

    /// <summary>Detaches the capability page seam so the page can be torn down cleanly.</summary>
    internal void DetachShell() => _shell = null;

    /// <summary>
    /// Recomputes the logs summary card from current logs load state. Driven by the page when the
    /// Logs subview's entry set or busy state changes - this is the one genuine cross-subview link.
    /// </summary>
    public void RefreshLogsSummary(bool isStructuredLogsLoading, int structuredLogEntryCount)
    {
        LogsSummaryText = isStructuredLogsLoading
            ? "Structured logs are loading."
            : structuredLogEntryCount > 0
                ? $"{structuredLogEntryCount} structured log entries are currently loaded."
                : DefaultLogsSummaryText;
    }

    [RelayCommand]
    private void OpenLogs() => InvokeBridgeCallback(_shell is { } shell ? shell.ShowLogs : null);

    [RelayCommand]
    private void OpenSupportExport()
    {
        var statusText = _logLocationLauncher.TryOpen(_logViewer.GetStructuredLogFilePath());
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            _shell?.ReportSupportStatus(statusText);
        }
    }
}
