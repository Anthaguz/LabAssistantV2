using LabAssistant.Business.Templates;
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

    /// <summary>
    /// The shell right panel a capability page drives to host progress/results/details content.
    /// A page pushes its own content element, title, and auto-open requests; the shell owns
    /// visibility, width, compact fallback, and the user open/close toggle.
    /// </summary>
    IShellRightPanel RightPanel { get; }

    /// <summary>
    /// Shell-owned dialog and file-picker service. Capability pages that need native file dialogs
    /// (which require the shell window handle) or shared confirmation dialogs use this instead of
    /// reaching back into the shell window directly.
    /// </summary>
    ShellDialogService Dialogs { get; }

    /// <summary>
    /// Cross-capability handoff: opens <paramref name="document"/> in the Templates editor and
    /// navigates to it. Deploy's "edit template" affordance uses this so it can hand a resolved
    /// template document to the Templates capability without owning Templates composition. The
    /// shell backs it with the live Templates runtime.
    /// </summary>
    System.Threading.Tasks.Task ShowTemplateInEditorAsync(TemplateEditorDocument document, string statusText);
}
