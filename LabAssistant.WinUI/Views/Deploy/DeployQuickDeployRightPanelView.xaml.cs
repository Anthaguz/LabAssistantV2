using LabAssistant.WinUI.ViewModels.Deploy;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

/// <summary>
/// Shell right-panel surface for the Quick Deploy lane. Binds to the shared
/// <see cref="DeployProgressViewModel"/> that the host page keeps in sync with lane progress.
/// </summary>
public sealed partial class DeployQuickDeployRightPanelView : UserControl
{
    public DeployProgressViewModel ViewModel { get; } = new();

    public DeployQuickDeployRightPanelView() => InitializeComponent();
}
