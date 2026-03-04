using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

public sealed partial class DeployOnTheFlyRightPanelView : UserControl
{
    public DeployOnTheFlyRightPanelView()
    {
        InitializeComponent();
    }

    public ListView DeployOnTheFlyVmResultsListViewControl => DeployOnTheFlyVmResultsListView;
}
