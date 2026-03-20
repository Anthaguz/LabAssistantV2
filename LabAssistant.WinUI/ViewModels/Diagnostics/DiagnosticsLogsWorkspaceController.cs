using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Views.Diagnostics;

namespace LabAssistant.WinUI.ViewModels.Diagnostics;

internal interface IDiagnosticsLogsWorkspaceControllerHost
{
    bool IsLogsActive { get; }

    int StructuredLogEntryCount { get; }

    Task<StructuredLogViewerLoadResult> LoadStructuredLogsAsync(StructuredLogViewerFilter filter);

    string GetStructuredLogFilePath();

    string? OpenStructuredLogLocation(string filePath);

    void ReplaceStructuredLogEntries(IReadOnlyList<StructuredLogViewerEntry> entries);

    void ApplyWorkspaceState(bool isLoading);
}

internal sealed class DiagnosticsLogsWorkspaceController
{
    private readonly DiagnosticsLogsWorkspaceViewModel _workspace;
    private readonly IDiagnosticsLogsWorkspaceControllerHost _host;
    private bool _isLoading;

    public DiagnosticsLogsWorkspaceController(
        DiagnosticsLogsWorkspaceViewModel workspace,
        IDiagnosticsLogsWorkspaceControllerHost host)
    {
        _workspace = workspace;
        _host = host;
    }

    public bool IsLoading => _isLoading;

    public async Task EnsureLogsLoadedAsync(bool forceReload)
    {
        if (!_host.IsLogsActive || _isLoading)
        {
            return;
        }

        if (!forceReload && _host.StructuredLogEntryCount > 0)
        {
            _host.ApplyWorkspaceState(_isLoading);
            return;
        }

        _isLoading = true;
        _workspace.SetStatusText("Loading structured logs...");
        _host.ApplyWorkspaceState(_isLoading);

        try
        {
            var filter = _workspace.BuildStructuredLogFilter();
            var result = await _host.LoadStructuredLogsAsync(filter);

            _host.ReplaceStructuredLogEntries(result.Entries);
            _workspace.ClearSelection();

            var filePath = _host.GetStructuredLogFilePath();
            var parseErrorSuffix = result.ParseErrorCount > 0
                ? $" Skipped malformed lines: {result.ParseErrorCount}."
                : string.Empty;
            _workspace.SetStatusText(File.Exists(filePath)
                ? $"Loaded {result.Entries.Count} events from {result.TotalLineCount} lines.{parseErrorSuffix}"
                : $"Structured log file not found yet: {filePath}");
        }
        catch (Exception ex)
        {
            _workspace.SetStatusText($"Failed to load structured logs. {ex.Message}");
        }
        finally
        {
            _isLoading = false;
            _host.ApplyWorkspaceState(_isLoading);
        }
    }

    public void HandleFilterStateChanged(DiagnosticsLogsFilterViewState state)
    {
        _workspace.ApplyFilterState(state);
        _host.ApplyWorkspaceState(_isLoading);
    }

    public Task ApplyFiltersAsync(DiagnosticsLogsFilterViewState state)
    {
        _workspace.ApplyFilterState(state);
        return EnsureLogsLoadedAsync(forceReload: true);
    }

    public Task ClearFiltersAsync()
    {
        _workspace.ClearFilters();
        return EnsureLogsLoadedAsync(forceReload: true);
    }

    public void HandleSelectionChanged(StructuredLogViewerEntry? selectedEntry)
    {
        _workspace.SetSelectedEntry(selectedEntry);
        _host.ApplyWorkspaceState(_isLoading);
    }

    public void OpenRawLogLocation()
    {
        var statusText = _host.OpenStructuredLogLocation(_host.GetStructuredLogFilePath());
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            _workspace.SetStatusText(statusText);
            _host.ApplyWorkspaceState(_isLoading);
        }
    }
}
