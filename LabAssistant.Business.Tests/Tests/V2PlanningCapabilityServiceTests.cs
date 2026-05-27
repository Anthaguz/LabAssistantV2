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
        var request = CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin"]);

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
        var request = CreateAdCoreRequest(null, ["slot-local", "slot-join", "slot-admin"]);
        request.DefaultDeploymentProfile = "Aggressive";

        var result = await _service.BuildPlanAsync(request);

        Assert.True(result.Success);
        Assert.Equal("Aggressive", result.Context.ResolvedDeploymentProfileName);
        Assert.Equal(V2DeploymentProfile.Aggressive, result.Context.ResolvedDeploymentProfile);
    }

    [Fact]
    public async Task BuildPlanAsync_DifferentProfiles_ChangeWavesWithoutChangingDependencies()
    {
        var balanced = await _service.BuildPlanAsync(CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin"]));
        var aggressive = await _service.BuildPlanAsync(CreateAdCoreRequest("Aggressive", ["slot-local", "slot-join", "slot-admin"]));

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
        var result = await _service.BuildPlanAsync(CreateAdCoreRequest("Balanced", ["slot-join", "slot-admin"]));

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
    public async Task BuildPlanAsync_RootAndMemberPlan_PreservesDomainReadyOrdering()
    {
        var result = await _service.BuildPlanAsync(CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin"]));
        var domainReady = Assert.Single(result.Nodes.Where(node => node.Kind == V2PlanNodeKind.DomainReady));
        var joinDomain = Assert.Single(result.Nodes.Where(node => node.Kind == V2PlanNodeKind.JoinDomain));

        Assert.Contains(result.Dependencies, dep =>
            dep.FromNodeId == domainReady.NodeId &&
            dep.ToNodeId == joinDomain.NodeId &&
            dep.ReasonCode == V2PlanDependencyReasonCode.DomainRequired);
        Assert.True(domainReady.WaveHint < joinDomain.WaveHint);
    }

    [Fact]
    public async Task BuildPlanAsync_PreservesExplicitMultiNicIntentInResolvedContext()
    {
        var result = await _service.BuildPlanAsync(CreateCrossSwitchRequest(includeRouter: true));
        var routerVm = Assert.Single(result.Context.Vms.Where(vm => vm.TopologyRole == "Router"));

        Assert.Equal(2, routerVm.Nics.Count);
        Assert.Equal("lab-core", routerVm.Nics[0].NetworkId);
        Assert.Equal("lab-edge", routerVm.Nics[1].NetworkId);
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
                TopologyRole = "StandaloneServer",
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
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local"
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
                TopologyRole = "MemberServer",
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local",
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
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local"
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
                TopologyRole = "MemberServer",
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local",
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
                        NetworkId = "lab-edge",
                        IpAddress = "10.0.1.1",
                        PrefixLength = 24
                    }
                ]
            });
        }

        template.VmTemplates = vmTemplates;

        return new V2PlanBuildRequest
        {
            Template = template,
            CatalogItems =
            [
                CreateCatalogItem("disk-dc", "slot-local"),
                CreateCatalogItem("disk-member", "slot-local"),
                CreateCatalogItem("disk-router", "slot-local")
            ],
            AvailableSwitchNames = ["vSwitch-Core", "vSwitch-Edge"],
            ResolvedCredentialSlotKeys = ["slot-local", "slot-join"],
            DefaultDeploymentProfile = "Balanced"
        };
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
}
