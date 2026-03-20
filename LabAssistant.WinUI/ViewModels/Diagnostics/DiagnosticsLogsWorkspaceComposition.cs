using System.Collections.ObjectModel;
using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Views.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Diagnostics;

internal interface IDiagnosticsLogsWorkspaceHost
{
    bool IsLogsActive { get; }

    Task<StructuredLogViewerLoadResult> LoadStructuredLogsAsync(StructuredLogViewerFilter filter);

    string GetStructuredLogFilePath();

    string? OpenStructuredLogLocation(string filePath);
}

internal sealed class DiagnosticsLogsWorkspaceHost : IDiagnosticsLogsWorkspaceHost
{
    private readonly Func<bool> _isLogsActive;
    private readonly Func<StructuredLogViewerFilter, Task<StructuredLogViewerLoadResult>> _loadStructuredLogsAsync;
    private readonly Func<string> _getStructuredLogFilePath;
    private readonly Func<string, string?> _openStructuredLogLocation;

    public DiagnosticsLogsWorkspaceHost(
        Func<bool> isLogsActive,
        Func<StructuredLogViewerFilter, Task<StructuredLogViewerLoadResult>> loadStructuredLogsAsync,
        Func<string> getStructuredLogFilePath,
        Func<string, string?> openStructuredLogLocation)
    {
        _isLogsActive = isLogsActive;
        _loadStructuredLogsAsync = loadStructuredLogsAsync;
        _getStructuredLogFilePath = getStructuredLogFilePath;
        _openStructuredLogLocation = openStructuredLogLocation;
    }

    public bool IsLogsActive => _isLogsActive();

    public Task<StructuredLogViewerLoadResult> LoadStructuredLogsAsync(StructuredLogViewerFilter filter)
        => _loadStructuredLogsAsync(filter);

    public string GetStructuredLogFilePath() => _getStructuredLogFilePath();

    public string? OpenStructuredLogLocation(string filePath) => _openStructuredLogLocation(filePath);
}

internal sealed class DiagnosticsLogsWorkspaceComposition
    : IDiagnosticsLogsWorkspaceControllerHost
{
    private readonly DiagnosticsLogsView _view;
    private readonly IDiagnosticsLogsWorkspaceHost _host;
    private readonly DiagnosticsLogsWorkspaceViewModel _workspace = new();
    private readonly DiagnosticsLogsWorkspaceController _controller;
    private readonly ObservableCollection<StructuredLogViewerEntry> _structuredLogEntries = [];

    public event EventHandler? WorkspaceStateChanged;

    public DiagnosticsLogsWorkspaceComposition(
        DiagnosticsLogsView view,
        IDiagnosticsLogsWorkspaceHost host)
    {
        _view = view;
        _host = host;
        _controller = new DiagnosticsLogsWorkspaceController(_workspace, this);
        _view.StructuredLogsListView.ItemsSource = _structuredLogEntries;
        WireHandlers();
        ApplyWorkspaceState(isLoading: false);
    }

    public bool IsLoading => _controller.IsLoading;

    public int StructuredLogEntryCount => _structuredLogEntries.Count;

    public void ApplyShellState()
    {
        _view.Visibility = _host.IsLogsActive ? Visibility.Visible : Visibility.Collapsed;
        if (_host.IsLogsActive)
        {
            _ = _controller.EnsureLogsLoadedAsync(forceReload: false);
        }
    }

    public void HandleFilterStateChanged()
    {
        _controller.HandleFilterStateChanged(_view.CaptureFilterState());
    }

    public Task ApplyFiltersAsync()
    {
        return _controller.ApplyFiltersAsync(_view.CaptureFilterState());
    }

    public Task ClearFiltersAsync()
    {
        return _controller.ClearFiltersAsync();
    }

    public void HandleSelectionChanged()
    {
        _controller.HandleSelectionChanged(_view.CaptureSelectedLogEntry());
    }

    public Task ReloadAsync()
    {
        return _controller.EnsureLogsLoadedAsync(forceReload: true);
    }

    public void OpenRawLogLocation()
    {
        _controller.OpenRawLogLocation();
    }

    public void ReportStatusText(string statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText))
        {
            return;
        }

        _workspace.SetStatusText(statusText);
        ApplyWorkspaceState(_controller.IsLoading);
        WorkspaceStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void WireHandlers()
    {
        _view.FilterStateChanged += FilterStateChanged;
        _view.ApplyFiltersRequested += ApplyFiltersRequested;
        _view.ClearFiltersRequested += ClearFiltersRequested;
        _view.ReloadLogsButton.Click += ReloadLogsButton_Click;
        _view.OpenRawJsonlButton.Click += OpenRawJsonlButton_Click;
        _view.SelectedLogChanged += SelectedLogChanged;
    }

    private void FilterStateChanged(object? sender, EventArgs e)
    {
        HandleFilterStateChanged();
    }

    private async void ApplyFiltersRequested(object sender, RoutedEventArgs e)
    {
        await ApplyFiltersAsync();
    }

    private async void ClearFiltersRequested(object sender, RoutedEventArgs e)
    {
        await ClearFiltersAsync();
    }

    private async void ReloadLogsButton_Click(object sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    private void OpenRawJsonlButton_Click(object sender, RoutedEventArgs e)
    {
        OpenRawLogLocation();
    }

    private void SelectedLogChanged(object? sender, EventArgs e)
    {
        HandleSelectionChanged();
    }

    bool IDiagnosticsLogsWorkspaceControllerHost.IsLogsActive => _host.IsLogsActive;

    int IDiagnosticsLogsWorkspaceControllerHost.StructuredLogEntryCount => _structuredLogEntries.Count;

    Task<StructuredLogViewerLoadResult> IDiagnosticsLogsWorkspaceControllerHost.LoadStructuredLogsAsync(StructuredLogViewerFilter filter)
        => _host.LoadStructuredLogsAsync(filter);

    string IDiagnosticsLogsWorkspaceControllerHost.GetStructuredLogFilePath()
        => _host.GetStructuredLogFilePath();

    string? IDiagnosticsLogsWorkspaceControllerHost.OpenStructuredLogLocation(string filePath)
        => _host.OpenStructuredLogLocation(filePath);

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
        ApplyWorkspaceState(isLoading);
        WorkspaceStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyWorkspaceState(bool isLoading)
    {
        _view.ApplyFilterState(_workspace.BuildViewState());
        _view.ApplySelectionState(_workspace.BuildSelectionViewState());
        _view.ApplyLogFiltersButton.IsEnabled = !isLoading;
        _view.ClearLogFiltersButton.IsEnabled = !isLoading;
        _view.ReloadLogsButton.IsEnabled = !isLoading;
        _view.OpenRawJsonlButton.IsEnabled = !isLoading;
        _view.LogsStatusTextBlock.Text = _workspace.StatusText;
    }
}
