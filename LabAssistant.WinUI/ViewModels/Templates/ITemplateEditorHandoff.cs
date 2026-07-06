using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using LabAssistant.Business.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates;

/// <summary>
/// DI-resolved cross-capability handoff for opening a template document in the Templates editor.
/// Deploy's "edit template" affordance depends on this instead of the shell host, so
/// <see cref="LabAssistant.WinUI.Shell.IShellHost"/> stays capability-agnostic.
/// </summary>
/// <remarks>
/// The Templates capability page is transient (created on entry, torn down on leave), so this
/// app-lifetime singleton acts as a pending-document mailbox plus a navigation trigger:
/// <list type="bullet">
///   <item><see cref="ShowInEditorAsync"/> (called by Deploy) stores the pending document and asks
///   the shell to route to the Templates editor.</item>
///   <item>The shell registers a one-time navigator via <see cref="SetNavigator"/>.</item>
///   <item>The Templates page drains the mailbox via <see cref="TryTakePendingDocument"/> when it is
///   navigated to, so a document handed off while no page is alive is shown on the next entry.</item>
/// </list>
/// </remarks>
internal interface ITemplateEditorHandoff
{
    /// <summary>
    /// Stores <paramref name="document"/> as the pending editor document and invokes the registered
    /// navigator to route to the Templates editor. Completes without navigating when no navigator has
    /// been registered yet; the document still remains pending for the next page entry.
    /// </summary>
    Task ShowInEditorAsync(TemplateEditorDocument document, string statusText);

    /// <summary>
    /// Registers (or clears with <see langword="null"/>) the navigator that routes to the Templates
    /// editor. The shell sets this once for the app lifetime.
    /// </summary>
    void SetNavigator(Action? navigator);

    /// <summary>
    /// Drains any pending editor document. Returns <see langword="true"/> and hands out the pending
    /// <paramref name="document"/>/<paramref name="statusText"/> exactly once; subsequent calls return
    /// <see langword="false"/> until another document is handed off.
    /// </summary>
    bool TryTakePendingDocument([NotNullWhen(true)] out TemplateEditorDocument? document, out string statusText);
}
