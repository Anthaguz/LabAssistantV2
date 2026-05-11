using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Captures the shell-owned Deploy navigation surfaces used by capability-local composition.
/// </summary>
internal sealed class DeployCapabilityShellNavigationHosts
{
    public TabView SubviewTabView { get; init; } = null!;

    public TabViewItem OverviewTabViewItem { get; init; } = null!;

    public TabViewItem QuickDeployTabViewItem { get; init; } = null!;

    public TabViewItem FromTemplateTabViewItem { get; init; } = null!;
}
