using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

public sealed partial class DeployFromTemplateRightPanelView : UserControl
{
    public DeployFromTemplateRightPanelView()
    {
        InitializeComponent();
    }

    public Expander DeployGlobalIssuesExpanderControl => DeployGlobalIssuesExpander;

    public ListView DeployGlobalIssuesListViewControl => DeployGlobalIssuesListView;

    public ListView DeployVmResultsListViewControl => DeployVmResultsListView;
}
