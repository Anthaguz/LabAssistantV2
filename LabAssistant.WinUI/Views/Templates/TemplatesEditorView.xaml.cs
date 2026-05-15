using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;

namespace LabAssistant.WinUI.Views.Templates;

public readonly record struct TemplatesEditorDocumentHeaderInteractionState(
    string TemplateName,
    string TemplateDescription);

public readonly record struct TemplatesEditorDocumentHeaderViewState(
    string TemplateEditorContextText,
    string TemplateIdText,
    string TemplateFilePathText,
    string TemplateVmCountText,
    string TemplateName,
    string TemplateDescription,
    string StatusText,
    bool IsStatusVisible);

public readonly record struct TemplatesEditorActionState(
    bool CanSave,
    bool CanSaveAs,
    bool CanValidate,
    bool CanBackToLibrary,
    bool CanAddTemplateVm,
    bool CanRemoveTemplateVm,
    bool CanAddTemplateVmSwitchRow,
    bool CanSelectTemplateVmVhdx,
    bool CanApplyTemplateVmChanges);

public readonly record struct TemplatesEditorVmListInteractionState(
    VmTemplate? SelectedVmEntry);

internal readonly record struct TemplatesEditorVmDraftInteractionState(
    string VmName,
    string VmMemoryText,
    string VmCpuText,
    IReadOnlyList<string> SelectedSwitches,
    TemplateVhdxCatalogOption? SelectedVhdxCatalogOption);

internal readonly record struct TemplatesEditorVmDraftViewState(
    string VmIdText,
    string VmName,
    string VmMemoryText,
    string VmCpuText,
    string VmVhdxIdText,
    string VmVhdPathText,
    string VmVhdxSignatureText,
    IReadOnlyList<string> AvailableSwitches,
    IReadOnlyList<string> SelectedSwitches,
    IReadOnlyList<TemplateVhdxCatalogOption> VhdxCatalogOptions,
    TemplateVhdxCatalogOption? SelectedVhdxCatalogOption,
    string VmSwitchGuidanceText,
    string VmVhdxGuidanceText,
    bool UseUnresolvedVhdxSelection);

public sealed partial class TemplatesEditorView : UserControl
{
    private const string TemplateSwitchPlaceholder = "(Select switch)";
    private const string TemplateVhdxPlaceholder = "(Keep current / unresolved)";

    private bool _isUpdatingDocumentHeader;
    private bool _isUpdatingVmSelection;
    private bool _isUpdatingVmDraft;
    private IReadOnlyList<string> _availableSwitches = Array.Empty<string>();
    private readonly List<ComboBox> _templateVmSwitchRowCombos = [];

    public event EventHandler? DocumentHeaderChanged;
    public event EventHandler? SelectedVmChanged;
    public event EventHandler? VmDraftChanged;
    public event EventHandler? AddVmRequested;
    public event EventHandler? RemoveVmRequested;
    public event EventHandler? ApplyVmChangesRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? SaveAsRequested;
    public event EventHandler? ValidateRequested;
    public event EventHandler? BackToLibraryRequested;

    public TemplatesEditorView()
    {
        InitializeComponent();
        TemplateNameTextBox.TextChanged += TemplateDocumentHeaderTextBox_TextChanged;
        TemplateDescriptionTextBox.TextChanged += TemplateDocumentHeaderTextBox_TextChanged;
        TemplateVmListView.SelectionChanged += TemplateVmListView_SelectionChanged;
        TemplateVmNameTextBox.TextChanged += TemplateVmDraftControl_Changed;
        TemplateVmMemoryTextBox.TextChanged += TemplateVmDraftControl_Changed;
        TemplateVmCpuTextBox.TextChanged += TemplateVmDraftControl_Changed;
        AddTemplateVmSwitchRowButton.Click += AddTemplateVmSwitchRowButton_Click;
        TemplateVmVhdxCatalogComboBox.SelectionChanged += TemplateVmVhdxCatalogComboBox_SelectionChanged;
        AddTemplateVmButton.Click += AddTemplateVmButton_Click;
        RemoveTemplateVmButton.Click += RemoveTemplateVmButton_Click;
        ApplyTemplateVmChangesButton.Click += ApplyTemplateVmChangesButton_Click;
        SaveTemplateButton.Click += SaveTemplateButton_Click;
        SaveTemplateAsButton.Click += SaveTemplateAsButton_Click;
        ValidateTemplateButton.Click += ValidateTemplateButton_Click;
        BackToLibraryButton.Click += BackToLibraryButton_Click;
    }

    public TemplatesEditorDocumentHeaderInteractionState CaptureDocumentHeaderInteractionState()
    {
        return new TemplatesEditorDocumentHeaderInteractionState(
            TemplateNameTextBox.Text,
            TemplateDescriptionTextBox.Text);
    }

    public TemplatesEditorVmListInteractionState CaptureVmListInteractionState()
    {
        return new TemplatesEditorVmListInteractionState(
            TemplateVmListView.SelectedItem as VmTemplate);
    }

    internal TemplatesEditorVmDraftInteractionState CaptureVmDraftInteractionState()
    {
        return new TemplatesEditorVmDraftInteractionState(
            TemplateVmNameTextBox.Text,
            TemplateVmMemoryTextBox.Text,
            TemplateVmCpuTextBox.Text,
            CaptureSelectedSwitches(),
            TemplateVmVhdxCatalogComboBox.SelectedItem as TemplateVhdxCatalogOption);
    }

    public void SetVmEntriesSource(object? itemsSource)
    {
        TemplateVmListView.ItemsSource = itemsSource;
    }

    public void UpdateVmSelection(VmTemplate? selectedVmEntry)
    {
        if (ReferenceEquals(TemplateVmListView.SelectedItem, selectedVmEntry))
        {
            return;
        }

        _isUpdatingVmSelection = true;
        try
        {
            TemplateVmListView.SelectedItem = selectedVmEntry;
        }
        finally
        {
            _isUpdatingVmSelection = false;
        }
    }

    public void RefreshVmEntries()
    {
        var itemsSource = TemplateVmListView.ItemsSource;
        var selectedItem = TemplateVmListView.SelectedItem;
        TemplateVmListView.ItemsSource = null;
        TemplateVmListView.ItemsSource = itemsSource;
        UpdateVmSelection(selectedItem as VmTemplate);
    }

    public void UpdateDocumentHeaderState(TemplatesEditorDocumentHeaderViewState state)
    {
        _isUpdatingDocumentHeader = true;
        try
        {
            SetTextIfChanged(TemplateEditorContextTextBlock, state.TemplateEditorContextText);
            SetTextIfChanged(TemplateIdTextBlock, state.TemplateIdText);
            SetTextIfChanged(TemplateFilePathTextBlock, state.TemplateFilePathText);
            SetTextIfChanged(TemplateVmCountTextBlock, state.TemplateVmCountText);
            SetTextIfChanged(TemplateNameTextBox, state.TemplateName);
            SetTextIfChanged(TemplateDescriptionTextBox, state.TemplateDescription);
            SetTextIfChanged(TemplateEditorStatusTextBlock, state.StatusText);
            TemplateEditorStatusTextBlock.Visibility = state.IsStatusVisible ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _isUpdatingDocumentHeader = false;
        }
    }

    internal void UpdateVmDraftState(TemplatesEditorVmDraftViewState state)
    {
        _isUpdatingVmDraft = true;
        try
        {
            _availableSwitches = state.AvailableSwitches ?? Array.Empty<string>();

            SetTextIfChanged(TemplateVmIdTextBlock, state.VmIdText);
            SetTextIfChanged(TemplateVmNameTextBox, state.VmName);
            SetTextIfChanged(TemplateVmMemoryTextBox, state.VmMemoryText);
            SetTextIfChanged(TemplateVmCpuTextBox, state.VmCpuText);
            SetTextIfChanged(TemplateVmVhdxIdTextBox, state.VmVhdxIdText);
            SetTextIfChanged(TemplateVmVhdPathTextBox, state.VmVhdPathText);
            SetTextIfChanged(TemplateVmVhdxSignatureTextBox, state.VmVhdxSignatureText);
            SetTextIfChanged(TemplateVmSwitchGuidanceTextBlock, state.VmSwitchGuidanceText);
            SetTextIfChanged(TemplateVmVhdxGuidanceTextBlock, state.VmVhdxGuidanceText);

            RenderTemplateSwitchRows(state.SelectedSwitches);

            var vhdxItems = new List<object> { TemplateVhdxPlaceholder };
            vhdxItems.AddRange(state.VhdxCatalogOptions);
            TemplateVmVhdxCatalogComboBox.ItemsSource = vhdxItems;
            TemplateVmVhdxCatalogComboBox.SelectedItem = state.UseUnresolvedVhdxSelection
                ? TemplateVhdxPlaceholder
                : (object?)state.SelectedVhdxCatalogOption ?? TemplateVhdxPlaceholder;
        }
        finally
        {
            _isUpdatingVmDraft = false;
        }
    }

    public void UpdateActionState(TemplatesEditorActionState state)
    {
        SaveTemplateButton.IsEnabled = state.CanSave;
        SaveTemplateAsButton.IsEnabled = state.CanSaveAs;
        ValidateTemplateButton.IsEnabled = state.CanValidate;
        BackToLibraryButton.IsEnabled = state.CanBackToLibrary;
        AddTemplateVmButton.IsEnabled = state.CanAddTemplateVm;
        RemoveTemplateVmButton.IsEnabled = state.CanRemoveTemplateVm;
        AddTemplateVmSwitchRowButton.IsEnabled = state.CanAddTemplateVmSwitchRow;
        TemplateVmVhdxCatalogComboBox.IsEnabled = state.CanSelectTemplateVmVhdx;
        ApplyTemplateVmChangesButton.IsEnabled = state.CanApplyTemplateVmChanges;
    }

    private void TemplateDocumentHeaderTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingDocumentHeader)
        {
            return;
        }

        DocumentHeaderChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TemplateVmListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingVmSelection)
        {
            return;
        }

        SelectedVmChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TemplateVmDraftControl_Changed(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingVmDraft)
        {
            return;
        }

        VmDraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddTemplateVmButton_Click(object sender, RoutedEventArgs e)
    {
        AddVmRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveTemplateVmButton_Click(object sender, RoutedEventArgs e)
    {
        RemoveVmRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyTemplateVmChangesButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyVmChangesRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SaveTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SaveTemplateAsButton_Click(object sender, RoutedEventArgs e)
    {
        SaveAsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ValidateTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        ValidateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BackToLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        BackToLibraryRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AddTemplateVmSwitchRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingVmDraft)
        {
            return;
        }

        AddTemplateSwitchRow(null);
        VmDraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveTemplateVmSwitchRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not ComboBox combo)
        {
            return;
        }

        _templateVmSwitchRowCombos.Remove(combo);

        var rowToRemove = TemplateVmSwitchRowsPanel.Children
            .OfType<Grid>()
            .FirstOrDefault(grid => grid.Children.OfType<ComboBox>().Any(c => ReferenceEquals(c, combo)));
        if (rowToRemove is not null)
        {
            TemplateVmSwitchRowsPanel.Children.Remove(rowToRemove);
        }

        if (_isUpdatingVmDraft)
        {
            return;
        }

        VmDraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TemplateVmSwitchRowCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingVmDraft)
        {
            return;
        }

        VmDraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TemplateVmVhdxCatalogComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingVmDraft)
        {
            return;
        }

        VmDraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private List<string> CaptureSelectedSwitches()
    {
        return _templateVmSwitchRowCombos
            .Select(combo => combo.SelectedItem?.ToString())
            .Select(value => string.Equals(value, TemplateSwitchPlaceholder, StringComparison.Ordinal)
                ? string.Empty
                : value?.Trim() ?? string.Empty)
            .ToList();
    }

    private void RenderTemplateSwitchRows(IReadOnlyList<string> selectedSwitches)
    {
        TemplateVmSwitchRowsPanel.Children.Clear();
        _templateVmSwitchRowCombos.Clear();

        foreach (var switchName in selectedSwitches)
        {
            AddTemplateSwitchRow(switchName);
        }
    }

    private void AddTemplateSwitchRow(string? selectedSwitch)
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
        combo.Items.Add(TemplateSwitchPlaceholder);
        foreach (var switchName in _availableSwitches)
        {
            combo.Items.Add(switchName);
        }

        var validSelection = !string.IsNullOrWhiteSpace(selectedSwitch) &&
                             _availableSwitches.Contains(selectedSwitch, StringComparer.OrdinalIgnoreCase);
        combo.SelectedItem = validSelection ? selectedSwitch : TemplateSwitchPlaceholder;
        combo.SelectionChanged += TemplateVmSwitchRowCombo_SelectionChanged;
        _templateVmSwitchRowCombos.Add(combo);
        Grid.SetColumn(combo, 0);
        row.Children.Add(combo);

        var removeButton = new Button
        {
            Content = "-",
            Tag = combo
        };
        ToolTipService.SetToolTip(removeButton, "Remove switch");
        removeButton.Click += RemoveTemplateVmSwitchRowButton_Click;
        Grid.SetColumn(removeButton, 1);
        row.Children.Add(removeButton);

        TemplateVmSwitchRowsPanel.Children.Add(row);
    }

    private static void SetTextIfChanged(TextBox textBox, string value)
    {
        if (!string.Equals(textBox.Text, value, StringComparison.Ordinal))
        {
            textBox.Text = value;
        }
    }

    private static void SetTextIfChanged(TextBlock textBlock, string value)
    {
        if (!string.Equals(textBlock.Text, value, StringComparison.Ordinal))
        {
            textBlock.Text = value;
        }
    }
}
