namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Collects the shell-owned inputs needed to bootstrap the long-lived Deploy capability runtime.
/// </summary>
internal sealed class DeployCapabilityBootstrapContext
{
    public DeployCapabilityShellBridge ShellBridge { get; init; } = null!;

    public DeployCapabilityShellViewHosts ShellViewHosts { get; init; } = null!;

    public DeployTemplatesShellAdapter Templates { get; init; } = null!;
}
