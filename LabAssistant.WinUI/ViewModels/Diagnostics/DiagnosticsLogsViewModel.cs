using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Infrastructure;

namespace LabAssistant.WinUI.ViewModels.Diagnostics;

/// <summary>
/// View model for the Diagnostics Logs subview. Owns the structured-log filter inputs (free-text plus a
/// facility/severity bitmask filter driven by the status-code registry), the loaded entry set, the
/// resolved detail projection for the Selected Context panel, and the load/reload/clear/open commands.
/// </summary>
public sealed partial class DiagnosticsLogsViewModel : ViewModelBase
{
    private readonly IStructuredLogViewerService _logViewer;
    private readonly ILogLocationLauncher _logLocationLauncher;

    [ObservableProperty]
    private string _operationIdQuery = string.Empty;

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
    private LogLevelFilterOption _selectedLevelOption;

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
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(SelectedSummary))]
    [NotifyPropertyChangedFor(nameof(SelectedTitle))]
    [NotifyPropertyChangedFor(nameof(SelectedMessage))]
    [NotifyPropertyChangedFor(nameof(SelectedRemediation))]
    [NotifyPropertyChangedFor(nameof(HasRemediation))]
    [NotifyPropertyChangedFor(nameof(SelectedCode))]
    [NotifyPropertyChangedFor(nameof(HasCode))]
    [NotifyPropertyChangedFor(nameof(SelectedSeverity))]
    [NotifyPropertyChangedFor(nameof(SelectedFacilityText))]
    [NotifyPropertyChangedFor(nameof(SelectedOperationText))]
    [NotifyPropertyChangedFor(nameof(SelectedPhaseText))]
    [NotifyPropertyChangedFor(nameof(SelectedThreadText))]
    [NotifyPropertyChangedFor(nameof(SelectedCallsite))]
    [NotifyPropertyChangedFor(nameof(SelectedRawJson))]
    [NotifyCanExecuteChangedFor(nameof(CopyCodeCommand))]
    private StructuredLogViewerEntry? _selectedEntry;

    public DiagnosticsLogsViewModel(IStructuredLogViewerService logViewer, ILogLocationLauncher logLocationLauncher)
    {
        _logViewer = logViewer;
        _logLocationLauncher = logLocationLauncher;

        LevelOptions =
        [
            new LogLevelFilterOption("All levels", null),
            new LogLevelFilterOption("Debug and up", StatusLevelRank.Debug),
            new LogLevelFilterOption("Info and up", StatusLevelRank.Info),
            new LogLevelFilterOption("Warnings and up", StatusLevelRank.Warn),
            new LogLevelFilterOption("Errors only", StatusLevelRank.Error)
        ];
        _selectedLevelOption = LevelOptions[0];

        foreach (var facility in StatusCodeCatalog.Facilities.OrderBy(item => item.Value))
        {
            var option = new FacilityFilterOption(facility.Value, facility.Name, facility.Title);
            option.PropertyChanged += OnFacilityOptionChanged;
            Facilities.Add(option);
        }
    }

    /// <summary>Structured log entries currently loaded, newest first.</summary>
    public ObservableCollection<StructuredLogViewerEntry> Entries { get; } = [];

    /// <summary>The selectable minimum-level options.</summary>
    public IReadOnlyList<LogLevelFilterOption> LevelOptions { get; }

    /// <summary>The facility toggles, one per registered facility.</summary>
    public ObservableCollection<FacilityFilterOption> Facilities { get; } = [];

    /// <summary>Rows parsed from the selected entry's context, rendered as selectable key/value pairs.</summary>
    public ObservableCollection<LogContextRow> SelectedContextRows { get; } = [];

    /// <summary>True when no load is in flight; gates the interaction commands.</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>A short summary of how many facilities are selected, for the filter flyout header.</summary>
    public string FacilityFilterSummary
    {
        get
        {
            var selected = Facilities.Count(option => option.IsSelected);
            return selected == 0 ? "All facilities" : $"{selected} selected";
        }
    }

    /// <summary>True when an entry is selected.</summary>
    public bool HasSelection => SelectedEntry is not null;

    /// <summary>A one-line summary of the selected entry for the panel header.</summary>
    public string SelectedSummary => SelectedEntry is { } entry
        ? $"{entry.TimestampText} - {OperationIdDisplay.ToDisplay(entry.OperationId)}"
        : "Select a log entry.";

    /// <summary>The friendly title for the selected entry, resolved from the code registry when available.</summary>
    public string SelectedTitle => SelectedEntry is { } entry
        ? entry.Title ?? entry.Event
        : string.Empty;

    /// <summary>The plain-language sentence for the selected entry.</summary>
    public string SelectedMessage
    {
        get
        {
            if (SelectedEntry is not { } entry)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(entry.Message))
            {
                return entry.Message!;
            }

            // No registry message (legacy or unregistered code): synthesize a readable line from the fields.
            var outcome = string.IsNullOrWhiteSpace(entry.Result) ? string.Empty : $" ({entry.Result})";
            return $"{entry.Event}{outcome}";
        }
    }

    /// <summary>Remediation guidance for the selected entry, when the registry defines it.</summary>
    public string SelectedRemediation => SelectedEntry?.Remediation ?? string.Empty;

    /// <summary>True when the selected entry has remediation guidance to show.</summary>
    public bool HasRemediation => !string.IsNullOrWhiteSpace(SelectedEntry?.Remediation);

    /// <summary>The selected entry's canonical code, for the copyable chip.</summary>
    public string SelectedCode => SelectedEntry?.Code ?? string.Empty;

    /// <summary>True when the selected entry carries a canonical code.</summary>
    public bool HasCode => !string.IsNullOrWhiteSpace(SelectedEntry?.Code);

    /// <summary>The selected entry's severity name, falling back to the level string for legacy events.</summary>
    public string SelectedSeverity => SelectedEntry is { } entry
        ? string.IsNullOrWhiteSpace(entry.Severity) ? entry.Level : entry.Severity
        : string.Empty;

    public string SelectedFacilityText => SelectedEntry is { } entry && !string.IsNullOrWhiteSpace(entry.Facility)
        ? $"{entry.Facility} (0x{entry.FacilityByte ?? 0:X2})"
        : SelectedEntry?.Facility ?? string.Empty;

    public string SelectedOperationText => SelectedEntry?.Operation ?? string.Empty;

    public string SelectedPhaseText => SelectedEntry?.Phase ?? string.Empty;

    public string SelectedThreadText => SelectedEntry?.Thread is { } thread ? thread.ToString() : string.Empty;

    public string SelectedCallsite => SelectedEntry?.Callsite ?? string.Empty;

    /// <summary>The pretty-printed raw context JSON, kept available in a collapsed expander.</summary>
    public string SelectedRawJson => SelectedEntry is { } entry
        ? FormatJsonForDetails(entry.ContextJson)
        : string.Empty;

    /// <summary>Loads structured logs on first entry into the subview.</summary>
    public override async Task InitializeAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(parameter, cancellationToken);
        await LoadLogsAsync(forceReload: false);
    }

    partial void OnSelectedEntryChanged(StructuredLogViewerEntry? value)
    {
        SelectedContextRows.Clear();
        if (value is null)
        {
            return;
        }

        foreach (var row in ParseContextRows(value.ContextJson))
        {
            SelectedContextRows.Add(row);
        }
    }

    private void OnFacilityOptionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FacilityFilterOption.IsSelected))
        {
            OnPropertyChanged(nameof(FacilityFilterSummary));
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task ApplyFiltersAsync() => LoadLogsAsync(forceReload: true);

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task ClearFiltersAsync()
    {
        OperationIdQuery = string.Empty;
        EventQuery = string.Empty;
        TextSearchQuery = string.Empty;
        UseStartDateFilter = false;
        UseEndDateFilter = false;
        StartDate = DateTimeOffset.Now;
        EndDate = DateTimeOffset.Now;
        SelectedLevelOption = LevelOptions[0];
        foreach (var facility in Facilities)
        {
            facility.IsSelected = false;
        }

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

    [RelayCommand(CanExecute = nameof(HasCode))]
    private void CopyCode()
    {
        // The clipboard write is performed by the view (a UI concern); this command exists so the copy
        // affordance is only enabled when there is a code to copy. The view reads SelectedCode.
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
            Event = NormalizeFilterText(EventQuery),
            TextSearch = NormalizeFilterText(TextSearchQuery),
            MinimumLevel = SelectedLevelOption?.Rank,
            Facilities = BuildFacilityFilter(),
            StartUtc = UseStartDateFilter ? ToDateBoundaryUtc(StartDate, isEndBoundary: false) : null,
            EndUtc = UseEndDateFilter ? ToDateBoundaryUtc(EndDate, isEndBoundary: true) : null
        };
    }

    /// <summary>
    /// Builds the facility include-set. Null (show everything, including legacy code-less entries) when
    /// no facility is selected or every facility is selected; otherwise only the selected facility bytes.
    /// </summary>
    private IReadOnlyCollection<byte>? BuildFacilityFilter()
    {
        var selected = Facilities.Where(option => option.IsSelected).Select(option => option.Value).ToArray();
        if (selected.Length == 0 || selected.Length == Facilities.Count)
        {
            return null;
        }

        return selected;
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

    private static IReadOnlyList<LogContextRow> ParseContextRows(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<LogContextRow>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<LogContextRow>();
            }

            var rows = new List<LogContextRow>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                rows.Add(new LogContextRow(property.Name, FormatValue(property.Value)));
            }

            return rows;
        }
        catch
        {
            return Array.Empty<LogContextRow>();
        }
    }

    private static string FormatValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Null => "null",
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Number => value.GetRawText(),
        _ => value.GetRawText()
    };

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
