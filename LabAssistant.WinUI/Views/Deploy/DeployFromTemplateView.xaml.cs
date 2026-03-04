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

    public Button DeployEvaluateReadinessButtonControl => DeployEvaluateReadinessButton;

    public Border DeployReadinessSummaryPanelControl => DeployReadinessSummaryPanel;

    public TextBlock DeployOverallStateTextBlockControl => DeployOverallStateTextBlock;

    public ProgressBar DeployProgressBarControl => DeployProgressBar;

    public TextBlock DeployProgressSummaryTextBlockControl => DeployProgressSummaryTextBlock;

    public TextBlock DeployGlobalIssuesBadgeTextBlockControl => DeployGlobalIssuesBadgeTextBlock;

    public TextBlock DeployReadinessSummaryTextBlockControl => DeployReadinessSummaryTextBlock;

    public Button DeployResolveSuggestionsButtonControl => DeployResolveSuggestionsButton;

    public Button DeployOpenTemplateEditorButtonControl => DeployOpenTemplateEditorButton;

    public Button DeployStartButtonControl => DeployStartButton;

    public TextBlock DeployActionStatusTextBlockControl => DeployActionStatusTextBlock;
}
