using LabAssistant.Business.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Opens Deploy-created template snapshots in the Templates editor through the existing Templates workspace.
/// </summary>
internal sealed class DeployTemplateEditorLauncher
{
    private readonly DeployTemplatesShellAdapter _templatesShellAdapter;

    public DeployTemplateEditorLauncher(DeployTemplatesShellAdapter templatesShellAdapter)
    {
        _templatesShellAdapter = templatesShellAdapter;
    }

    public Task ShowEditorAsync(TemplateEditorDocument document, string statusText) =>
        _templatesShellAdapter.ShowTemplateEditorAsync(document, statusText);
}
