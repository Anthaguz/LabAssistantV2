using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

public sealed partial class TemplatesLibraryView : UserControl
{
    public TemplatesLibraryView()
    {
        InitializeComponent();
    }

    public ListView TemplateLibraryListViewControl => TemplateLibraryListView;

    public TextBox TemplateSearchTextBoxControl => TemplateSearchTextBox;

    public Button ApplyTemplateSearchButtonControl => ApplyTemplateSearchButton;

    public Button ClearTemplateSearchButtonControl => ClearTemplateSearchButton;

    public Button ReloadTemplatesButtonControl => ReloadTemplatesButton;

    public Button OpenTemplateInEditorButtonControl => OpenTemplateInEditorButton;

    public Button CreateTemplateButtonControl => CreateTemplateButton;

    public Button DeleteTemplateButtonControl => DeleteTemplateButton;

    public Button ImportTemplateButtonControl => ImportTemplateButton;

    public Button ExportTemplateButtonControl => ExportTemplateButton;

    public TextBlock TemplatesLibraryStatusTextBlockControl => TemplatesLibraryStatusTextBlock;
}
