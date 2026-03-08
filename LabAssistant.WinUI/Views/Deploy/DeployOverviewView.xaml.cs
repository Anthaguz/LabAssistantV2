using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

public sealed partial class DeployOverviewView : UserControl
{
    public DeployOverviewView()
    {
        InitializeComponent();
    }

    public Button DeployOverviewOpenQuickDeployButtonControl => DeployOverviewOpenQuickDeployButton;

    public Button DeployOverviewOpenFromTemplateButtonControl => DeployOverviewOpenFromTemplateButton;

    public TextBlock DeployOverviewQuickDeploySummaryTextBlockControl => DeployOverviewQuickDeploySummaryTextBlock;

    public TextBlock DeployOverviewFromTemplateSummaryTextBlockControl => DeployOverviewFromTemplateSummaryTextBlock;
}
