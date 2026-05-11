using LabAssistant.WinUI.Views.Deploy;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Groups the shell-owned Deploy hosts that capability bootstrap may bind to long-lived Deploy seams.
/// </summary>
internal sealed class DeployCapabilityShellViewHosts
{
    public FrameworkElement LocalNavigationHost { get; init; } = null!;

    public DeployOverviewView OverviewView { get; init; } = null!;

    public DeployQuickDeployShellViewHosts QuickDeploy { get; init; } = null!;

    public DeployFromTemplateShellViewHosts FromTemplate { get; init; } = null!;

    public DeployCapabilityShellNavigationHosts Navigation { get; init; } = null!;
}
