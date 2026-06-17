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
    IReadOnlyList<TemplatesBuilderLabNetworkDraft> LabNetworks,
    IReadOnlyList<TemplatesBuilderCredentialSlotDraft> CredentialSlots,
    IReadOnlyList<TemplatesBuilderForestDraft> Forests,
    IReadOnlyList<TemplatesBuilderDomainDraft> Domains,
    IReadOnlyList<TemplatesBuilderVmDraft> Vms,
    bool IsSaveConfirmed);

internal readonly record struct TemplatesBuilderLabNetworkDraft(
    string NetworkId,
    string Name,
    string SwitchName,
    string Subnet,
    string Notes);

internal readonly record struct TemplatesBuilderCredentialSlotDraft(
    string SlotKey,
    string Label,
    string ScopeHint);

internal readonly record struct TemplatesBuilderForestDraft(
    string ForestId,
    string RootDomainId);

internal readonly record struct TemplatesBuilderDomainDraft(
    string DomainId,
    string DnsName,
    string NetBiosName,
    string ForestId,
    string RelationKind,
    string ParentDomainId);

internal readonly record struct TemplatesBuilderVmCredentialSlotDraft(
    string LocalBootstrap,
    string DomainAdmin,
    string DomainJoin,
    string Dsrm,
    string ParentDomainAdmin);

internal readonly record struct TemplatesBuilderVmDraft(
    string VmId,
    string Name,
    string MemoryMb,
    string CpuCount,
    string VhdxId,
    string MembershipMode,
    string DomainId,
    bool IsActiveDirectoryDomainController,
    TemplatesBuilderVmCredentialSlotDraft CredentialSlots,
    IReadOnlyList<TemplatesBuilderNicDraft> Nics);

internal readonly record struct TemplatesBuilderNicDraft(
    string NicId,
    string Name,
    string NetworkId,
    string SwitchName,
    string IpAddress,
    string PrefixLength,
    string DefaultGateway,
    IReadOnlyList<string> DnsServers);

internal sealed class TemplatesBuilderDraftBuildResult
{
    public TemplateEditorDocument? Document { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
