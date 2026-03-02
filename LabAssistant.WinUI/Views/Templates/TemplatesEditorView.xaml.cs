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

    public TextBlock TemplateEditorStatusTextBlockControl => TemplateEditorStatusTextBlock;

    public Button SaveTemplateButtonControl => SaveTemplateButton;

    public Button SaveTemplateAsButtonControl => SaveTemplateAsButton;

    public Button ValidateTemplateButtonControl => ValidateTemplateButton;

    public Button BackToLibraryButtonControl => BackToLibraryButton;
}
