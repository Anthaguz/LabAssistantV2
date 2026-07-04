using LabAssistant.WinUI.ViewModels.Deploy;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

public sealed partial class DeployFromTemplateRightPanelView : UserControl
{
    public DeployCredentialsViewModel ViewModel { get; } = new();

    public DeployFromTemplateRightPanelView() => InitializeComponent();
}
