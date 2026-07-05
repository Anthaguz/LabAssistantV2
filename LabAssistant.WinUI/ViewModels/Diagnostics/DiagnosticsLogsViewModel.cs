using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Infrastructure;

namespace LabAssistant.WinUI.ViewModels.Diagnostics;

/// <summary>
/// View model for the Diagnostics Logs subview. Owns the structured-log filter inputs, the loaded
/// entry set, selection detail, and the load/reload/clear/open commands. Folds in what were
/// previously separate workspace view-model, controller, and composition layers so the view binds
/// directly through <c>x:Bind</c> instead of imperative view-state marshalling.
/// </summary>
public sealed partial class DiagnosticsLogsViewModel : Infrastructure.ViewModelBase
{
    private readonly IStructuredLogViewerService _logViewer;
    private readonly ILogLocationLauncher _logLocationLauncher;

    [ObservableProperty]
    private string _operationIdQuery = string.Empty;

    [ObservableProperty]
    private string _levelQuery = string.Empty;

    [ObservableProperty]
    private string _eventQuery = string.Empty;

    [ObservableProperty]
    private string _textSearchQuery = string.Empty;

    [ObservableProperty]
    private bool _useStartDateFilter;

    [ObservableProperty]
    private DateTimeOffset _startDate = DateTimeOffset.Now;

    [ObservableProperty]
    private bool _useEndDateFilter;

    [ObservableProperty]
    private DateTimeOffset _endDate = DateTimeOffset.Now;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyCanExecuteChangedFor(nameof(ApplyFiltersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearFiltersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenRawJsonlCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Logs not loaded yet.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedEnvelopeText))]
    [NotifyPropertyChangedFor(nameof(SelectedContextText))]
    private StructuredLogViewerEntry? _selectedEntry;

    public DiagnosticsLogsViewModel(IStructuredLogViewerService logViewer, ILogLocationLauncher logLocationLauncher)
    {
        _logViewer = logViewer;
        _logLocationLauncher = logLocationLauncher;
    }

    /// <summary>Structured log entries currently loaded, newest first.</summary>
    public ObservableCollection<StructuredLogViewerEntry> Entries { get; } = [];

    /// <summary>True when no load is in flight; gates the interaction commands.</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>Single-line envelope summary for the selected entry.</summary>
    public string SelectedEnvelopeText => SelectedEntry is { } entry
        ? $"ts={entry.TimestampText} | level={entry.Level} | event={entry.Event} | operationId={entry.OperationId} | result={entry.Result}"
        : "Select a log entry.";

    /// <summary>Pretty-printed context JSON for the selected entry.</summary>
    public string SelectedContextText => SelectedEntry is { } entry
        ? FormatJsonForDetails(entry.ContextJson)
        : string.Empty;

    /// <summary>Loads structured logs on first entry into the subview.</summary>
    public override async Task InitializeAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(parameter, cancellationToken);
        await LoadLogsAsync(forceReload: false);
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task ApplyFiltersAsync() => LoadLogsAsync(forceReload: true);

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task ClearFiltersAsync()
    {
        OperationIdQuery = string.Empty;
        LevelQuery = string.Empty;
        EventQuery = string.Empty;
        TextSearchQuery = string.Empty;
        UseStartDateFilter = false;
        UseEndDateFilter = false;
        StartDate = DateTimeOffset.Now;
        EndDate = DateTimeOffset.Now;
        return LoadLogsAsync(forceReload: true);
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task ReloadAsync() => LoadLogsAsync(forceReload: true);

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private void OpenRawJsonl()
    {
        var statusText = _logLocationLauncher.TryOpen(_logViewer.GetStructuredLogFilePath());
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            StatusText = statusText;
        }
    }

    private async Task LoadLogsAsync(bool forceReload)
    {
        if (IsBusy)
        {
            return;
        }

        // Preserve the prior cache behavior: a non-forced load is a no-op once entries exist.
        if (!forceReload && Entries.Count > 0)
        {
            return;
        }

        IsBusy = true;
        StatusText = "Loading structured logs...";
        // Capture the lifecycle token locally: ViewModelBase nulls its CTS before cancelling on
        // cleanup, so re-reading the LifecycleToken property in the catch filter would observe
        // CancellationToken.None and misclassify navigation-away cancellation as a load failure.
        var lifecycleToken = LifecycleToken;
        try
        {
            var filter = BuildFilter();
            var result = await _logViewer.LoadAsync(filter, lifecycleToken);

            Entries.Clear();
            foreach (var entry in result.Entries)
            {
                Entries.Add(entry);
            }

            SelectedEntry = null;

            var filePath = _logViewer.GetStructuredLogFilePath();
            var parseErrorSuffix = result.ParseErrorCount > 0
                ? $" Skipped malformed lines: {result.ParseErrorCount}."
                : string.Empty;
            StatusText = File.Exists(filePath)
                ? $"Loaded {result.Entries.Count} events from {result.TotalLineCount} lines.{parseErrorSuffix}"
                : $"Structured log file not found yet: {filePath}";
        }
        catch (OperationCanceledException) when (lifecycleToken.IsCancellationRequested)
        {
            // Navigation away cancelled the load; leave state as-is.
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load structured logs. {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private StructuredLogViewerFilter BuildFilter()
    {
        return new StructuredLogViewerFilter
        {
            OperationId = NormalizeFilterText(OperationIdQuery),
            Level = NormalizeFilterText(LevelQuery),
            Event = NormalizeFilterText(EventQuery),
            TextSearch = NormalizeFilterText(TextSearchQuery),
            StartUtc = UseStartDateFilter ? ToDateBoundaryUtc(StartDate, isEndBoundary: false) : null,
            EndUtc = UseEndDateFilter ? ToDateBoundaryUtc(EndDate, isEndBoundary: true) : null
        };
    }

    private static string NormalizeFilterText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static DateTimeOffset ToDateBoundaryUtc(DateTimeOffset date, bool isEndBoundary)
    {
        var selectedDate = date.Date;
        var localBoundary = isEndBoundary
            ? selectedDate.AddDays(1).AddTicks(-1)
            : selectedDate;
        return localBoundary.ToUniversalTime();
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
