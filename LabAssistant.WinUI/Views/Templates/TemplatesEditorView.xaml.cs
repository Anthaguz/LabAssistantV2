using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using LabAssistant.Models.Templates;

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

public sealed partial class TemplatesEditorView : UserControl
{
    private bool _isUpdatingDocumentHeader;
    private bool _isUpdatingVmSelection;

    public event EventHandler? DocumentHeaderChanged;
    public event EventHandler? SelectedVmChanged;

    public TemplatesEditorView()
    {
        InitializeComponent();
        TemplateNameTextBox.TextChanged += TemplateDocumentHeaderTextBox_TextChanged;
        TemplateDescriptionTextBox.TextChanged += TemplateDocumentHeaderTextBox_TextChanged;
        TemplateVmListView.SelectionChanged += TemplateVmListView_SelectionChanged;
    }

    public ListView TemplateVmListViewControl => TemplateVmListView;

    public Button AddTemplateVmButtonControl => AddTemplateVmButton;

    public Button RemoveTemplateVmButtonControl => RemoveTemplateVmButton;

    public TextBlock TemplateVmIdTextBlockControl => TemplateVmIdTextBlock;

    public TextBox TemplateVmNameTextBoxControl => TemplateVmNameTextBox;

    public TextBox TemplateVmMemoryTextBoxControl => TemplateVmMemoryTextBox;

    public TextBox TemplateVmCpuTextBoxControl => TemplateVmCpuTextBox;

    public StackPanel TemplateVmSwitchRowsPanelControl => TemplateVmSwitchRowsPanel;

    public Button AddTemplateVmSwitchRowButtonControl => AddTemplateVmSwitchRowButton;

    public TextBlock TemplateVmSwitchGuidanceTextBlockControl => TemplateVmSwitchGuidanceTextBlock;

    public TextBox TemplateVmVhdxIdTextBoxControl => TemplateVmVhdxIdTextBox;

    public ComboBox TemplateVmVhdxCatalogComboBoxControl => TemplateVmVhdxCatalogComboBox;

    public TextBlock TemplateVmVhdxGuidanceTextBlockControl => TemplateVmVhdxGuidanceTextBlock;

    public TextBox TemplateVmVhdPathTextBoxControl => TemplateVmVhdPathTextBox;

    public TextBox TemplateVmVhdxSignatureTextBoxControl => TemplateVmVhdxSignatureTextBox;

    public Button ApplyTemplateVmChangesButtonControl => ApplyTemplateVmChangesButton;

    public Button SaveTemplateButtonControl => SaveTemplateButton;

    public Button SaveTemplateAsButtonControl => SaveTemplateAsButton;

    public Button ValidateTemplateButtonControl => ValidateTemplateButton;

    public Button BackToLibraryButtonControl => BackToLibraryButton;

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
