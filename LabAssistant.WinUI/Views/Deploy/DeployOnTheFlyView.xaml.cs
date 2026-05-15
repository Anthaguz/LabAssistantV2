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
    IReadOnlyList<string> SelectedSwitches,
    TemplateVhdxCatalogOption? SelectedVhdxCatalogOption);

internal readonly record struct DeployOnTheFlyEditorViewState(
    string VmName,
    string VmMemoryText,
    string VmCpuText,
    IReadOnlyList<string> AvailableSwitches,
    IReadOnlyList<string> SelectedSwitches,
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
    private const string SwitchPlaceholder = "(Select switch)";
    private bool _isUpdatingVmSelection;
    private bool _isUpdatingEditorState;
    private IReadOnlyList<string> _availableSwitches = Array.Empty<string>();
    private readonly List<ComboBox> _switchRowCombos = [];

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
            CaptureSelectedSwitches(),
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
            _availableSwitches = state.AvailableSwitches ?? Array.Empty<string>();
            RenderSwitchRows(state.SelectedSwitches);
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
        DeployOnTheFlyAddVmSwitchRowButton.Click += DeployOnTheFlyAddVmSwitchRowButton_Click;
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

    private void DeployOnTheFlyAddVmSwitchRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingEditorState)
        {
            return;
        }

        AddSwitchRow(null);
        VmDraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DeployOnTheFlyRemoveVmSwitchRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ComboBox combo })
        {
            return;
        }

        _switchRowCombos.Remove(combo);

        var rowToRemove = DeployOnTheFlyVmSwitchRowsPanel.Children
            .OfType<Grid>()
            .FirstOrDefault(grid => grid.Children.OfType<ComboBox>().Any(c => ReferenceEquals(c, combo)));
        if (rowToRemove is not null)
        {
            DeployOnTheFlyVmSwitchRowsPanel.Children.Remove(rowToRemove);
        }

        if (_isUpdatingEditorState)
        {
            return;
        }

        VmDraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DeployOnTheFlySwitchRowCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

    private List<string> CaptureSelectedSwitches()
    {
        return _switchRowCombos
            .Select(combo => combo.SelectedItem?.ToString())
            .Select(value => string.Equals(value, SwitchPlaceholder, StringComparison.Ordinal)
                ? string.Empty
                : value?.Trim() ?? string.Empty)
            .ToList();
    }

    private void RenderSwitchRows(IReadOnlyList<string> selectedSwitches)
    {
        DeployOnTheFlyVmSwitchRowsPanel.Children.Clear();
        _switchRowCombos.Clear();

        foreach (var selectedSwitch in selectedSwitches)
        {
            AddSwitchRow(selectedSwitch);
        }
    }

    private void AddSwitchRow(string? selectedSwitch)
    {
        var row = new Grid
        {
            ColumnSpacing = 8
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var combo = new ComboBox
        {
            MinWidth = 220
        };
        combo.Items.Add(SwitchPlaceholder);
        foreach (var switchName in _availableSwitches)
        {
            combo.Items.Add(switchName);
        }

        var validSelection = !string.IsNullOrWhiteSpace(selectedSwitch) &&
                             _availableSwitches.Contains(selectedSwitch, StringComparer.OrdinalIgnoreCase);
        combo.SelectedItem = validSelection ? selectedSwitch : SwitchPlaceholder;
        combo.SelectionChanged += DeployOnTheFlySwitchRowCombo_SelectionChanged;
        _switchRowCombos.Add(combo);
        Grid.SetColumn(combo, 0);
        row.Children.Add(combo);

        var removeButton = new Button
        {
            Content = "-",
            Tag = combo
        };
        ToolTipService.SetToolTip(removeButton, "Remove switch");
        removeButton.Click += DeployOnTheFlyRemoveVmSwitchRowButton_Click;
        Grid.SetColumn(removeButton, 1);
        row.Children.Add(removeButton);

        DeployOnTheFlyVmSwitchRowsPanel.Children.Add(row);
    }
}
