using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;

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

    public IReadOnlyList<TemplatesBuilderLabNetworkDraft> LabNetworks { get; private set; } = [];

    public IReadOnlyList<TemplatesBuilderCredentialSlotDraft> CredentialSlots { get; private set; } = [];

    public IReadOnlyList<TemplatesBuilderForestDraft> Forests { get; private set; } = [];

    public IReadOnlyList<TemplatesBuilderDomainDraft> Domains { get; private set; } = [];

    public IReadOnlyList<TemplatesBuilderVmDraft> Vms { get; private set; } = [];

    public bool IsSaveConfirmed { get; private set; }

    public bool HasActiveDraft { get; private set; }

    public bool HasStatusText { get; private set; }

    public IReadOnlyList<V2TrustTemplate> PreservedTrusts { get; private set; } = Array.Empty<V2TrustTemplate>();

    public TemplatesBuilderValidationState ValidationState { get; private set; } = TemplatesBuilderValidationState.Empty;

    public bool HasValidationBlockers => ValidationState.HasBlockers;

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
        PreservedTrusts = Array.Empty<V2TrustTemplate>();
        ApplyDraft(draft with { IsSaveConfirmed = false }, TemplatesBuilderValidationRequest.All());
        HasActiveDraft = true;
        ContextText = "Editing new V2 Builder draft.";
        SetStatus("Review the suggested topology, then save.");
    }

    public void LoadDocument(TemplateEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        TemplateId = document.Template.Id;
        TemplateRevision = document.Template.TemplateRevision;
        CreatedWithAppVersion = document.Template.CreatedWithAppVersion;
        SourceFilePath = document.SourceFilePath;
        PreservedTrusts = CopyTrusts(document.Template.DirectoryTopology?.Trusts);
        ApplyDraft(TemplatesBuilderDraftMapper.FromTemplate(document.Template), TemplatesBuilderValidationRequest.All());
        HasActiveDraft = true;
        ContextText = string.IsNullOrWhiteSpace(SourceFilePath)
            ? "Editing V2 Builder draft."
            : $"Editing V2 template: {SourceFilePath}";
        SetStatus("V2 template loaded in Builder.");
    }

    public void ApplyDraft(TemplatesBuilderDraftSnapshot draft)
        => ApplyDraft(draft, TemplatesBuilderValidationRequest.DetectChangedScopes(CaptureDraft(), draft));

    public void ApplyDraft(TemplatesBuilderDraftSnapshot draft, TemplatesBuilderValidationRequest validationRequest)
    {
        var editableContentChanged =
            !string.Equals(TemplateName, draft.TemplateName, StringComparison.Ordinal) ||
            !string.Equals(TemplateDescription, draft.TemplateDescription, StringComparison.Ordinal) ||
            !string.Equals(DeploymentProfile, draft.DeploymentProfile, StringComparison.Ordinal) ||
            !LabNetworks.SequenceEqual(draft.LabNetworks) ||
            !CredentialSlots.SequenceEqual(draft.CredentialSlots) ||
            !Forests.SequenceEqual(draft.Forests) ||
            !Domains.SequenceEqual(draft.Domains) ||
            !Vms.SequenceEqual(draft.Vms);

        TemplateName = draft.TemplateName;
        TemplateDescription = draft.TemplateDescription;
        DeploymentProfile = draft.DeploymentProfile;
        LabNetworks = draft.LabNetworks.ToList();
        CredentialSlots = draft.CredentialSlots.ToList();
        Forests = draft.Forests.ToList();
        Domains = draft.Domains.ToList();
        Vms = draft.Vms.ToList();
        IsSaveConfirmed = editableContentChanged && IsSaveConfirmed
            ? false
            : draft.IsSaveConfirmed;
        if (validationRequest.Categories.Count > 0)
        {
            ValidationState = MergeValidationState(
                ValidationState,
                TemplatesBuilderDraftValidator.Validate(CaptureDraft(), validationRequest));
        }
    }

    public TemplatesBuilderDraftSnapshot CaptureDraft()
    {
        return new TemplatesBuilderDraftSnapshot(
            TemplateName,
            TemplateDescription,
            DeploymentProfile,
            LabNetworks,
            CredentialSlots,
            Forests,
            Domains,
            Vms,
            IsSaveConfirmed);
    }

    public void SetSavedDocument(TemplateEditorDocument document, string filePath)
    {
        SourceFilePath = filePath;
        TemplateId = document.Template.Id;
        TemplateRevision = document.Template.TemplateRevision;
        CreatedWithAppVersion = document.Template.CreatedWithAppVersion;
        PreservedTrusts = CopyTrusts(document.Template.DirectoryTopology?.Trusts);
        IsSaveConfirmed = false;
        ContextText = $"Editing V2 template: {filePath}";
    }

    public void SetStatus(string statusText)
    {
        StatusText = statusText;
        HasStatusText = !string.IsNullOrWhiteSpace(statusText);
    }

    private static IReadOnlyList<V2TrustTemplate> CopyTrusts(IEnumerable<V2TrustTemplate>? trusts)
    {
        if (trusts is null)
        {
            return Array.Empty<V2TrustTemplate>();
        }

        return trusts
            .Select(trust => new V2TrustTemplate
            {
                TrustId = trust.TrustId,
                SourceDomainId = trust.SourceDomainId,
                TargetDomainId = trust.TargetDomainId,
                TrustType = trust.TrustType,
                Direction = trust.Direction
            })
            .ToList();
    }

    private static TemplatesBuilderValidationState MergeValidationState(
        TemplatesBuilderValidationState existing,
        TemplatesBuilderValidationState updated)
    {
        var refreshedCategories = updated.EvaluatedCategories;
        var blockers = existing.Blockers
            .Where(issue => !refreshedCategories.Contains(issue.Category))
            .Concat(updated.Blockers)
            .ToList();
        var warnings = existing.Warnings
            .Where(issue => !refreshedCategories.Contains(issue.Category))
            .Concat(updated.Warnings)
            .ToList();
        var categories = existing.EvaluatedCategories
            .Concat(updated.EvaluatedCategories)
            .ToHashSet();

        return new TemplatesBuilderValidationState(blockers, warnings, categories);
    }
}
