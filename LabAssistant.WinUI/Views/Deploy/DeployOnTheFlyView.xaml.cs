using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.ViewModels.Templates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

public readonly record struct DeployOnTheFlyVmSelectionInteractionState(
    DeployOnTheFlyVmEntryRow? SelectedVmEntryRow);

internal readonly record struct DeployOnTheFlyEditorInteractionState(
    string VmName,
    string VmMemoryText,
    string VmCpuText,
    object? SelectedSwitchItem,
    TemplateVhdxCatalogOption? SelectedVhdxCatalogOption);

internal readonly record struct DeployOnTheFlyEditorViewState(
    string VmName,
    string VmMemoryText,
    string VmCpuText,
    IReadOnlyList<object> SwitchItems,
    object? SelectedSwitchItem,
    string SwitchGuidanceText,
    IReadOnlyList<object> VhdxCatalogItems,
    object? SelectedVhdxCatalogItem,
    string VhdxGuidanceText);

public readonly record struct DeployOnTheFlyWorkspaceViewState(
    bool CanAddVm,
    bool CanRemoveVm,
    bool CanApplyVmChanges,
    bool CanEvaluate,
    bool CanResolveSuggestions,
    bool CanOpenTemplateEditor,
    bool CanStartDeploy,
    string EditorIssueSummaryText,
    string OverallStateText,
    int ProgressPercent,
    string ProgressSummaryText,
    string GlobalIssuesBadgeText,
    string ReadinessSummaryText,
    string StatusText);

public sealed partial class DeployOnTheFlyView : UserControl
{
    private const double CompactLayoutThreshold = 1120;
    private bool _isUpdatingVmSelection;
    private bool _isUpdatingEditorState;

    public event EventHandler? VmEntrySelectionChanged;
    public event Action<VmTemplate>? VmRemoveRequested;
    public event EventHandler? AddVmRequested;
    public event EventHandler? RemoveSelectedVmRequested;
    public event EventHandler? ApplyVmChangesRequested;
    public event EventHandler? VmDraftChanged;
    public event EventHandler? EvaluateRequested;
    public event EventHandler? ResolveSuggestionsRequested;
    public event EventHandler? OpenTemplateEditorRequested;
    public event EventHandler? StartDeployRequested;
    public event EventHandler? OpenResultsPanelRequested;

    public DeployOnTheFlyView()
    {
        InitializeComponent();
        SizeChanged += DeployOnTheFlyView_SizeChanged;
        WireHandlers();
        UpdateLayoutMode(CompactLayoutThreshold + 1);
    }

    public void SetVmEntriesSource(object? itemsSource)
    {
        DeployOnTheFlyVmEntriesListView.ItemsSource = itemsSource;
    }

    public DeployOnTheFlyVmSelectionInteractionState CaptureVmSelectionInteractionState()
    {
        return new DeployOnTheFlyVmSelectionInteractionState(
            DeployOnTheFlyVmEntriesListView.SelectedItem as DeployOnTheFlyVmEntryRow);
    }

    internal DeployOnTheFlyEditorInteractionState CaptureEditorInteractionState()
    {
        return new DeployOnTheFlyEditorInteractionState(
            DeployOnTheFlyVmNameTextBox.Text,
            DeployOnTheFlyVmMemoryTextBox.Text,
            DeployOnTheFlyVmCpuTextBox.Text,
            DeployOnTheFlyVmSwitchComboBox.SelectedItem,
            DeployOnTheFlyVmVhdxCatalogComboBox.SelectedItem as TemplateVhdxCatalogOption);
    }

    public void SetVmEntrySelection(DeployOnTheFlyVmEntryRow? selectedVmEntryRow)
    {
        if (ReferenceEquals(DeployOnTheFlyVmEntriesListView.SelectedItem, selectedVmEntryRow))
        {
            return;
        }

        _isUpdatingVmSelection = true;
        try
        {
            DeployOnTheFlyVmEntriesListView.SelectedItem = selectedVmEntryRow;
        }
        finally
        {
            _isUpdatingVmSelection = false;
        }
    }

    internal void ApplyEditorViewState(DeployOnTheFlyEditorViewState state)
    {
        _isUpdatingEditorState = true;
        try
        {
            DeployOnTheFlyVmNameTextBox.Text = state.VmName;
            DeployOnTheFlyVmMemoryTextBox.Text = state.VmMemoryText;
            DeployOnTheFlyVmCpuTextBox.Text = state.VmCpuText;
            DeployOnTheFlyVmSwitchComboBox.ItemsSource = state.SwitchItems;
            DeployOnTheFlyVmSwitchComboBox.SelectedItem = state.SelectedSwitchItem;
            DeployOnTheFlyVmSwitchGuidanceTextBlock.Text = state.SwitchGuidanceText;
            DeployOnTheFlyVmVhdxCatalogComboBox.ItemsSource = state.VhdxCatalogItems;
            DeployOnTheFlyVmVhdxCatalogComboBox.SelectedItem = state.SelectedVhdxCatalogItem;
            DeployOnTheFlyVmVhdxGuidanceTextBlock.Text = state.VhdxGuidanceText;
        }
        finally
        {
            _isUpdatingEditorState = false;
        }
    }

    public void ApplyWorkspaceState(DeployOnTheFlyWorkspaceViewState state)
    {
        DeployOnTheFlyAddVmButton.IsEnabled = state.CanAddVm;
        DeployOnTheFlyRemoveVmButton.IsEnabled = state.CanRemoveVm;
        DeployOnTheFlyApplyVmChangesButton.IsEnabled = state.CanApplyVmChanges;
        DeployOnTheFlyEvaluateButton.IsEnabled = state.CanEvaluate;
        DeployOnTheFlyResolveSuggestionsButton.IsEnabled = state.CanResolveSuggestions;
        DeployOnTheFlyOpenTemplateEditorButton.IsEnabled = state.CanOpenTemplateEditor;
        DeployOnTheFlyStartButton.IsEnabled = state.CanStartDeploy;
        DeployOnTheFlyEditorIssueSummaryTextBlock.Text = state.EditorIssueSummaryText;
        DeployOnTheFlyOverallStateTextBlock.Text = state.OverallStateText;
        DeployOnTheFlyProgressBar.Value = state.ProgressPercent;
        DeployOnTheFlyProgressSummaryTextBlock.Text = state.ProgressSummaryText;
        DeployOnTheFlyGlobalIssuesBadgeTextBlock.Text = state.GlobalIssuesBadgeText;
        DeployOnTheFlyReadinessSummaryTextBlock.Text = state.ReadinessSummaryText;
        DeployOnTheFlyStatusTextBlock.Text = state.StatusText;
    }

    public void SetResultsPanelLauncherState(string buttonText, bool isEnabled, string summaryText)
    {
        DeployOnTheFlyOpenResultsPanelButton.Content = buttonText;
        DeployOnTheFlyOpenResultsPanelButton.IsEnabled = isEnabled;
        DeployOnTheFlyResultsPanelSummaryTextBlock.Text = summaryText;
    }

    private void WireHandlers()
    {
        DeployOnTheFlyVmEntriesListView.SelectionChanged += DeployOnTheFlyVmEntriesListView_SelectionChanged;
        DeployOnTheFlyAddVmButton.Click += (_, _) => AddVmRequested?.Invoke(this, EventArgs.Empty);
        DeployOnTheFlyRemoveVmButton.Click += (_, _) => RemoveSelectedVmRequested?.Invoke(this, EventArgs.Empty);
        DeployOnTheFlyApplyVmChangesButton.Click += (_, _) => ApplyVmChangesRequested?.Invoke(this, EventArgs.Empty);
        DeployOnTheFlyVmNameTextBox.TextChanged += DeployOnTheFlyEditorControl_Changed;
        DeployOnTheFlyVmMemoryTextBox.TextChanged += DeployOnTheFlyEditorControl_Changed;
        DeployOnTheFlyVmCpuTextBox.TextChanged += DeployOnTheFlyEditorControl_Changed;
        DeployOnTheFlyVmSwitchComboBox.SelectionChanged += DeployOnTheFlyEditorControl_Changed;
        DeployOnTheFlyVmVhdxCatalogComboBox.SelectionChanged += DeployOnTheFlyEditorControl_Changed;
        DeployOnTheFlyEvaluateButton.Click += (_, _) => EvaluateRequested?.Invoke(this, EventArgs.Empty);
        DeployOnTheFlyResolveSuggestionsButton.Click += (_, _) => ResolveSuggestionsRequested?.Invoke(this, EventArgs.Empty);
        DeployOnTheFlyOpenTemplateEditorButton.Click += (_, _) => OpenTemplateEditorRequested?.Invoke(this, EventArgs.Empty);
        DeployOnTheFlyStartButton.Click += (_, _) => StartDeployRequested?.Invoke(this, EventArgs.Empty);
        DeployOnTheFlyOpenResultsPanelButton.Click += (_, _) => OpenResultsPanelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DeployOnTheFlyRowRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: VmTemplate vmEntry })
        {
            VmRemoveRequested?.Invoke(vmEntry);
        }
    }

    private void DeployOnTheFlyView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLayoutMode(e.NewSize.Width);
    }

    private void DeployOnTheFlyVmEntriesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingVmSelection)
        {
            return;
        }

        VmEntrySelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DeployOnTheFlyEditorControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingEditorState)
        {
            return;
        }

        VmDraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateLayoutMode(double width)
    {
        var useStackedLayout = width < CompactLayoutThreshold;
        DeployOnTheFlyListColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
        DeployOnTheFlyEditorColumnDefinition.Width = useStackedLayout ? new GridLength(0) : new GridLength(1.25, GridUnitType.Star);
        DeployOnTheFlyPrimaryRowDefinition.Height = new GridLength(1, GridUnitType.Star);
        DeployOnTheFlyEditorRowDefinition.Height = useStackedLayout ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        Grid.SetRow(DeployOnTheFlyVmEntriesPanel, 0);
        Grid.SetColumn(DeployOnTheFlyVmEntriesPanel, 0);

        Grid.SetRow(DeployOnTheFlyVmEditorPanel, useStackedLayout ? 1 : 0);
        Grid.SetColumn(DeployOnTheFlyVmEditorPanel, useStackedLayout ? 0 : 1);
    }
}
