using System.Collections.ObjectModel;
using System.Text.Json;
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
{
    private readonly FrameworkElement _localNavigationHost;
    private readonly DiagnosticsOverviewWorkspaceComposition _overviewWorkspaceComposition;
    private readonly DiagnosticsLogsView _logsView;
    private readonly DiagnosticsLogsWorkspaceViewModel _logsWorkspace = new();
    private readonly TabView _subviewTabView;
    private readonly TabViewItem _overviewTabViewItem;
    private readonly TabViewItem _logsTabViewItem;
    private readonly IDiagnosticsWorkspaceHost _host;
    private readonly IDiagnosticsWorkspaceShellBridge _shellBridge;
    private readonly ObservableCollection<StructuredLogViewerEntry> _structuredLogEntries = [];
    private StructuredLogViewerEntry? _selectedStructuredLogEntry;
    private bool _isStructuredLogsLoading;
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
        _overviewWorkspaceComposition = new DiagnosticsOverviewWorkspaceComposition(
            overviewView,
            new DiagnosticsOverviewWorkspaceHost(
                _host.GetStructuredLogFilePath,
                statusText => _logsView.LogsStatusTextBlock.Text = statusText),
            new DiagnosticsOverviewWorkspaceShellBridge(
                () => _shellBridge.IsDiagnosticsOverviewActive,
                _shellBridge.NavigateToRoute,
                _shellBridge.OpenStructuredLogLocation));
        _logsView.ApplyFilterState(_logsWorkspace.BuildViewState());
        _logsView.StructuredLogsListView.ItemsSource = _structuredLogEntries;
        WireSharedHandlers();
        UpdateStructuredLogSelectionDetails();
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
            _ = EnsureStructuredLogsLoadedAsync(forceReload: false);
        }
    }

    private void WireSharedHandlers()
    {
        _subviewTabView.SelectionChanged += DiagnosticsSubviewTabView_SelectionChanged;
        _logsView.FilterStateChanged += LogsView_FilterStateChanged;
        _logsView.ApplyFiltersRequested += ApplyLogFiltersButton_Click;
        _logsView.ClearFiltersRequested += ClearLogFiltersButton_Click;
        _logsView.ReloadLogsButton.Click += ReloadLogsButton_Click;
        _logsView.OpenRawJsonlButton.Click += (_, _) => OpenStructuredLogLocation();
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
        await EnsureStructuredLogsLoadedAsync(forceReload: true);
    }

    private void RefreshOverviewSummary()
    {
        _overviewWorkspaceComposition.RefreshSummary(_isStructuredLogsLoading, _structuredLogEntries.Count);
    }

    private void LogsView_FilterStateChanged(object? sender, EventArgs e)
    {
        _logsWorkspace.ApplyFilterState(_logsView.CaptureFilterState());
    }

    private async void ApplyLogFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        _logsWorkspace.ApplyFilterState(_logsView.CaptureFilterState());
        await EnsureStructuredLogsLoadedAsync(forceReload: true);
    }

    private async void ClearLogFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        _logsWorkspace.ClearFilters();
        _logsView.ApplyFilterState(_logsWorkspace.BuildViewState());
        await EnsureStructuredLogsLoadedAsync(forceReload: true);
    }

    private void StructuredLogsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedStructuredLogEntry = _logsView.StructuredLogsListView.SelectedItem as StructuredLogViewerEntry;
        UpdateStructuredLogSelectionDetails();
    }

    private async Task EnsureStructuredLogsLoadedAsync(bool forceReload)
    {
        if (!_shellBridge.IsDiagnosticsLogsActive || _isStructuredLogsLoading)
        {
            return;
        }

        if (!forceReload && _structuredLogEntries.Count > 0)
        {
            return;
        }

        _isStructuredLogsLoading = true;
        _logsView.ApplyLogFiltersButton.IsEnabled = false;
        _logsView.ClearLogFiltersButton.IsEnabled = false;
        _logsView.ReloadLogsButton.IsEnabled = false;
        _logsView.OpenRawJsonlButton.IsEnabled = false;
        _logsView.LogsStatusTextBlock.Text = "Loading structured logs...";
        RefreshOverviewSummary();

        try
        {
            var filter = BuildStructuredLogFilter();
            var result = await _host.LoadStructuredLogsAsync(filter);

            _structuredLogEntries.Clear();
            foreach (var entry in result.Entries)
            {
                _structuredLogEntries.Add(entry);
            }

            _logsView.StructuredLogsListView.SelectedItem = null;
            _selectedStructuredLogEntry = null;
            UpdateStructuredLogSelectionDetails();

            var filePath = _host.GetStructuredLogFilePath();
            var parseErrorSuffix = result.ParseErrorCount > 0
                ? $" Skipped malformed lines: {result.ParseErrorCount}."
                : string.Empty;
            _logsView.LogsStatusTextBlock.Text = File.Exists(filePath)
                ? $"Loaded {_structuredLogEntries.Count} events from {result.TotalLineCount} lines.{parseErrorSuffix}"
                : $"Structured log file not found yet: {filePath}";
        }
        catch (Exception ex)
        {
            _logsView.LogsStatusTextBlock.Text = $"Failed to load structured logs. {ex.Message}";
        }
        finally
        {
            _isStructuredLogsLoading = false;
            _logsView.ApplyLogFiltersButton.IsEnabled = true;
            _logsView.ClearLogFiltersButton.IsEnabled = true;
            _logsView.ReloadLogsButton.IsEnabled = true;
            _logsView.OpenRawJsonlButton.IsEnabled = true;
            RefreshOverviewSummary();
        }
    }

    private StructuredLogViewerFilter BuildStructuredLogFilter()
    {
        return _logsWorkspace.BuildStructuredLogFilter();
    }

    private void UpdateStructuredLogSelectionDetails()
    {
        if (_selectedStructuredLogEntry is null)
        {
            _logsView.SelectedLogEnvelopeTextBlock.Text = "Select a log entry.";
            _logsView.SelectedLogContextTextBox.Text = string.Empty;
            return;
        }

        _logsView.SelectedLogEnvelopeTextBlock.Text =
            $"ts={_selectedStructuredLogEntry.TimestampText} | level={_selectedStructuredLogEntry.Level} | event={_selectedStructuredLogEntry.Event} | operationId={_selectedStructuredLogEntry.OperationId} | result={_selectedStructuredLogEntry.Result}";
        _logsView.SelectedLogContextTextBox.Text = FormatJsonForDetails(_selectedStructuredLogEntry.ContextJson);
    }

    private void OpenStructuredLogLocation()
    {
        var statusText = _shellBridge.OpenStructuredLogLocation(_host.GetStructuredLogFilePath());
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            _logsView.LogsStatusTextBlock.Text = statusText;
        }
    }

    private static string FormatJsonForDetails(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch
        {
            return json;
        }
    }
}
