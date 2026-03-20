using LabAssistant.Services.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Diagnostics;

public readonly record struct DiagnosticsLogsFilterViewState(
    string OperationIdQuery,
    string LevelQuery,
    string EventQuery,
    string TextSearchQuery,
    bool UseStartDateFilter,
    DateTimeOffset StartDate,
    bool UseEndDateFilter,
    DateTimeOffset EndDate);

public readonly record struct DiagnosticsLogsSelectionViewState(
    StructuredLogViewerEntry? SelectedEntry,
    string EnvelopeText,
    string ContextText);

public sealed partial class DiagnosticsLogsView : UserControl
{
    private bool _isApplyingFilterState;

    public event EventHandler? FilterStateChanged;

    public event RoutedEventHandler? ApplyFiltersRequested;

    public event RoutedEventHandler? ClearFiltersRequested;

    public event EventHandler? SelectedLogChanged;

    public DiagnosticsLogsView()
    {
        InitializeComponent();
        WireFilterStateHandlers();
        ApplyFilterState(new DiagnosticsLogsFilterViewState(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            false,
            DateTimeOffset.Now,
            false,
            DateTimeOffset.Now));
        ApplySelectionState(new DiagnosticsLogsSelectionViewState(
            null,
            "Select a log entry.",
            string.Empty));
    }

    public DiagnosticsLogsFilterViewState CaptureFilterState()
    {
        return new DiagnosticsLogsFilterViewState(
            LogFilterOperationIdTextBox.Text,
            LogFilterLevelTextBox.Text,
            LogFilterEventTextBox.Text,
            LogFilterTextSearchTextBox.Text,
            LogFilterUseStartDateCheckBox.IsChecked == true,
            LogFilterStartDatePicker.Date,
            LogFilterUseEndDateCheckBox.IsChecked == true,
            LogFilterEndDatePicker.Date);
    }

    public void ApplyFilterState(DiagnosticsLogsFilterViewState state)
    {
        _isApplyingFilterState = true;
        try
        {
            LogFilterOperationIdTextBox.Text = state.OperationIdQuery;
            LogFilterLevelTextBox.Text = state.LevelQuery;
            LogFilterEventTextBox.Text = state.EventQuery;
            LogFilterTextSearchTextBox.Text = state.TextSearchQuery;
            LogFilterUseStartDateCheckBox.IsChecked = state.UseStartDateFilter;
            LogFilterStartDatePicker.Date = state.StartDate;
            LogFilterUseEndDateCheckBox.IsChecked = state.UseEndDateFilter;
            LogFilterEndDatePicker.Date = state.EndDate;
        }
        finally
        {
            _isApplyingFilterState = false;
        }
    }

    public StructuredLogViewerEntry? CaptureSelectedLogEntry()
    {
        return StructuredLogsListView.SelectedItem as StructuredLogViewerEntry;
    }

    public void ApplySelectionState(DiagnosticsLogsSelectionViewState state)
    {
        if (!ReferenceEquals(StructuredLogsListView.SelectedItem, state.SelectedEntry))
        {
            StructuredLogsListView.SelectedItem = state.SelectedEntry;
        }

        SelectedLogEnvelopeTextBlock.Text = state.EnvelopeText;
        SelectedLogContextTextBox.Text = state.ContextText;
    }

    private void WireFilterStateHandlers()
    {
        LogFilterOperationIdTextBox.TextChanged += FilterTextBox_TextChanged;
        LogFilterLevelTextBox.TextChanged += FilterTextBox_TextChanged;
        LogFilterEventTextBox.TextChanged += FilterTextBox_TextChanged;
        LogFilterTextSearchTextBox.TextChanged += FilterTextBox_TextChanged;
        LogFilterUseStartDateCheckBox.Checked += FilterControl_StateChanged;
        LogFilterUseStartDateCheckBox.Unchecked += FilterControl_StateChanged;
        LogFilterUseEndDateCheckBox.Checked += FilterControl_StateChanged;
        LogFilterUseEndDateCheckBox.Unchecked += FilterControl_StateChanged;
        LogFilterStartDatePicker.DateChanged += FilterDatePicker_DateChanged;
        LogFilterEndDatePicker.DateChanged += FilterDatePicker_DateChanged;
        ApplyLogFiltersButton.Click += ApplyLogFiltersButton_Click;
        ClearLogFiltersButton.Click += ClearLogFiltersButton_Click;
        StructuredLogsListView.SelectionChanged += StructuredLogsListView_SelectionChanged;
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isApplyingFilterState)
        {
            return;
        }

        FilterStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FilterControl_StateChanged(object sender, RoutedEventArgs e)
    {
        if (_isApplyingFilterState)
        {
            return;
        }

        FilterStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FilterDatePicker_DateChanged(object? sender, DatePickerValueChangedEventArgs args)
    {
        if (_isApplyingFilterState)
        {
            return;
        }

        FilterStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyLogFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyFiltersRequested?.Invoke(this, e);
    }

    private void ClearLogFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        ClearFiltersRequested?.Invoke(this, e);
    }

    private void StructuredLogsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedLogChanged?.Invoke(this, EventArgs.Empty);
    }
}
