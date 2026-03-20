using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Views.Diagnostics;
using System.Text.Json;

namespace LabAssistant.WinUI.ViewModels.Diagnostics;

internal sealed class DiagnosticsLogsWorkspaceViewModel
{
    public string OperationIdQuery { get; private set; } = string.Empty;

    public string LevelQuery { get; private set; } = string.Empty;

    public string EventQuery { get; private set; } = string.Empty;

    public string TextSearchQuery { get; private set; } = string.Empty;

    public bool UseStartDateFilter { get; private set; }

    public DateTimeOffset StartDate { get; private set; } = DateTimeOffset.Now;

    public bool UseEndDateFilter { get; private set; }

    public DateTimeOffset EndDate { get; private set; } = DateTimeOffset.Now;

    public StructuredLogViewerEntry? SelectedEntry { get; private set; }

    public string? SelectedEntryOperationId { get; private set; }

    public string SelectedEnvelopeText { get; private set; } = "Select a log entry.";

    public string SelectedContextText { get; private set; } = string.Empty;

    public void ApplyFilterState(DiagnosticsLogsFilterViewState state)
    {
        OperationIdQuery = NormalizeFilterText(state.OperationIdQuery);
        LevelQuery = NormalizeFilterText(state.LevelQuery);
        EventQuery = NormalizeFilterText(state.EventQuery);
        TextSearchQuery = NormalizeFilterText(state.TextSearchQuery);
        UseStartDateFilter = state.UseStartDateFilter;
        StartDate = state.StartDate;
        UseEndDateFilter = state.UseEndDateFilter;
        EndDate = state.EndDate;
    }

    public void ClearFilters()
    {
        OperationIdQuery = string.Empty;
        LevelQuery = string.Empty;
        EventQuery = string.Empty;
        TextSearchQuery = string.Empty;
        UseStartDateFilter = false;
        UseEndDateFilter = false;
        StartDate = DateTimeOffset.Now;
        EndDate = DateTimeOffset.Now;
    }

    public void SetSelectedEntry(StructuredLogViewerEntry? entry)
    {
        SelectedEntry = entry;
        SelectedEntryOperationId = entry?.OperationId;

        if (entry is null)
        {
            SelectedEnvelopeText = "Select a log entry.";
            SelectedContextText = string.Empty;
            return;
        }

        SelectedEnvelopeText =
            $"ts={entry.TimestampText} | level={entry.Level} | event={entry.Event} | operationId={entry.OperationId} | result={entry.Result}";
        SelectedContextText = FormatJsonForDetails(entry.ContextJson);
    }

    public void ClearSelection()
    {
        SetSelectedEntry(null);
    }

    public DiagnosticsLogsFilterViewState BuildViewState()
    {
        return new DiagnosticsLogsFilterViewState(
            OperationIdQuery,
            LevelQuery,
            EventQuery,
            TextSearchQuery,
            UseStartDateFilter,
            StartDate,
            UseEndDateFilter,
            EndDate);
    }

    public DiagnosticsLogsSelectionViewState BuildSelectionViewState()
    {
        return new DiagnosticsLogsSelectionViewState(
            SelectedEntry,
            SelectedEnvelopeText,
            SelectedContextText);
    }

    public StructuredLogViewerFilter BuildStructuredLogFilter()
    {
        return new StructuredLogViewerFilter
        {
            OperationId = OperationIdQuery,
            Level = LevelQuery,
            Event = EventQuery,
            TextSearch = TextSearchQuery,
            StartUtc = UseStartDateFilter
                ? ToDateBoundaryUtc(StartDate, isEndBoundary: false)
                : null,
            EndUtc = UseEndDateFilter
                ? ToDateBoundaryUtc(EndDate, isEndBoundary: true)
                : null
        };
    }

    private static string NormalizeFilterText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim();
    }

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
