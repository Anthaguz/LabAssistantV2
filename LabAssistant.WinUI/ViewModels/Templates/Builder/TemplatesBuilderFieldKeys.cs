namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

internal enum BuilderDraftFieldScope
{
    Network,
    CredentialSlot,
    Forest,
    Domain,
    Vm,
    Nic
}

internal readonly record struct BuilderDraftFieldKey(
    BuilderDraftFieldScope Scope,
    string Name);

internal static class TemplatesBuilderFieldKeys
{
    public static readonly BuilderDraftFieldKey NetworkId = new(BuilderDraftFieldScope.Network, nameof(TemplatesBuilderLabNetworkDraft.NetworkId));
    public static readonly BuilderDraftFieldKey NetworkName = new(BuilderDraftFieldScope.Network, nameof(TemplatesBuilderLabNetworkDraft.Name));
    public static readonly BuilderDraftFieldKey NetworkSwitchName = new(BuilderDraftFieldScope.Network, nameof(TemplatesBuilderLabNetworkDraft.SwitchName));
    public static readonly BuilderDraftFieldKey NetworkSwitchType = new(BuilderDraftFieldScope.Network, nameof(TemplatesBuilderLabNetworkDraft.SwitchType));
    public static readonly BuilderDraftFieldKey NetworkSubnet = new(BuilderDraftFieldScope.Network, nameof(TemplatesBuilderLabNetworkDraft.Subnet));
    public static readonly BuilderDraftFieldKey NetworkNotes = new(BuilderDraftFieldScope.Network, nameof(TemplatesBuilderLabNetworkDraft.Notes));

    public static readonly BuilderDraftFieldKey CredentialSlotKey = new(BuilderDraftFieldScope.CredentialSlot, nameof(TemplatesBuilderCredentialSlotDraft.SlotKey));
    public static readonly BuilderDraftFieldKey CredentialSlotLabel = new(BuilderDraftFieldScope.CredentialSlot, nameof(TemplatesBuilderCredentialSlotDraft.Label));
    public static readonly BuilderDraftFieldKey CredentialSlotScopeHint = new(BuilderDraftFieldScope.CredentialSlot, nameof(TemplatesBuilderCredentialSlotDraft.ScopeHint));

    public static readonly BuilderDraftFieldKey ForestId = new(BuilderDraftFieldScope.Forest, nameof(TemplatesBuilderForestDraft.ForestId));
    public static readonly BuilderDraftFieldKey ForestRootDomainId = new(BuilderDraftFieldScope.Forest, nameof(TemplatesBuilderForestDraft.RootDomainId));

    public static readonly BuilderDraftFieldKey DomainId = new(BuilderDraftFieldScope.Domain, nameof(TemplatesBuilderDomainDraft.DomainId));
    public static readonly BuilderDraftFieldKey DomainDnsName = new(BuilderDraftFieldScope.Domain, nameof(TemplatesBuilderDomainDraft.DnsName));
    public static readonly BuilderDraftFieldKey DomainNetBiosName = new(BuilderDraftFieldScope.Domain, nameof(TemplatesBuilderDomainDraft.NetBiosName));
    public static readonly BuilderDraftFieldKey DomainForestId = new(BuilderDraftFieldScope.Domain, nameof(TemplatesBuilderDomainDraft.ForestId));
    public static readonly BuilderDraftFieldKey DomainRelationKind = new(BuilderDraftFieldScope.Domain, nameof(TemplatesBuilderDomainDraft.RelationKind));
    public static readonly BuilderDraftFieldKey DomainParentDomainId = new(BuilderDraftFieldScope.Domain, nameof(TemplatesBuilderDomainDraft.ParentDomainId));

    public static readonly BuilderDraftFieldKey VmId = new(BuilderDraftFieldScope.Vm, nameof(TemplatesBuilderVmDraft.VmId));
    public static readonly BuilderDraftFieldKey VmName = new(BuilderDraftFieldScope.Vm, nameof(TemplatesBuilderVmDraft.Name));
    public static readonly BuilderDraftFieldKey VmMemoryMb = new(BuilderDraftFieldScope.Vm, nameof(TemplatesBuilderVmDraft.MemoryMb));
    public static readonly BuilderDraftFieldKey VmCpuCount = new(BuilderDraftFieldScope.Vm, nameof(TemplatesBuilderVmDraft.CpuCount));
    public static readonly BuilderDraftFieldKey VmVhdxId = new(BuilderDraftFieldScope.Vm, nameof(TemplatesBuilderVmDraft.VhdxId));
    public static readonly BuilderDraftFieldKey VmMembershipMode = new(BuilderDraftFieldScope.Vm, nameof(TemplatesBuilderVmDraft.MembershipMode));
    public static readonly BuilderDraftFieldKey VmDomainId = new(BuilderDraftFieldScope.Vm, nameof(TemplatesBuilderVmDraft.DomainId));
    public static readonly BuilderDraftFieldKey VmIsActiveDirectoryDomainController = new(BuilderDraftFieldScope.Vm, nameof(TemplatesBuilderVmDraft.IsActiveDirectoryDomainController));
    public static readonly BuilderDraftFieldKey VmLocalBootstrap = new(BuilderDraftFieldScope.Vm, nameof(TemplatesBuilderVmCredentialSlotDraft.LocalBootstrap));
    public static readonly BuilderDraftFieldKey VmDomainAdmin = new(BuilderDraftFieldScope.Vm, nameof(TemplatesBuilderVmCredentialSlotDraft.DomainAdmin));
    public static readonly BuilderDraftFieldKey VmDomainJoin = new(BuilderDraftFieldScope.Vm, nameof(TemplatesBuilderVmCredentialSlotDraft.DomainJoin));
    public static readonly BuilderDraftFieldKey VmDsrm = new(BuilderDraftFieldScope.Vm, nameof(TemplatesBuilderVmCredentialSlotDraft.Dsrm));
    public static readonly BuilderDraftFieldKey VmParentDomainAdmin = new(BuilderDraftFieldScope.Vm, nameof(TemplatesBuilderVmCredentialSlotDraft.ParentDomainAdmin));
    public static readonly BuilderDraftFieldKey VmNicsPanel = new(BuilderDraftFieldScope.Vm, nameof(TemplatesBuilderVmDraft.Nics));

    public static readonly BuilderDraftFieldKey NicRow = new(BuilderDraftFieldScope.Nic, "Row");
    public static readonly BuilderDraftFieldKey NicId = new(BuilderDraftFieldScope.Nic, nameof(TemplatesBuilderNicDraft.NicId));
    public static readonly BuilderDraftFieldKey NicName = new(BuilderDraftFieldScope.Nic, nameof(TemplatesBuilderNicDraft.Name));
    public static readonly BuilderDraftFieldKey NicNetworkId = new(BuilderDraftFieldScope.Nic, nameof(TemplatesBuilderNicDraft.NetworkId));
    public static readonly BuilderDraftFieldKey NicSwitchName = new(BuilderDraftFieldScope.Nic, nameof(TemplatesBuilderNicDraft.SwitchName));
    public static readonly BuilderDraftFieldKey NicIpAddress = new(BuilderDraftFieldScope.Nic, nameof(TemplatesBuilderNicDraft.IpAddress));
    public static readonly BuilderDraftFieldKey NicPrefixLength = new(BuilderDraftFieldScope.Nic, nameof(TemplatesBuilderNicDraft.PrefixLength));
    public static readonly BuilderDraftFieldKey NicDefaultGateway = new(BuilderDraftFieldScope.Nic, nameof(TemplatesBuilderNicDraft.DefaultGateway));
    public static readonly BuilderDraftFieldKey NicDnsServers = new(BuilderDraftFieldScope.Nic, nameof(TemplatesBuilderNicDraft.DnsServers));
}
