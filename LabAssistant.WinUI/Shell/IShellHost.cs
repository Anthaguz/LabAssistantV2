using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.Shell;

/// <summary>
/// Narrow seam a capability <see cref="Microsoft.UI.Xaml.Controls.Page"/> uses to coordinate
/// with the shell. Replaces the per-capability delegate-bag "shell bridge" layer: a page owns
/// its own capability and talks back to the shell only through this contract.
/// </summary>
internal interface IShellHost
{
    /// <summary>
    /// Requests root capability/subview navigation. Routes through the shell
    /// <see cref="Microsoft.UI.Xaml.Controls.NavigationView"/> and capability frame.
    /// </summary>
    void NavigateToRoute(string routeKey);

    /// <summary>
    /// Reports the active subview a page has switched to on its own (for example via an internal
    /// tab) so the shell updates header context and navigation selection without re-navigating
    /// the capability frame.
    /// </summary>
    void ReportActiveSubview(string routeKey);

    /// <summary>The shell <see cref="XamlRoot"/> for shell-owned dialogs.</summary>
    XamlRoot? XamlRoot { get; }
}
