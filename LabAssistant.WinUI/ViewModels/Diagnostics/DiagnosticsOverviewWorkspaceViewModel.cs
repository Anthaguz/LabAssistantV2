namespace LabAssistant.WinUI.ViewModels.Diagnostics;

internal sealed class DiagnosticsOverviewWorkspaceViewModel
{
    private const string DefaultLogsSummaryText = "Open Logs to inspect structured events and current support context.";
    private const string DefaultSupportSummaryText = "Open the current structured log location for support export or manual diagnostics collection.";

    public string LogsSummaryText { get; private set; } = DefaultLogsSummaryText;

    public string SupportSummaryText { get; } = DefaultSupportSummaryText;

    public void RefreshSummary(bool isStructuredLogsLoading, int structuredLogEntryCount)
    {
        LogsSummaryText = isStructuredLogsLoading
            ? "Structured logs are loading."
            : structuredLogEntryCount > 0
                ? $"{structuredLogEntryCount} structured log entries are currently loaded."
                : DefaultLogsSummaryText;
    }
}
