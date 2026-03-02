using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

public sealed partial class DeployFromTemplateView : UserControl
{
    public DeployFromTemplateView()
    {
        InitializeComponent();
    }

    public ComboBox DeployTemplateSelectorComboBoxControl => DeployTemplateSelectorComboBox;

    public Button DeployReloadTemplatesButtonControl => DeployReloadTemplatesButton;

    public Border DeployReadinessSummaryPanelControl => DeployReadinessSummaryPanel;

    public TextBlock DeployReadinessSummaryTextBlockControl => DeployReadinessSummaryTextBlock;

    public Button DeployResolveSuggestionsButtonControl => DeployResolveSuggestionsButton;

    public Button DeployOpenTemplateEditorButtonControl => DeployOpenTemplateEditorButton;

    public TextBlock DeployActionStatusTextBlockControl => DeployActionStatusTextBlock;
}
