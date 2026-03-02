using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

public sealed partial class TemplatesEditorView : UserControl
{
    public TemplatesEditorView()
    {
        InitializeComponent();
    }

    public TextBox TemplateNameTextBoxControl => TemplateNameTextBox;

    public TextBox TemplateDescriptionTextBoxControl => TemplateDescriptionTextBox;

    public TextBlock TemplateEditorContextTextBlockControl => TemplateEditorContextTextBlock;

    public TextBlock TemplateIdTextBlockControl => TemplateIdTextBlock;

    public TextBlock TemplateFilePathTextBlockControl => TemplateFilePathTextBlock;

    public TextBlock TemplateVmCountTextBlockControl => TemplateVmCountTextBlock;

    public ListView TemplateVmListViewControl => TemplateVmListView;

    public Button AddTemplateVmButtonControl => AddTemplateVmButton;

    public Button RemoveTemplateVmButtonControl => RemoveTemplateVmButton;

    public TextBlock TemplateVmIdTextBlockControl => TemplateVmIdTextBlock;

    public TextBox TemplateVmNameTextBoxControl => TemplateVmNameTextBox;

    public TextBox TemplateVmMemoryTextBoxControl => TemplateVmMemoryTextBox;

    public TextBox TemplateVmCpuTextBoxControl => TemplateVmCpuTextBox;

    public TextBox TemplateVmSwitchTextBoxControl => TemplateVmSwitchTextBox;

    public TextBox TemplateVmVhdxIdTextBoxControl => TemplateVmVhdxIdTextBox;

    public TextBox TemplateVmVhdPathTextBoxControl => TemplateVmVhdPathTextBox;

    public TextBox TemplateVmVhdxSignatureTextBoxControl => TemplateVmVhdxSignatureTextBox;

    public Button ApplyTemplateVmChangesButtonControl => ApplyTemplateVmChangesButton;

    public TextBlock TemplateEditorStatusTextBlockControl => TemplateEditorStatusTextBlock;

    public Button SaveTemplateButtonControl => SaveTemplateButton;

    public Button SaveTemplateAsButtonControl => SaveTemplateAsButton;

    public Button ValidateTemplateButtonControl => ValidateTemplateButton;

    public Button BackToLibraryButtonControl => BackToLibraryButton;
}
