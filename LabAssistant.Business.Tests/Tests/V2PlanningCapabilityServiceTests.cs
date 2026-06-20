using LabAssistant.Business.Planning;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public sealed class V2PlanningCapabilityServiceTests
{
    private readonly IV2PlanningCapabilityService _service = new V2PlanningCapabilityService();

    [Fact]
    public async Task BuildPlanAsync_ProducesDeterministicGraphAndWaves()
    {
        var request = CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin", "slot-dsrm"]);

        var first = await _service.BuildPlanAsync(request);
        var second = await _service.BuildPlanAsync(request);

        Assert.True(first.Success);
        Assert.Equal(first.Nodes.Select(node => node.NodeId), second.Nodes.Select(node => node.NodeId));
        Assert.Equal(first.Dependencies.Select(dep => $"{dep.FromNodeId}->{dep.ToNodeId}:{dep.ReasonCode}"), second.Dependencies.Select(dep => $"{dep.FromNodeId}->{dep.ToNodeId}:{dep.ReasonCode}"));
        Assert.Equal(first.Waves.Select(wave => $"{wave.WaveNumber}:{string.Join("|", wave.NodeIds)}"), second.Waves.Select(wave => $"{wave.WaveNumber}:{string.Join("|", wave.NodeIds)}"));
    }

    [Fact]
    public async Task BuildPlanAsync_V1Template_ReturnsBlockingResult()
    {
        var template = new LabTemplate
        {
            Id = "legacy-template",
            Name = "Legacy Template",
            SchemaVersion = "1.0.0",
            VmTemplates =
            [
                new VmTemplate
                {
                    VmId = "vm-legacy",
                    Name = "legacy-vm",
                    MemoryMb = 2048,
                    CpuCount = 2,
                    VhdxId = "disk-legacy"
                }
            ]
        };

        var result = await _service.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = template,
            CatalogItems =
            [
                CreateCatalogItem("disk-legacy", "slot-local")
            ],
            DefaultDeploymentProfile = "Balanced"
        });

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "v2-template-required" && issue.Severity == V2PlanIssueSeverity.Blocking);
    }

    [Fact]
    public async Task BuildPlanAsync_BlankDeploymentProfile_UsesDefaultDeploymentProfile()
    {
        var request = CreateAdCoreRequest(null, ["slot-local", "slot-join", "slot-admin", "slot-dsrm"]);
        request.DefaultDeploymentProfile = "Aggressive";

        var result = await _service.BuildPlanAsync(request);

        Assert.True(result.Success);
        Assert.Equal("Aggressive", result.Context.ResolvedDeploymentProfileName);
        Assert.Equal(V2DeploymentProfile.Aggressive, result.Context.ResolvedDeploymentProfile);
    }

    [Fact]
    public async Task BuildPlanAsync_DifferentProfiles_ChangeWavesWithoutChangingDependencies()
    {
        var balanced = await _service.BuildPlanAsync(CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin", "slot-dsrm"]));
        var aggressive = await _service.BuildPlanAsync(CreateAdCoreRequest("Aggressive", ["slot-local", "slot-join", "slot-admin", "slot-dsrm"]));

        Assert.Equal(
            balanced.Dependencies.Select(dep => $"{dep.FromNodeId}->{dep.ToNodeId}:{dep.ReasonCode}"),
            aggressive.Dependencies.Select(dep => $"{dep.FromNodeId}->{dep.ToNodeId}:{dep.ReasonCode}"));
        Assert.NotEqual(
            balanced.Waves.Select(wave => $"{wave.WaveNumber}:{string.Join("|", wave.NodeIds)}"),
            aggressive.Waves.Select(wave => $"{wave.WaveNumber}:{string.Join("|", wave.NodeIds)}"));
    }

    [Fact]
    public async Task BuildPlanAsync_UnresolvedBootstrapCredentialSlot_IsProjected()
    {
        var result = await _service.BuildPlanAsync(CreateAdCoreRequest("Balanced", ["slot-join", "slot-admin", "slot-dsrm"]));

        Assert.False(result.Success);
        Assert.Contains(result.UnresolvedRequirements, requirement => requirement.Kind == V2UnresolvedRequirementKind.CredentialSlot && requirement.Key == "slot-local");
        Assert.Contains(result.Issues, issue => issue.Code == "credential-slot-unresolved" && issue.VmName == "dc01");
    }

    [Fact]
    public async Task BuildPlanAsync_RouterFreeTemplate_EmitsNoRouterNodes()
    {
        var result = await _service.BuildPlanAsync(CreateSingleVmStandaloneRequest());

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Nodes, node => node.Kind == V2PlanNodeKind.RouterReady);
        Assert.False(result.Context.RouterSemanticsRequired);
    }

    [Fact]
    public async Task BuildPlanAsync_CrossSwitchPlan_AddsRouterDependency()
    {
        var result = await _service.BuildPlanAsync(CreateCrossSwitchRequest(includeRouter: true));

        Assert.True(result.Context.RouterSemanticsRequired);
        var routerNode = Assert.Single(result.Nodes.Where(node => node.Kind == V2PlanNodeKind.RouterReady));
        Assert.Contains(result.Nodes, node => node.Kind == V2PlanNodeKind.PrepareRouterNetwork);
        Assert.Contains(result.Nodes, node => node.Kind == V2PlanNodeKind.ConfigureRouterNat);
        Assert.Contains(result.Nodes, node => node.Kind == V2PlanNodeKind.ValidateCrossSwitchRouting);
        Assert.Contains(result.Nodes, node => node.Kind == V2PlanNodeKind.ValidateRouterEgress);
        Assert.Contains(result.Dependencies, dep => dep.FromNodeId == routerNode.NodeId && dep.ReasonCode == V2PlanDependencyReasonCode.RouterRequired);
    }

    [Fact]
    public async Task BuildPlanAsync_CrossSwitchPlanWithoutRouter_ReturnsBlockingRequirement()
    {
        var result = await _service.BuildPlanAsync(CreateCrossSwitchRequest(includeRouter: false));

        Assert.False(result.Success);
        Assert.Contains(result.UnresolvedRequirements, requirement => requirement.Kind == V2UnresolvedRequirementKind.RouterRequirement);
        Assert.Contains(result.Issues, issue => issue.Code == "router-required" && issue.Severity == V2PlanIssueSeverity.Blocking);
    }

    [Fact]
    public async Task BuildPlanAsync_MissingInternalNetworkSwitch_AddsEnsureNodeBeforeVmProvision()
    {
        var request = CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin", "slot-dsrm"]);
        var network = Assert.Single(request.Template.LabNetworks!, item => item.NetworkId == "lab-core");
        network.SwitchName = "vSwitch-NewCore";
        network.SwitchType = V2SwitchTypeCatalog.Internal;
        request.AvailableSwitchNames = [];
        request.AvailableSwitches = [];

        var result = await _service.BuildPlanAsync(request);

        Assert.True(result.Success);
        var requirement = Assert.Single(result.Context.NetworkSwitchRequirements);
        Assert.Equal("vSwitch-NewCore", requirement.SwitchName);
        Assert.Equal(V2SwitchTypeCatalog.Internal, requirement.SwitchType);

        var ensureNode = Assert.Single(result.Nodes, node => node.Kind == V2PlanNodeKind.EnsureNetworkSwitch);
        var provisionNodes = result.Nodes.Where(node => node.Kind == V2PlanNodeKind.ProvisionVm).ToList();
        Assert.All(provisionNodes, provisionNode =>
            Assert.Contains(result.Dependencies, dep =>
                dep.FromNodeId == ensureNode.NodeId &&
                dep.ToNodeId == provisionNode.NodeId &&
                dep.ReasonCode == V2PlanDependencyReasonCode.SwitchRequired));
        Assert.All(provisionNodes, provisionNode => Assert.True(ensureNode.WaveHint < provisionNode.WaveHint));
    }

    [Fact]
    public async Task BuildPlanAsync_ExistingSwitchWithDifferentType_BlocksTypedNetworkReuse()
    {
        var request = CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin", "slot-dsrm"]);
        var network = Assert.Single(request.Template.LabNetworks!, item => item.NetworkId == "lab-core");
        network.SwitchType = V2SwitchTypeCatalog.Private;

        var result = await _service.BuildPlanAsync(request);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "switch-type-mismatch");
        Assert.Contains(result.UnresolvedRequirements, requirement =>
            requirement.Kind == V2UnresolvedRequirementKind.SwitchReference &&
            requirement.Key == "vSwitch-Core");
    }

    [Fact]
    public async Task BuildPlanAsync_ConflictingDeclaredSwitchTypesForSameName_BlocksTemplateSwitchIntent()
    {
        var request = CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin", "slot-dsrm"]);
        var coreNetwork = Assert.Single(request.Template.LabNetworks!, item => item.NetworkId == "lab-core");
        coreNetwork.SwitchName = "vSwitch-Shared";
        coreNetwork.SwitchType = V2SwitchTypeCatalog.Internal;
        request.Template.LabNetworks =
        [
            coreNetwork,
            new LabNetworkTemplate
            {
                NetworkId = "lab-isolated",
                Name = "Isolated",
                SwitchName = "vSwitch-Shared",
                SwitchType = V2SwitchTypeCatalog.Private
            }
        ];
        var memberVm = Assert.Single(request.Template.VmTemplates!, vm => vm.VmId == "vm-member01");
        var memberNic = Assert.Single(memberVm.Nics!);
        memberNic.NetworkId = "lab-isolated";
        request.AvailableSwitchNames = [];
        request.AvailableSwitches = [];

        var result = await _service.BuildPlanAsync(request);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "switch-type-mismatch");
        Assert.Equal(
            result.Nodes.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Count(),
            result.Nodes.Count);
    }

    [Fact]
    public async Task BuildPlanAsync_MissingExternalNetworkSwitch_RequiresDeployReviewAdapterMapping()
    {
        var request = CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin", "slot-dsrm"]);
        var network = Assert.Single(request.Template.LabNetworks!, item => item.NetworkId == "lab-core");
        network.SwitchName = "vSwitch-Wan";
        network.SwitchType = V2SwitchTypeCatalog.External;
        request.AvailableSwitchNames = [];
        request.AvailableSwitches = [];

        var result = await _service.BuildPlanAsync(request);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "external-switch-adapter-required");
        Assert.Contains(result.UnresolvedRequirements, requirement =>
            requirement.Kind == V2UnresolvedRequirementKind.ExternalSwitchAdapterMapping &&
            requirement.Key == "vSwitch-Wan");
    }

    [Fact]
    public async Task BuildPlanAsync_ExternalAdapterMapping_ResolvesMissingExternalSwitchRequirement()
    {
        var request = CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin", "slot-dsrm"]);
        var network = Assert.Single(request.Template.LabNetworks!, item => item.NetworkId == "lab-core");
        network.SwitchName = "vSwitch-Wan";
        network.SwitchType = V2SwitchTypeCatalog.External;
        request.AvailableSwitchNames = [];
        request.AvailableSwitches = [];
        request.ExternalSwitchAdapterMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["vSwitch-Wan"] = "Ethernet 2"
        };

        var result = await _service.BuildPlanAsync(request);

        Assert.True(result.Success);
        var requirement = Assert.Single(result.Context.NetworkSwitchRequirements);
        Assert.Equal("vSwitch-Wan", requirement.SwitchName);
        Assert.Equal(V2SwitchTypeCatalog.External, requirement.SwitchType);
        Assert.Equal("Ethernet 2", requirement.ExternalAdapterName);
    }

    [Fact]
    public async Task BuildPlanAsync_RootAndMemberPlan_PreservesDomainReadyOrdering()
    {
        var result = await _service.BuildPlanAsync(CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin", "slot-dsrm"]));
        var domainReady = Assert.Single(result.Nodes.Where(node => node.Kind == V2PlanNodeKind.DomainReady));
        var dnsGate = Assert.Single(result.Nodes.Where(node => node.Kind == V2PlanNodeKind.StabilizeDomainDns));
        var joinDomain = Assert.Single(result.Nodes.Where(node => node.Kind == V2PlanNodeKind.JoinDomain));
        var installAdDs = Assert.Single(result.Nodes.Where(node => node.Kind == V2PlanNodeKind.InstallAdDomainServicesFeature));
        var promoteRoot = Assert.Single(result.Nodes.Where(node => node.Kind == V2PlanNodeKind.PromoteRootDomainController));

        Assert.Contains(result.Dependencies, dep =>
            dep.FromNodeId == domainReady.NodeId &&
            dep.ToNodeId == joinDomain.NodeId &&
            dep.ReasonCode == V2PlanDependencyReasonCode.DomainRequired);
        Assert.Contains(result.Dependencies, dep =>
            dep.FromNodeId == installAdDs.NodeId &&
            dep.ToNodeId == promoteRoot.NodeId &&
            dep.ReasonCode == V2PlanDependencyReasonCode.RoleOrdering);
        Assert.Contains(result.Dependencies, dep =>
            dep.FromNodeId == dnsGate.NodeId &&
            dep.ToNodeId == joinDomain.NodeId &&
            dep.ReasonCode == V2PlanDependencyReasonCode.DomainRequired);
        Assert.True(domainReady.WaveHint < dnsGate.WaveHint);
        Assert.True(dnsGate.WaveHint < joinDomain.WaveHint);
    }

    [Fact]
    public async Task BuildPlanAsync_ReplicaAndMemberPlan_EmitPostRootNodes()
    {
        var request = CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin", "slot-dsrm"]);
        request.Template.VmTemplates.Insert(1, new VmTemplate
        {
            VmId = "vm-replica01",
            Name = "replica01",
            MemoryMb = 4096,
            CpuCount = 2,
            VhdxId = "disk-replica",
            TopologyRole = "ReplicaDomainController",
            DomainId = "domain-contoso",
            CredentialSlots = new VmCredentialSlotBindings
            {
                LocalBootstrap = "slot-local",
                DomainAdmin = "slot-admin",
                Dsrm = "slot-dsrm"
            },
            Nics =
            [
                new VmNetworkInterfaceTemplate
                {
                    NicId = "nic-replica",
                    NetworkId = "lab-core",
                    IpAddress = "10.0.0.11",
                    PrefixLength = 24,
                    DefaultGateway = "10.0.0.1",
                    DnsServers = ["10.0.0.10", "8.8.8.8"]
                }
            ]
        });
        request.CatalogItems = request.CatalogItems.Concat([CreateCatalogItem("disk-replica", "slot-local")]).ToArray();

        var result = await _service.BuildPlanAsync(request);

        Assert.True(result.Success);
        Assert.Contains(result.Nodes, node => node.Kind == V2PlanNodeKind.EnableGuestServices);
        Assert.Contains(result.Nodes, node => node.Kind == V2PlanNodeKind.PrepareGuestNetwork);
        Assert.Contains(result.Nodes, node => node.Kind == V2PlanNodeKind.ConfigureBaseRemoteAccess);
        Assert.Contains(result.Nodes, node => node.Kind == V2PlanNodeKind.BaseRemoteAccessReady);
        Assert.Contains(result.Nodes, node => node.Kind == V2PlanNodeKind.PromoteReplicaDomainController);
        Assert.Contains(result.Nodes, node => node.Kind == V2PlanNodeKind.ReplicaDomainReady);
        Assert.Contains(result.Nodes, node => node.Kind == V2PlanNodeKind.StabilizeDomainDns);
        Assert.Contains(result.Nodes, node => node.Kind == V2PlanNodeKind.JoinedDomainReady);
    }

    [Fact]
    public async Task BuildPlanAsync_ChildDomain_EmitsPerDomainCreationAndProgression()
    {
        var request = CreateChildDomainRequest();

        var result = await _service.BuildPlanAsync(request);

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "domain-relation-not-supported");

        var childFirstPromotion = Assert.Single(result.Nodes.Where(node =>
            node.Kind == V2PlanNodeKind.PromoteFirstDomainController &&
            node.VmId == "vm-childdc01"));
        var parentReady = Assert.Single(result.Nodes.Where(node =>
            node.Kind == V2PlanNodeKind.DomainReady &&
            node.VmId == "vm-dc01"));
        var childDnsGate = Assert.Single(result.Nodes.Where(node =>
            node.Kind == V2PlanNodeKind.StabilizeDomainDns &&
            node.VmId == "vm-childdc01"));
        var childReplicaPromotion = Assert.Single(result.Nodes.Where(node =>
            node.Kind == V2PlanNodeKind.PromoteReplicaDomainController &&
            node.VmId == "vm-childreplica01"));
        var childJoin = Assert.Single(result.Nodes.Where(node =>
            node.Kind == V2PlanNodeKind.JoinDomain &&
            node.VmId == "vm-childmember01"));

        Assert.Contains(result.Dependencies, dep =>
            dep.FromNodeId == parentReady.NodeId &&
            dep.ToNodeId == childFirstPromotion.NodeId &&
            dep.ReasonCode == V2PlanDependencyReasonCode.DomainRequired);
        Assert.Contains(result.Dependencies, dep =>
            dep.FromNodeId == childDnsGate.NodeId &&
            dep.ToNodeId == childJoin.NodeId &&
            dep.ReasonCode == V2PlanDependencyReasonCode.DomainRequired);
        Assert.Contains(result.Dependencies, dep =>
            dep.ToNodeId == childReplicaPromotion.NodeId &&
            dep.ReasonCode == V2PlanDependencyReasonCode.DomainRequired);
    }

    [Fact]
    public async Task BuildPlanAsync_TreeDomain_EmitsSharedPerDomainProgression()
    {
        var request = CreateChildDomainRequest();
        var treeDomain = Assert.Single(request.Template.DirectoryTopology!.Domains!.Where(domain => domain.DomainId == "domain-child"));
        treeDomain.RelationKind = V2DomainRelationKind.Tree;
        treeDomain.DnsName = "fabrikam.com";
        treeDomain.NetBiosName = "FABRIKAM";

        var result = await _service.BuildPlanAsync(request);

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "domain-relation-not-supported");

        var treeFirstPromotion = Assert.Single(result.Nodes.Where(node =>
            node.Kind == V2PlanNodeKind.PromoteFirstDomainController &&
            node.VmId == "vm-childdc01"));
        var sponsorReady = Assert.Single(result.Nodes.Where(node =>
            node.Kind == V2PlanNodeKind.DomainReady &&
            node.VmId == "vm-dc01"));
        var treeDnsGate = Assert.Single(result.Nodes.Where(node =>
            node.Kind == V2PlanNodeKind.StabilizeDomainDns &&
            node.VmId == "vm-childdc01"));
        var treeReplicaPromotion = Assert.Single(result.Nodes.Where(node =>
            node.Kind == V2PlanNodeKind.PromoteReplicaDomainController &&
            node.VmId == "vm-childreplica01"));
        var treeJoin = Assert.Single(result.Nodes.Where(node =>
            node.Kind == V2PlanNodeKind.JoinDomain &&
            node.VmId == "vm-childmember01"));

        Assert.Contains(result.Dependencies, dep =>
            dep.FromNodeId == sponsorReady.NodeId &&
            dep.ToNodeId == treeFirstPromotion.NodeId &&
            dep.ReasonCode == V2PlanDependencyReasonCode.DomainRequired);
        Assert.Contains(result.Dependencies, dep =>
            dep.FromNodeId == treeDnsGate.NodeId &&
            dep.ToNodeId == treeJoin.NodeId &&
            dep.ReasonCode == V2PlanDependencyReasonCode.DomainRequired);
        Assert.Contains(result.Dependencies, dep =>
            dep.ToNodeId == treeReplicaPromotion.NodeId &&
            dep.ReasonCode == V2PlanDependencyReasonCode.DomainRequired);
    }

    [Fact]
    public async Task BuildPlanAsync_MultipleIndependentRootForests_EmitEqualRootPaths()
    {
        var request = CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin", "slot-dsrm", "slot-fabrikam-admin"]);
        request.Template.VmTemplates.Add(new VmTemplate
        {
            VmId = "vm-fabrikamdc01",
            Name = "fabrikamdc01",
            MemoryMb = 4096,
            CpuCount = 2,
            VhdxId = "disk-fabrikamdc",
            TopologyRole = "FirstDomainController",
            DomainId = "domain-fabrikam",
            CredentialSlots = new VmCredentialSlotBindings
            {
                LocalBootstrap = "slot-local",
                DomainAdmin = "slot-fabrikam-admin",
                Dsrm = "slot-dsrm"
            },
            Nics =
            [
                new VmNetworkInterfaceTemplate
                {
                    NicId = "nic-fabrikamdc",
                    NetworkId = "lab-core",
                    IpAddress = "10.0.0.50",
                    PrefixLength = 24,
                    DefaultGateway = "10.0.0.1",
                    DnsServers = ["10.0.0.50"]
                }
            ]
        });
        request.Template.DirectoryTopology!.Forests!.Add(new V2ForestTemplate
        {
            ForestId = "forest-fabrikam",
            RootDomainId = "domain-fabrikam"
        });
        request.Template.DirectoryTopology.Domains!.Add(new V2DomainTemplate
        {
            DomainId = "domain-fabrikam",
            DnsName = "fabrikam.com",
            NetBiosName = "FABRIKAM",
            ForestId = "forest-fabrikam",
            RelationKind = V2DomainRelationKind.Root,
            FirstDomainControllerVmId = "vm-fabrikamdc01"
        });
        request.CatalogItems = request.CatalogItems.Concat([CreateCatalogItem("disk-fabrikamdc", "slot-local")]).ToArray();

        var result = await _service.BuildPlanAsync(request);

        Assert.True(result.Success);
        Assert.Equal(2, result.Nodes.Count(node => node.Kind == V2PlanNodeKind.PromoteFirstDomainController));
        Assert.Equal(2, result.Nodes.Count(node => node.Kind == V2PlanNodeKind.DomainReady));
        Assert.Contains(result.Nodes, node => node.Kind == V2PlanNodeKind.PromoteFirstDomainController && node.VmId == "vm-dc01");
        Assert.Contains(result.Nodes, node => node.Kind == V2PlanNodeKind.PromoteFirstDomainController && node.VmId == "vm-fabrikamdc01");
    }

    [Fact]
    public async Task BuildPlanAsync_ManagedBidirectionalForestTrust_EmitsResolvedContextAndOrderedTrustNodes()
    {
        var request = CreateManagedForestTrustRequest(["slot-local", "slot-admin", "slot-dsrm", "slot-fabrikam-admin"]);

        var result = await _service.BuildPlanAsync(request);

        Assert.True(result.Success);
        var trust = Assert.Single(result.Context.Trusts);
        Assert.Equal("trust-contoso-fabrikam", trust.TrustId);
        Assert.Equal("domain-contoso", trust.SourceDomainId);
        Assert.Equal("domain-fabrikam", trust.TargetDomainId);
        Assert.Equal("vm-dc01", trust.SourceAnchorVmId);
        Assert.Equal("vm-fabrikamdc01", trust.TargetAnchorVmId);
        Assert.Equal("slot-admin", trust.SourceDomainAdminCredentialSlot);
        Assert.Equal("slot-fabrikam-admin", trust.TargetDomainAdminCredentialSlot);

        var prepareDns = Assert.Single(result.Nodes.Where(node => node.Kind == V2PlanNodeKind.PrepareForestTrustDns));
        var createTrust = Assert.Single(result.Nodes.Where(node => node.Kind == V2PlanNodeKind.CreateForestTrust));
        var validateTrust = Assert.Single(result.Nodes.Where(node => node.Kind == V2PlanNodeKind.ValidateForestTrust));
        var contosoReady = Assert.Single(result.Nodes.Where(node => node.Kind == V2PlanNodeKind.DomainReady && node.VmId == "vm-dc01"));
        var fabrikamReady = Assert.Single(result.Nodes.Where(node => node.Kind == V2PlanNodeKind.DomainReady && node.VmId == "vm-fabrikamdc01"));

        Assert.Equal("trust-contoso-fabrikam", prepareDns.TrustId);
        Assert.Equal("vm-dc01", prepareDns.VmId);
        Assert.Contains(result.Dependencies, dep => dep.FromNodeId == contosoReady.NodeId && dep.ToNodeId == prepareDns.NodeId && dep.ReasonCode == V2PlanDependencyReasonCode.TrustRequired);
        Assert.Contains(result.Dependencies, dep => dep.FromNodeId == fabrikamReady.NodeId && dep.ToNodeId == prepareDns.NodeId && dep.ReasonCode == V2PlanDependencyReasonCode.TrustRequired);
        Assert.Contains(result.Dependencies, dep => dep.FromNodeId == prepareDns.NodeId && dep.ToNodeId == createTrust.NodeId && dep.ReasonCode == V2PlanDependencyReasonCode.TrustRequired);
        Assert.Contains(result.Dependencies, dep => dep.FromNodeId == createTrust.NodeId && dep.ToNodeId == validateTrust.NodeId && dep.ReasonCode == V2PlanDependencyReasonCode.TrustRequired);
        Assert.True(contosoReady.WaveHint < prepareDns.WaveHint);
        Assert.True(fabrikamReady.WaveHint < prepareDns.WaveHint);
        Assert.True(prepareDns.WaveHint < createTrust.WaveHint);
        Assert.True(createTrust.WaveHint < validateTrust.WaveHint);
    }

    [Fact]
    public async Task BuildPlanAsync_ManagedForestTrustMissingDomainAdminSlot_BlocksBeforeRuntime()
    {
        var request = CreateManagedForestTrustRequest(["slot-local", "slot-admin", "slot-dsrm"]);

        var result = await _service.BuildPlanAsync(request);

        Assert.False(result.Success);
        Assert.Contains(result.UnresolvedRequirements, requirement =>
            requirement.Kind == V2UnresolvedRequirementKind.CredentialSlot &&
            requirement.Key == "slot-fabrikam-admin");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "credential-slot-unresolved" &&
            issue.Message.Contains("forest trust 'trust-contoso-fabrikam' target domain", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildPlanAsync_UnsupportedTrustShape_BlocksBeforeRuntime()
    {
        var request = CreateManagedForestTrustRequest(["slot-local", "slot-admin", "slot-dsrm", "slot-fabrikam-admin"]);
        request.Template.DirectoryTopology!.Trusts![0].Direction = V2TrustDirection.Outbound;

        var result = await _service.BuildPlanAsync(request);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "trust-shape-unsupported" && issue.Severity == V2PlanIssueSeverity.Blocking);
        Assert.DoesNotContain(result.Nodes, node => node.Kind == V2PlanNodeKind.CreateForestTrust);
    }

    [Fact]
    public async Task BuildPlanAsync_PreservesExplicitMultiNicIntentInResolvedContext()
    {
        var result = await _service.BuildPlanAsync(CreateCrossSwitchRequest(includeRouter: true));
        var routerVm = Assert.Single(result.Context.Vms.Where(vm => vm.TopologyRole == "Router"));

        Assert.Equal(2, routerVm.Nics.Count);
        Assert.Equal("lab-core", routerVm.Nics[0].NetworkId);
        Assert.Equal("lab-external", routerVm.Nics[1].NetworkId);
    }

    [Fact]
    public async Task BuildPlanAsync_KnownCapabilityRoles_EmitCapabilityNodes()
    {
        var request = CreateSingleVmStandaloneRequest();
        request.Template.VmTemplates[0].CapabilityRoles = ["Web", "Operations"];

        var result = await _service.BuildPlanAsync(request);

        Assert.True(result.Success);
        Assert.Equal(2, result.Nodes.Count(node => node.Kind == V2PlanNodeKind.ApplyCapabilityRole));
        Assert.Contains(result.Nodes, node => node.CapabilityRole == "Web");
        Assert.Contains(result.Nodes, node => node.CapabilityRole == "Operations");
    }

    [Fact]
    public async Task BuildPlanAsync_UnknownCapabilityRoles_SurfaceWarnings()
    {
        var request = CreateSingleVmStandaloneRequest();
        request.Template.VmTemplates[0].CapabilityRoles = ["CustomRole"];

        var result = await _service.BuildPlanAsync(request);

        Assert.Contains(result.Issues, issue => issue.Code == "unknown-capability-role" && issue.Severity == V2PlanIssueSeverity.Warning);
        Assert.DoesNotContain(result.Nodes, node => node.Kind == V2PlanNodeKind.ApplyCapabilityRole);
    }

    private static V2PlanBuildRequest CreateSingleVmStandaloneRequest()
    {
        var template = CreateBaseTemplate("Balanced");
        template.LabNetworks =
        [
            new LabNetworkTemplate
            {
                NetworkId = "lab-core",
                Name = "Core",
                SwitchName = "vSwitch-Core"
            }
        ];
        template.VmTemplates =
        [
            new VmTemplate
            {
                VmId = "vm-standalone",
                Name = "standalone01",
                MemoryMb = 4096,
                CpuCount = 2,
                VhdxId = "disk-standalone",
                MembershipMode = V2MembershipModeCatalog.Standalone,
                Nics =
                [
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-1",
                        NetworkId = "lab-core",
                        IpAddress = "10.0.0.20",
                        PrefixLength = 24,
                        DefaultGateway = "10.0.0.1",
                        DnsServers = ["10.0.0.10"]
                    }
                ]
            }
        ];

        return new V2PlanBuildRequest
        {
            Template = template,
            CatalogItems =
            [
                CreateCatalogItem("disk-standalone", "slot-local")
            ],
            AvailableSwitchNames = ["vSwitch-Core"],
            AvailableSwitches = [CreateSwitch("vSwitch-Core", "Internal")],
            ResolvedCredentialSlotKeys = ["slot-local"],
            DefaultDeploymentProfile = "Balanced"
        };
    }

    private static V2PlanBuildRequest CreateAdCoreRequest(string? persistedProfile, IReadOnlyCollection<string> resolvedSlots)
    {
        var template = CreateBaseTemplate(persistedProfile);
        template.LabNetworks =
        [
            new LabNetworkTemplate
            {
                NetworkId = "lab-core",
                Name = "Core",
                SwitchName = "vSwitch-Core"
            }
        ];
        template.DirectoryTopology = CreateDirectoryTopology("forest-contoso", "domain-contoso", "contoso.com", "CONTOSO", "vm-dc01");
        template.VmTemplates =
        [
            new VmTemplate
            {
                VmId = "vm-dc01",
                Name = "dc01",
                MemoryMb = 4096,
                CpuCount = 2,
                VhdxId = "disk-dc",
                TopologyRole = "RootDomainController",
                DomainId = "domain-contoso",
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local",
                    DomainAdmin = "slot-admin",
                    Dsrm = "slot-dsrm"
                },
                Nics =
                [
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-dc",
                        NetworkId = "lab-core",
                        IpAddress = "10.0.0.10",
                        PrefixLength = 24,
                        DefaultGateway = "10.0.0.1",
                        DnsServers = ["10.0.0.10"]
                    }
                ]
            },
            new VmTemplate
            {
                VmId = "vm-member01",
                Name = "member01",
                MemoryMb = 4096,
                CpuCount = 2,
                VhdxId = "disk-member",
                MembershipMode = V2MembershipModeCatalog.DomainMember,
                DomainId = "domain-contoso",
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local",
                    DomainAdmin = "slot-admin",
                    DomainJoin = "slot-join"
                },
                CapabilityRoles = ["Web"],
                Nics =
                [
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-member",
                        NetworkId = "lab-core",
                        IpAddress = "10.0.0.20",
                        PrefixLength = 24,
                        DefaultGateway = "10.0.0.1",
                        DnsServers = ["10.0.0.10"]
                    }
                ]
            }
        ];

        return new V2PlanBuildRequest
        {
            Template = template,
            CatalogItems =
            [
                CreateCatalogItem("disk-dc", "slot-local"),
                CreateCatalogItem("disk-member", "slot-local")
            ],
            AvailableSwitchNames = ["vSwitch-Core"],
            AvailableSwitches = [CreateSwitch("vSwitch-Core", "Internal")],
            ResolvedCredentialSlotKeys = resolvedSlots,
            DefaultDeploymentProfile = "Balanced"
        };
    }

    private static V2PlanBuildRequest CreateCrossSwitchRequest(bool includeRouter)
    {
        var template = CreateBaseTemplate("Balanced");
        template.LabNetworks =
        [
            new LabNetworkTemplate
            {
                NetworkId = "lab-core",
                Name = "Core",
                SwitchName = "vSwitch-Core"
            },
            new LabNetworkTemplate
            {
                NetworkId = "lab-edge",
                Name = "Edge",
                SwitchName = "vSwitch-Edge"
            },
            new LabNetworkTemplate
            {
                NetworkId = "lab-external",
                Name = "External",
                SwitchName = "vSwitch-External"
            }
        ];

        var vmTemplates = new List<VmTemplate>
        {
            new()
            {
                VmId = "vm-root",
                Name = "dc01",
                MemoryMb = 4096,
                CpuCount = 2,
                VhdxId = "disk-dc",
                TopologyRole = "RootDomainController",
                DomainId = "domain-contoso",
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local",
                    DomainAdmin = "slot-admin",
                    Dsrm = "slot-dsrm"
                },
                Nics =
                [
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-root",
                        NetworkId = "lab-core",
                        IpAddress = "10.0.0.10",
                        PrefixLength = 24,
                        DefaultGateway = "10.0.0.1",
                        DnsServers = ["10.0.0.10"]
                    }
                ]
            },
            new()
            {
                VmId = "vm-member",
                Name = "member01",
                MemoryMb = 4096,
                CpuCount = 2,
                VhdxId = "disk-member",
                MembershipMode = V2MembershipModeCatalog.DomainMember,
                DomainId = "domain-contoso",
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local",
                    DomainAdmin = "slot-admin",
                    DomainJoin = "slot-join"
                },
                Nics =
                [
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-member",
                        NetworkId = "lab-edge",
                        IpAddress = "10.0.1.20",
                        PrefixLength = 24,
                        DefaultGateway = "10.0.1.1",
                        DnsServers = ["10.0.0.10"]
                    }
                ]
            }
        };

        if (includeRouter)
        {
            vmTemplates.Add(new VmTemplate
            {
                VmId = "vm-router",
                Name = "router01",
                MemoryMb = 4096,
                CpuCount = 2,
                VhdxId = "disk-router",
                TopologyRole = "Router",
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local"
                },
                Nics =
                [
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-router-core",
                        NetworkId = "lab-core",
                        IpAddress = "10.0.0.1",
                        PrefixLength = 24
                    },
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-router-edge",
                        NetworkId = "lab-external"
                    }
                ]
            });
        }

        template.VmTemplates = vmTemplates;
        template.DirectoryTopology = CreateDirectoryTopology("forest-contoso", "domain-contoso", "contoso.com", "CONTOSO", "vm-root");

        return new V2PlanBuildRequest
        {
            Template = template,
            CatalogItems =
            [
                CreateCatalogItem("disk-dc", "slot-local"),
                CreateCatalogItem("disk-member", "slot-local"),
                CreateCatalogItem("disk-router", "slot-local")
            ],
            AvailableSwitchNames = ["vSwitch-Core", "vSwitch-Edge", "vSwitch-External"],
            AvailableSwitches =
            [
                CreateSwitch("vSwitch-Core", "Internal"),
                CreateSwitch("vSwitch-Edge", "Internal"),
                CreateSwitch("vSwitch-External", "External")
            ],
            ResolvedCredentialSlotKeys = ["slot-local", "slot-join", "slot-admin", "slot-dsrm"],
            DefaultDeploymentProfile = "Balanced"
        };
    }

    private static V2PlanBuildRequest CreateChildDomainRequest()
    {
        var request = CreateAdCoreRequest(
            "Balanced",
            ["slot-local", "slot-join", "slot-admin", "slot-dsrm", "slot-child-admin", "slot-parent-admin"]);

        request.Template.VmTemplates.AddRange(
        [
            new VmTemplate
            {
                VmId = "vm-childdc01",
                Name = "childdc01",
                MemoryMb = 4096,
                CpuCount = 2,
                VhdxId = "disk-childdc",
                TopologyRole = "FirstDomainController",
                DomainId = "domain-child",
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local",
                    DomainAdmin = "slot-child-admin",
                    Dsrm = "slot-dsrm",
                    ParentDomainAdmin = "slot-parent-admin"
                },
                Nics =
                [
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-childdc",
                        NetworkId = "lab-core",
                        IpAddress = "10.0.0.30",
                        PrefixLength = 24,
                        DefaultGateway = "10.0.0.1",
                        DnsServers = ["10.0.0.10"]
                    }
                ]
            },
            new VmTemplate
            {
                VmId = "vm-childreplica01",
                Name = "childreplica01",
                MemoryMb = 4096,
                CpuCount = 2,
                VhdxId = "disk-childreplica",
                TopologyRole = "ReplicaDomainController",
                DomainId = "domain-child",
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local",
                    DomainAdmin = "slot-child-admin",
                    Dsrm = "slot-dsrm"
                },
                Nics =
                [
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-childreplica",
                        NetworkId = "lab-core",
                        IpAddress = "10.0.0.31",
                        PrefixLength = 24,
                        DefaultGateway = "10.0.0.1",
                        DnsServers = ["10.0.0.30", "10.0.0.10"]
                    }
                ]
            },
            new VmTemplate
            {
                VmId = "vm-childmember01",
                Name = "childmember01",
                MemoryMb = 4096,
                CpuCount = 2,
                VhdxId = "disk-childmember",
                MembershipMode = V2MembershipModeCatalog.DomainMember,
                DomainId = "domain-child",
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local",
                    DomainAdmin = "slot-child-admin",
                    DomainJoin = "slot-join"
                },
                Nics =
                [
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-childmember",
                        NetworkId = "lab-core",
                        IpAddress = "10.0.0.40",
                        PrefixLength = 24,
                        DefaultGateway = "10.0.0.1",
                        DnsServers = ["10.0.0.30", "10.0.0.10"]
                    }
                ]
            }
        ]);

        request.Template.DirectoryTopology!.Domains!.Add(new V2DomainTemplate
        {
            DomainId = "domain-child",
            DnsName = "child.contoso.com",
            NetBiosName = "CHILD",
            ForestId = "forest-contoso",
            RelationKind = V2DomainRelationKind.Child,
            ParentDomainId = "domain-contoso",
            FirstDomainControllerVmId = "vm-childdc01"
        });

        request.CatalogItems = request.CatalogItems.Concat(
        [
            CreateCatalogItem("disk-childdc", "slot-local"),
            CreateCatalogItem("disk-childreplica", "slot-local"),
            CreateCatalogItem("disk-childmember", "slot-local")
        ]).ToArray();

        return request;
    }

    private static V2PlanBuildRequest CreateManagedForestTrustRequest(IReadOnlyCollection<string> resolvedSlots)
    {
        var request = CreateAdCoreRequest("Balanced", resolvedSlots);
        request.Template.VmTemplates.RemoveAll(vm => vm.VmId == "vm-member01");
        request.Template.VmTemplates.Add(new VmTemplate
        {
            VmId = "vm-fabrikamdc01",
            Name = "fabrikamdc01",
            MemoryMb = 4096,
            CpuCount = 2,
            VhdxId = "disk-fabrikamdc",
            TopologyRole = "FirstDomainController",
            DomainId = "domain-fabrikam",
            CredentialSlots = new VmCredentialSlotBindings
            {
                LocalBootstrap = "slot-local",
                DomainAdmin = "slot-fabrikam-admin",
                Dsrm = "slot-dsrm"
            },
            Nics =
            [
                new VmNetworkInterfaceTemplate
                {
                    NicId = "nic-fabrikamdc",
                    NetworkId = "lab-core",
                    IpAddress = "10.0.0.50",
                    PrefixLength = 24,
                    DefaultGateway = "10.0.0.1",
                    DnsServers = ["10.0.0.50"]
                }
            ]
        });
        request.Template.DirectoryTopology!.Forests!.Add(new V2ForestTemplate
        {
            ForestId = "forest-fabrikam",
            RootDomainId = "domain-fabrikam"
        });
        request.Template.DirectoryTopology.Domains!.Add(new V2DomainTemplate
        {
            DomainId = "domain-fabrikam",
            DnsName = "fabrikam.com",
            NetBiosName = "FABRIKAM",
            ForestId = "forest-fabrikam",
            RelationKind = V2DomainRelationKind.Root,
            FirstDomainControllerVmId = "vm-fabrikamdc01"
        });
        request.Template.DirectoryTopology.Trusts =
        [
            new V2TrustTemplate
            {
                TrustId = "trust-contoso-fabrikam",
                SourceDomainId = "domain-contoso",
                TargetDomainId = "domain-fabrikam",
                TrustType = V2TrustType.Forest,
                Direction = V2TrustDirection.Bidirectional
            }
        ];
        request.CatalogItems = request.CatalogItems.Concat([CreateCatalogItem("disk-fabrikamdc", "slot-local")]).ToArray();
        request.ResolvedCredentialSlotKeys = resolvedSlots;

        return request;
    }

    private static LabTemplate CreateBaseTemplate(string? persistedProfile)
    {
        return new LabTemplate
        {
            Id = "template-v2",
            Name = "V2 Template",
            SchemaVersion = "2.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = LabTemplate.SupportedTemplateType,
            DeploymentProfile = persistedProfile,
            ExecutionEngine = TemplateExecutionEngine.V2UnifiedPlanning
        };
    }

    private static VhdxCatalogItem CreateCatalogItem(string id, string slotRef)
    {
        return new VhdxCatalogItem
        {
            Id = id,
            Path = $@"C:\base\{id}.vhdx",
            OsName = "Windows Server",
            OsVersion = "2022",
            Generation = 2,
            Signature = $"{id}-sig",
            BootstrapProfile = new VhdxBootstrapProfile
            {
                ExpectedLocalUser = "Administrator",
                LocalCredentialSlotRef = slotRef,
                GuestOsFamily = "windows",
                GuestTransport = "powershell-direct"
            }
        };
    }

    private static V2DirectoryTopologyTemplate CreateDirectoryTopology(
        string forestId,
        string domainId,
        string dnsName,
        string netBiosName,
        string firstDomainControllerVmId)
    {
        return new V2DirectoryTopologyTemplate
        {
            Forests =
            [
                new V2ForestTemplate
                {
                    ForestId = forestId,
                    RootDomainId = domainId
                }
            ],
            Domains =
            [
                new V2DomainTemplate
                {
                    DomainId = domainId,
                    DnsName = dnsName,
                    NetBiosName = netBiosName,
                    ForestId = forestId,
                    RelationKind = V2DomainRelationKind.Root,
                    FirstDomainControllerVmId = firstDomainControllerVmId
                }
            ]
        };
    }

    private static V2AvailableSwitchInfo CreateSwitch(string name, string switchType)
        => new()
        {
            Name = name,
            SwitchType = switchType
        };
}
