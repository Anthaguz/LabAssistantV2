using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

public sealed partial class DeployFromTemplateRightPanelView : UserControl
{
    public DeployFromTemplateRightPanelView()
    {
        InitializeComponent();
    }

    public void SetIssueRowsItemsSource(object? itemsSource)
    {
        DeployGlobalIssuesListView.ItemsSource = itemsSource;
    }

    public void SetResultRowsItemsSource(object? itemsSource)
    {
        DeployVmResultsListView.ItemsSource = itemsSource;
    }

    public void ResetPanelState()
    {
        DeployGlobalIssuesExpander.IsExpanded = false;
    }
}
