using LabAssistant.Business.Templates;
using LabAssistant.WinUI.ViewModels.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

internal readonly record struct TemplatesBuilderReferenceData(
    IReadOnlyList<string> AvailableVmSwitches,
    IReadOnlyList<TemplateVhdxCatalogOption> VhdxCatalogOptions);

internal readonly record struct TemplatesBuilderDraftSnapshot(
    string TemplateName,
    string TemplateDescription,
    string DeploymentProfile,
    string LabNetworksText,
    string ForestsText,
    string DomainsText,
    string VmsText,
    string NicsText,
    bool IsSaveConfirmed);

internal sealed class TemplatesBuilderDraftBuildResult
{
    public TemplateEditorDocument? Document { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
