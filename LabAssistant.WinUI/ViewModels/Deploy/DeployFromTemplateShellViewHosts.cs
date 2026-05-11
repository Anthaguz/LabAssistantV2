using LabAssistant.WinUI.Views.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Captures the shell-owned From Template lane hosts.
/// </summary>
internal sealed class DeployFromTemplateShellViewHosts
{
    public DeployFromTemplateView View { get; init; } = null!;

    public DeployFromTemplateRightPanelView RightPanelView { get; init; } = null!;
}
