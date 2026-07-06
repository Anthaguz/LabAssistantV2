using System.Threading.Tasks;
using LabAssistant.Business.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates;

/// <summary>
/// Cross-subview seam the Templates Library view model uses to reach sibling subviews and shell
/// affordances it does not own: opening a document in the Editor or Builder, creating a Builder
/// draft, reporting Editor status on a failed hand-off, and confirming a template delete. The
/// hosting <c>TemplatesPage</c> implements it, so the Library view model stays free of navigation
/// and dialog concerns and remains unit-testable.
/// </summary>
internal interface ITemplatesLibraryHost
{
    /// <summary>Loads <paramref name="document"/> into the Editor subview and routes to it.</summary>
    Task ShowTemplateInEditorAsync(TemplateEditorDocument document, string statusText);

    /// <summary>
    /// Reports a status message onto the Editor subview without routing to it. Preserves the legacy
    /// behavior where a failed "open in editor" surfaces its error on the editor status line.
    /// </summary>
    void ReportEditorStatus(string statusText);

    /// <summary>Loads <paramref name="document"/> into the Builder subview and routes to it.</summary>
    Task ShowTemplateInBuilderAsync(TemplateEditorDocument document);

    /// <summary>Creates a fresh Builder draft and routes to the Builder subview.</summary>
    Task CreateTemplateBuilderDraftAsync();

    /// <summary>Shows the delete confirmation dialog for <paramref name="templateItem"/>.</summary>
    Task<bool> ConfirmDeleteTemplateAsync(TemplateLibraryItem templateItem);
}
