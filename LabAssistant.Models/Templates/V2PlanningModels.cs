using LabAssistant.Models.Catalog;

namespace LabAssistant.Models.Templates;

public sealed class V2PlanBuildRequest
{
    public LabTemplate Template { get; set; } = new();

    public IReadOnlyList<VhdxCatalogItem> CatalogItems { get; set; } = Array.Empty<VhdxCatalogItem>();

    public IReadOnlyList<string> AvailableSwitchNames { get; set; } = Array.Empty<string>();

    public IReadOnlyCollection<string> ResolvedCredentialSlotKeys { get; set; } = Array.Empty<string>();

    public string? DefaultDeploymentProfile { get; set; }
}

public sealed class V2PlanBuildResult
{
    public bool Success { get; init; }

    public V2ResolvedPlanningContext Context { get; init; } = new();

    public IReadOnlyList<V2PlanNode> Nodes { get; init; } = Array.Empty<V2PlanNode>();

    public IReadOnlyList<V2PlanDependency> Dependencies { get; init; } = Array.Empty<V2PlanDependency>();

    public IReadOnlyList<V2SchedulingWave> Waves { get; init; } = Array.Empty<V2SchedulingWave>();

    public IReadOnlyList<V2PlanIssue> Issues { get; init; } = Array.Empty<V2PlanIssue>();

    public IReadOnlyList<V2UnresolvedRequirement> UnresolvedRequirements { get; init; } = Array.Empty<V2UnresolvedRequirement>();
}

public sealed class V2ResolvedPlanningContext
{
    public TemplateExecutionEngine ExecutionEngine { get; init; }

    public string ResolvedDeploymentProfileName { get; init; } = string.Empty;

    public V2DeploymentProfile ResolvedDeploymentProfile { get; init; }

    public bool RouterSemanticsRequired { get; init; }

    public bool DomainSemanticsRequired { get; init; }

    public IReadOnlyList<V2ResolvedForestPlanningContext> Forests { get; init; } = Array.Empty<V2ResolvedForestPlanningContext>();

    public IReadOnlyList<V2ResolvedDomainPlanningContext> Domains { get; init; } = Array.Empty<V2ResolvedDomainPlanningContext>();

    public IReadOnlyList<V2ResolvedVmPlanningContext> Vms { get; init; } = Array.Empty<V2ResolvedVmPlanningContext>();
}

public sealed class V2ResolvedForestPlanningContext
{
    public string ForestId { get; init; } = string.Empty;

    public string RootDomainId { get; init; } = string.Empty;
}

public sealed class V2ResolvedDomainPlanningContext
{
    public string DomainId { get; init; } = string.Empty;

    public string DnsName { get; init; } = string.Empty;

    public string NetBiosName { get; init; } = string.Empty;

    public string ForestId { get; init; } = string.Empty;

    public V2DomainRelationKind RelationKind { get; init; }

    public string? ParentDomainId { get; init; }

    public string FirstDomainControllerVmId { get; init; } = string.Empty;
}

public sealed class V2ResolvedVmPlanningContext
{
    public string VmId { get; init; } = string.Empty;

    public string VmName { get; init; } = string.Empty;

    public string? TopologyRole { get; init; }

    public string? MembershipMode { get; init; }

    public string? DomainId { get; init; }

    public IReadOnlyList<string> CapabilityRoles { get; init; } = Array.Empty<string>();

    public string? ResolvedCatalogItemId { get; init; }

    public string? ResolvedCatalogPath { get; init; }

    public string? BootstrapProfileRef { get; init; }

    public bool HasBootstrapProfile { get; init; }

    public string? EffectiveBootstrapUser { get; init; }

    public string? EffectiveBootstrapCredentialSlot { get; init; }

    public string? EffectiveDomainAdminCredentialSlot { get; init; }

    public string? EffectiveDomainJoinCredentialSlot { get; init; }

    public string? EffectiveDsrmCredentialSlot { get; init; }

    public bool RequiresGuestWork { get; init; }

    public bool RequiresDomainJoin { get; init; }

    public bool IsRouterCapable { get; init; }

    public IReadOnlyList<V2ResolvedVmNetworkInterface> Nics { get; init; } = Array.Empty<V2ResolvedVmNetworkInterface>();
}

public sealed class V2ResolvedVmNetworkInterface
{
    public string NicId { get; init; } = string.Empty;

    public string? Name { get; init; }

    public string? NetworkId { get; init; }

    public string? EffectiveSwitchName { get; init; }

    public string? IpAddress { get; init; }

    public int? PrefixLength { get; init; }

    public string? DefaultGateway { get; init; }

    public IReadOnlyList<string> DnsServers { get; init; } = Array.Empty<string>();
}

public enum V2PlanNodeKind
{
    ProvisionVm = 0,
    EnableGuestServices = 1,
    StartVm = 2,
    GuestTransportReady = 3,
    PrepareGuestNetwork = 4,
    InstallAdDomainServicesFeature = 5,
    RouterReady = 6,
    DomainReady = 7,
    PromoteRootDomainController = 8,
    PromoteReplicaDomainController = 9,
    ReplicaDomainReady = 10,
    StabilizeDomainDns = 11,
    JoinDomain = 12,
    JoinedDomainReady = 13,
    ApplyCapabilityRole = 14
}

public sealed class V2PlanNode
{
    public string NodeId { get; init; } = string.Empty;

    public string VmId { get; init; } = string.Empty;

    public string VmName { get; init; } = string.Empty;

    public V2PlanNodeKind Kind { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public V2WorkloadClass WorkloadClass { get; init; }

    public V2VmRoleContext RoleContext { get; init; } = new();

    public string? CapabilityRole { get; init; }

    public int WaveHint { get; set; }
}

public sealed class V2VmRoleContext
{
    public string? TopologyRole { get; init; }

    public string? MembershipMode { get; init; }

    public IReadOnlyList<string> CapabilityRoles { get; init; } = Array.Empty<string>();
}

public enum V2PlanDependencyReasonCode
{
    VmLifecycle = 0,
    ExplicitDependsOn = 1,
    RouterRequired = 2,
    DomainRequired = 3,
    RoleOrdering = 4,
    ProfileWavePolicy = 5
}

public sealed class V2PlanDependency
{
    public string FromNodeId { get; init; } = string.Empty;

    public string ToNodeId { get; init; } = string.Empty;

    public V2PlanDependencyReasonCode ReasonCode { get; init; }

    public string Description { get; init; } = string.Empty;

    public bool IsBlockingGate { get; init; }
}

public sealed class V2SchedulingWave
{
    public int WaveNumber { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public IReadOnlyList<string> NodeIds { get; init; } = Array.Empty<string>();

    public string Summary { get; init; } = string.Empty;
}

public enum V2PlanIssueSeverity
{
    Blocking = 0,
    Warning = 1
}

public sealed class V2PlanIssue
{
    public V2PlanIssueSeverity Severity { get; init; }

    public string Code { get; init; } = string.Empty;

    public string? VmId { get; init; }

    public string? VmName { get; init; }

    public string Message { get; init; } = string.Empty;

    public string SuggestedAction { get; init; } = string.Empty;
}

public enum V2UnresolvedRequirementKind
{
    CredentialSlot = 0,
    BootstrapProfile = 1,
    CatalogReference = 2,
    LabNetwork = 3,
    SwitchReference = 4,
    RouterRequirement = 5
}

public sealed class V2UnresolvedRequirement
{
    public V2UnresolvedRequirementKind Kind { get; init; }

    public string Key { get; init; } = string.Empty;

    public IReadOnlyList<string> AffectedVmIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AffectedNodeIds { get; init; } = Array.Empty<string>();

    public string Description { get; init; } = string.Empty;
}
