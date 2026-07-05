using System;
using System.Threading.Tasks;
using LabAssistant.Business.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates;

/// <summary>
/// Default <see cref="ITemplateEditorHandoff"/> mediator. Registered as a DI singleton so the
/// Templates runtime (created later, in the shell) can register its editor handler while Deploy
/// resolves the same instance to request editor handoffs. This removes the Templates-specific
/// method that previously sat on the otherwise capability-agnostic shell host seam.
/// </summary>
internal sealed class TemplateEditorHandoff : ITemplateEditorHandoff
{
    private Func<TemplateEditorDocument, string, Task>? _handler;

    /// <inheritdoc />
    public Task ShowInEditorAsync(TemplateEditorDocument document, string statusText) =>
        _handler?.Invoke(document, statusText) ?? Task.CompletedTask;

    /// <inheritdoc />
    public void SetHandler(Func<TemplateEditorDocument, string, Task>? handler) => _handler = handler;
}
