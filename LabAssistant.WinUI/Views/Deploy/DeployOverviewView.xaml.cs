using LabAssistant.WinUI.ViewModels.Deploy;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

public sealed partial class DeployOverviewView : UserControl
{
    public DeployOverviewViewModel ViewModel { get; } = new();

    public DeployOverviewView() => InitializeComponent();
}
