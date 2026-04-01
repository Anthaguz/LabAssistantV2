using LabAssistant.Business.Templates;
using LabAssistant.WinUI.ViewModels.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Opens Deploy-created template snapshots in the Templates editor through the existing Templates workspace.
/// </summary>
internal sealed class DeployTemplateEditorLauncher
{
    private readonly TemplatesWorkspaceComposition _templatesWorkspaceComposition;

    public DeployTemplateEditorLauncher(TemplatesWorkspaceComposition templatesWorkspaceComposition)
    {
        _templatesWorkspaceComposition = templatesWorkspaceComposition;
    }

    public Task ShowEditorAsync(TemplateEditorDocument document, string statusText) =>
        _templatesWorkspaceComposition.ShowEditorDocumentAsync(document, statusText);
}
