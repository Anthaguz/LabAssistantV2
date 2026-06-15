using LabAssistant.Business.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

internal sealed class TemplatesBuilderWorkspaceViewModel
{
    public string TemplateId { get; private set; } = Guid.NewGuid().ToString("N");

    public int TemplateRevision { get; private set; } = 1;

    public string CreatedWithAppVersion { get; private set; } = "1.0.0";

    public string? SourceFilePath { get; private set; }

    public string TemplateName { get; private set; } = string.Empty;

    public string TemplateDescription { get; private set; } = string.Empty;

    public string DeploymentProfile { get; private set; } = "Balanced";

    public string LabNetworksText { get; private set; } = string.Empty;

    public string ForestsText { get; private set; } = string.Empty;

    public string DomainsText { get; private set; } = string.Empty;

    public string VmsText { get; private set; } = string.Empty;

    public string NicsText { get; private set; } = string.Empty;

    public bool IsSaveConfirmed { get; private set; }

    public bool HasActiveDraft { get; private set; }

    public bool HasStatusText { get; private set; }

    public string ContextText { get; private set; } = "No Builder draft loaded.";

    public string StatusText { get; private set; } = "Create or open a V2 Builder draft.";

    public string ReferenceText { get; private set; } = "No reference data loaded.";

    public void SetReferenceData(TemplatesBuilderReferenceData referenceData)
    {
        var switchText = referenceData.AvailableVmSwitches.Count == 0
            ? "switches: none loaded"
            : $"switches: {string.Join(", ", referenceData.AvailableVmSwitches)}";
        var diskText = referenceData.VhdxCatalogOptions.Count == 0
            ? "catalog disks: none loaded"
            : $"catalog disks: {string.Join(", ", referenceData.VhdxCatalogOptions.Select(option => option.Id))}";
        ReferenceText = $"{switchText}; {diskText}";
    }

    public void LoadNewDraft(TemplatesBuilderDraftSnapshot draft)
    {
        TemplateId = Guid.NewGuid().ToString("N");
        TemplateRevision = 1;
        CreatedWithAppVersion = "1.0.0";
        SourceFilePath = null;
        ApplyDraft(draft with { IsSaveConfirmed = false });
        HasActiveDraft = true;
        ContextText = "Editing new V2 Builder draft.";
        SetStatus("Review the suggested topology, confirm intent, then save.");
    }

    public void LoadDocument(TemplateEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        TemplateId = document.Template.Id;
        TemplateRevision = document.Template.TemplateRevision;
        CreatedWithAppVersion = document.Template.CreatedWithAppVersion;
        SourceFilePath = document.SourceFilePath;
        ApplyDraft(TemplatesBuilderDraftMapper.FromTemplate(document.Template));
        HasActiveDraft = true;
        ContextText = string.IsNullOrWhiteSpace(SourceFilePath)
            ? "Editing V2 Builder draft."
            : $"Editing V2 template: {SourceFilePath}";
        SetStatus("V2 template loaded in Builder.");
    }

    public void ApplyDraft(TemplatesBuilderDraftSnapshot draft)
    {
        TemplateName = draft.TemplateName;
        TemplateDescription = draft.TemplateDescription;
        DeploymentProfile = draft.DeploymentProfile;
        LabNetworksText = draft.LabNetworksText;
        ForestsText = draft.ForestsText;
        DomainsText = draft.DomainsText;
        VmsText = draft.VmsText;
        NicsText = draft.NicsText;
        IsSaveConfirmed = draft.IsSaveConfirmed;
    }

    public TemplatesBuilderDraftSnapshot CaptureDraft()
    {
        return new TemplatesBuilderDraftSnapshot(
            TemplateName,
            TemplateDescription,
            DeploymentProfile,
            LabNetworksText,
            ForestsText,
            DomainsText,
            VmsText,
            NicsText,
            IsSaveConfirmed);
    }

    public void SetSavedDocument(TemplateEditorDocument document, string filePath)
    {
        SourceFilePath = filePath;
        TemplateId = document.Template.Id;
        TemplateRevision = document.Template.TemplateRevision;
        CreatedWithAppVersion = document.Template.CreatedWithAppVersion;
        IsSaveConfirmed = false;
        ContextText = $"Editing V2 template: {filePath}";
    }

    public void SetStatus(string statusText)
    {
        StatusText = statusText;
        HasStatusText = !string.IsNullOrWhiteSpace(statusText);
    }
}

