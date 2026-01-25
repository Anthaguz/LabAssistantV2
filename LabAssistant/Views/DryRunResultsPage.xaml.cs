using System.Windows.Controls;
using LabAssistant.Business.Deployment;

namespace LabAssistant.Views;

public partial class DryRunResultsPage : Page
{
    public DryRunResultsPage(DryRunDeploymentResult result)
    {
        InitializeComponent();
        SummaryText.Text = $"Steps: {result.Plan.Steps.Count}";
        StepsList.ItemsSource = result.Plan.Steps;
    }
}
