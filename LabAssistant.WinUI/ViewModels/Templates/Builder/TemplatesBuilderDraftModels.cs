using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

internal readonly record struct TemplatesBuilderReferenceData(
    IReadOnlyList<string> AvailableVmSwitches,
    IReadOnlyList<TemplateVhdxCatalogOption> VhdxCatalogOptions,
    IReadOnlyList<V2AvailableSwitchInfo> AvailableSwitchInventory)
{
    public TemplatesBuilderReferenceData(
        IReadOnlyList<string> availableVmSwitches,
        IReadOnlyList<TemplateVhdxCatalogOption> vhdxCatalogOptions)
        : this(
            availableVmSwitches,
            vhdxCatalogOptions,
            BuildUnknownSwitchInventory(availableVmSwitches))
    {
    }

    private static IReadOnlyList<V2AvailableSwitchInfo> BuildUnknownSwitchInventory(IReadOnlyList<string>? switchNames)
        => switchNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new V2AvailableSwitchInfo { Name = name.Trim(), SwitchType = "Unknown" })
            .ToList() ?? [];
}

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
    string SwitchType,
    string Subnet,
    string Notes)
{
    /// <summary>
    /// The domain this switch belongs to under the one-switch-per-domain model. In-memory only (never
    /// persisted): reconstructed on load from the NICs that reference this network. Empty for the single
    /// shared standalone switch that seats standalone machines.
    /// </summary>
    public string DomainId { get; init; }
}

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
    IReadOnlyList<TemplatesBuilderNicDraft> Nics)
{
    /// <summary>
    /// True for the single required router VM the network model auto-creates: a standalone box that carries a
    /// NIC on every switch (holding each switch's .1) and bridges them. Maps to the persisted
    /// <c>TopologyRole = "Router"</c> and is reconstructed from it on load.
    /// </summary>
    public bool IsRouter { get; init; }

    /// <summary>
    /// Non-structural Windows roles enabled on this VM (DNS, DHCP, File Server, ADCS, ...) by catalog key.
    /// The structural directory role (domain controller / router) is NOT stored here: it stays on
    /// <see cref="IsActiveDirectoryDomainController"/> / <see cref="IsRouter"/> and the persisted
    /// <c>TopologyRole</c>, which the deploy pipeline depends on. Null or empty means none. This is additive
    /// authoring state introduced with the Level 2 machine inspector; it does not affect the deploy pipeline.
    /// </summary>
    public IReadOnlyList<string>? AdditionalRoles { get; init; }
}

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
