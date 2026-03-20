using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Views.Diagnostics;

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
}
