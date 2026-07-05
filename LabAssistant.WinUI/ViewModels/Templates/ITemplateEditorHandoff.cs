using System;
using System.Threading.Tasks;
using LabAssistant.Business.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates;

/// <summary>
/// DI-resolved cross-capability handoff for opening a template document in the Templates editor.
/// Deploy's "edit template" affordance depends on this instead of the shell host, so
/// <see cref="LabAssistant.WinUI.Shell.IShellHost"/> stays capability-agnostic. The Templates
/// capability registers the concrete handler via <see cref="SetHandler"/> when its runtime is
/// composed; consumers call <see cref="ShowInEditorAsync"/> without knowing Templates composition.
/// </summary>
internal interface ITemplateEditorHandoff
{
    /// <summary>
    /// Opens <paramref name="document"/> in the Templates editor and navigates to it. Completes
    /// without effect when no handler has been registered yet.
    /// </summary>
    Task ShowInEditorAsync(TemplateEditorDocument document, string statusText);

    /// <summary>
    /// Registers (or clears with <see langword="null"/>) the handler that fulfills
    /// <see cref="ShowInEditorAsync"/>. The Templates runtime owner sets this once it exists.
    /// </summary>
    void SetHandler(Func<TemplateEditorDocument, string, Task>? handler);
}
