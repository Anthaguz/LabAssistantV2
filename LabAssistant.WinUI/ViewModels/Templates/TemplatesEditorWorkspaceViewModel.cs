using LabAssistant.Business.Templates;
using System.Collections.ObjectModel;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates;

internal sealed class TemplatesEditorWorkspaceViewModel
{
    public ObservableCollection<VmTemplate> VmEntries { get; } = [];

    public string TemplateName { get; private set; } = string.Empty;

    public string TemplateDescription { get; private set; } = string.Empty;

    public string TemplateEditorContextText { get; private set; } = "No template selected.";

    public string TemplateIdText { get; private set; } = "Template ID: -";

    public string TemplateFilePathText { get; private set; } = "File path: new template (not saved)";

    public string TemplateVmCountText { get; private set; } = "VMs: 0";

    public string StatusText { get; private set; } = "No template loaded.";

    public bool HasStatusText { get; private set; }

    public bool HasActiveDocument { get; private set; }

    public VmTemplate? SelectedVmEntry { get; private set; }

    public string? SelectedVmId { get; private set; }

    public void ClearDocument()
    {
        HasActiveDocument = false;
        VmEntries.Clear();
        SelectedVmEntry = null;
        SelectedVmId = null;
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

    public void ReplaceVmEntries(IReadOnlyList<VmTemplate> vmEntries)
    {
        var selectedVmId = SelectedVmId;
        var selectedVmReference = SelectedVmEntry;

        VmEntries.Clear();
        foreach (var vmEntry in vmEntries)
        {
            VmEntries.Add(vmEntry);
        }

        var reconciledSelection = ResolveSelection(vmEntries, selectedVmId, selectedVmReference);
        SetSelectedVmEntry(reconciledSelection);
    }

    public void AddVmEntry(VmTemplate vmEntry)
    {
        ArgumentNullException.ThrowIfNull(vmEntry);

        VmEntries.Add(vmEntry);
        SetSelectedVmEntry(vmEntry);
    }

    public VmTemplate? RemoveSelectedVmEntry()
    {
        var removedEntry = SelectedVmEntry;
        if (removedEntry is null)
        {
            return null;
        }

        var selectedIndex = VmEntries.IndexOf(removedEntry);
        VmEntries.Remove(removedEntry);

        VmTemplate? nextSelection = null;
        if (VmEntries.Count > 0)
        {
            var nextIndex = Math.Clamp(selectedIndex, 0, VmEntries.Count - 1);
            nextSelection = VmEntries[nextIndex];
        }

        SetSelectedVmEntry(nextSelection);
        return removedEntry;
    }

    public void SetSelectedVmEntry(VmTemplate? selectedVmEntry)
    {
        SelectedVmEntry = selectedVmEntry;
        SelectedVmId = string.IsNullOrWhiteSpace(selectedVmEntry?.VmId)
            ? null
            : selectedVmEntry.VmId;
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

    private static VmTemplate? ResolveSelection(
        IReadOnlyList<VmTemplate> vmEntries,
        string? selectedVmId,
        VmTemplate? selectedVmReference)
    {
        if (!string.IsNullOrWhiteSpace(selectedVmId))
        {
            var idMatch = vmEntries.FirstOrDefault(vmEntry =>
                string.Equals(vmEntry.VmId, selectedVmId, StringComparison.Ordinal));
            if (idMatch is not null)
            {
                return idMatch;
            }
        }

        if (selectedVmReference is not null)
        {
            var referenceMatch = vmEntries.FirstOrDefault(vmEntry => ReferenceEquals(vmEntry, selectedVmReference));
            if (referenceMatch is not null)
            {
                return referenceMatch;
            }
        }

        return vmEntries.FirstOrDefault();
    }
}
