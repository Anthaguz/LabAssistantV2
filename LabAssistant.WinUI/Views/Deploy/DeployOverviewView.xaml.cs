using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

public sealed partial class DeployOverviewView : UserControl
{
    public event EventHandler? OpenQuickDeployRequested;

    public event EventHandler? OpenFromTemplateRequested;

    public DeployOverviewView()
    {
        InitializeComponent();
    }

    public void UpdateSummary(string quickDeploySummaryText, string fromTemplateSummaryText)
    {
        DeployOverviewQuickDeploySummaryTextBlock.Text = quickDeploySummaryText;
        DeployOverviewFromTemplateSummaryTextBlock.Text = fromTemplateSummaryText;
    }

    private void DeployOverviewOpenQuickDeployButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        OpenQuickDeployRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DeployOverviewOpenFromTemplateButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        OpenFromTemplateRequested?.Invoke(this, EventArgs.Empty);
    }
}
