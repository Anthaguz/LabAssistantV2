using LabAssistant.WinUI.Views.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Captures the shell-owned Quick Deploy lane hosts.
/// </summary>
internal sealed class DeployQuickDeployShellViewHosts
{
    public DeployOnTheFlyView View { get; init; } = null!;

    public DeployOnTheFlyRightPanelView RightPanelView { get; init; } = null!;
}
