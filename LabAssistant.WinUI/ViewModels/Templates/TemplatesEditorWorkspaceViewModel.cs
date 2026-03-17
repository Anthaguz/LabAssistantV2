using LabAssistant.Business.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates;

internal sealed class TemplatesEditorWorkspaceViewModel
{
    public string TemplateName { get; private set; } = string.Empty;

    public string TemplateDescription { get; private set; } = string.Empty;

    public string TemplateEditorContextText { get; private set; } = "No template selected.";

    public string TemplateIdText { get; private set; } = "Template ID: -";

    public string TemplateFilePathText { get; private set; } = "File path: new template (not saved)";

    public string TemplateVmCountText { get; private set; } = "VMs: 0";

    public string StatusText { get; private set; } = "No template loaded.";

    public bool HasStatusText { get; private set; }

    public bool HasActiveDocument { get; private set; }

    public void ClearDocument()
    {
        HasActiveDocument = false;
        TemplateName = string.Empty;
        TemplateDescription = string.Empty;
        TemplateEditorContextText = "No template selected.";
        TemplateIdText = "Template ID: -";
        TemplateFilePathText = "File path: new template (not saved)";
        TemplateVmCountText = "VMs: 0";
    }

    public void SetDocument(TemplateEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        HasActiveDocument = true;
        TemplateName = document.Template.Name ?? string.Empty;
        TemplateDescription = document.Template.Description ?? string.Empty;
        TemplateEditorContextText = string.IsNullOrWhiteSpace(document.SourceFilePath)
            ? "Editing new template draft."
            : "Editing existing template.";
        TemplateIdText = $"Template ID: {document.Template.Id}";
        TemplateFilePathText = $"File path: {document.SourceFilePath ?? "new template (not saved)"}";
        TemplateVmCountText = $"VMs: {document.Template.VmTemplates.Count}";
    }

    public void SetDocumentHeaderDraft(string? templateName, string? templateDescription)
    {
        TemplateName = templateName ?? string.Empty;
        TemplateDescription = templateDescription ?? string.Empty;
    }

    public void SetVmCount(int vmCount)
    {
        TemplateVmCountText = $"VMs: {vmCount}";
    }

    public void SetStatusText(string statusText)
    {
        StatusText = statusText ?? string.Empty;
        HasStatusText = !string.IsNullOrWhiteSpace(StatusText);
    }
}
