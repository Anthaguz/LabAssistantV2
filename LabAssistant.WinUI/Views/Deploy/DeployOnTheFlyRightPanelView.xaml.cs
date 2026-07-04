using LabAssistant.WinUI.ViewModels.Deploy;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

public sealed partial class DeployOnTheFlyRightPanelView : UserControl
{
    public DeployProgressViewModel ViewModel { get; } = new();

    public DeployOnTheFlyRightPanelView() => InitializeComponent();
}
