using LabAssistant.Models.Templates;

namespace LabAssistant.Deployment.Harness;

/// <summary>
/// Builders for the canonical lab topologies used by the smoke suite. Standalone is implemented now; richer
/// topologies (domain controller, router, child domain, forest trust) are added here as each is promoted from
/// a fast plan-level golden test into a real Hyper-V smoke test.
/// </summary>
public static class LabScenarioLibrary
{
    /// <summary>
    /// One standalone VM off the base image on the Default Switch, driven through the V2 planner + runtime.
    /// The NIC is a bare DHCP attachment (switch only, no static addressing), so no guest configuration is
    /// required and the plan is <c>ProvisionVm -&gt; StartVm</c>. This is exactly the degenerate case the
    /// orphan-guest-network fix (PR #853) protects, which makes it a good end-to-end smoke.
    /// </summary>
    public static LabScenario Standalone(HarnessOptions options, string vmName = "harness-standalone-01")
    {
        var template = new LabTemplate
        {
            Name = "Harness Standalone Smoke",
            Description = "Single standalone VM through the V2 planner + runtime + ready-set graph scheduler.",
            SchemaVersion = TemplateSchemaVersionCatalog.V2SchemaVersion,
            TemplateType = LabTemplate.SupportedTemplateType,
            DeploymentProfile = "Balanced",
            VmTemplates = new List<VmTemplate>
            {
                new()
                {
                    Name = vmName,
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = options.BaseImageId,
                    VhdPath = options.BaseImagePath,
                    SwitchName = "Default Switch",
                    SwitchNames = new List<string> { "Default Switch" },
                    MembershipMode = V2MembershipModeCatalog.Standalone
                }
            },
            NetworkConfig = new NetworkConfig { SwitchName = "Default Switch" }
        };

        return new LabScenario
        {
            Name = "Standalone",
            Template = template,
            ExpectedVmNames = new[] { vmName }
        };
    }

    /// <summary>
    /// A single first domain controller that promotes a brand-new forest. The VM sits on an Internal switch
    /// (which the runtime creates and tears down itself), carries a static address, and is its own DNS server -
    /// so the topology is fully self-contained and needs no router or upstream DNS. This is the first rung above
    /// <see cref="Standalone"/>: it exercises the AD promotion path end to end (feature install, promote, domain
    /// ready, DNS stabilize) without any join, replica, or trust to muddy the first real run.
    /// </summary>
    /// <remarks>
    /// No secret is embedded: the domain-admin slot reuses the base image admin password (the built-in
    /// Administrator keeps its password through promotion) and the DSRM slot reuses it too. When no real password
    /// is supplied (plan-only validation) both fall back to a throwaway placeholder, because planning never reads
    /// credential values - only their slot keys must resolve.
    /// </remarks>
    public static LabScenario DomainController(HarnessOptions options, string vmName = "harness-dc-01")
    {
        const string switchName = "LabCore";
        const string networkId = "lab-core";
        const string forestId = "forest-smoke";
        const string domainId = "domain-smoke";
        const string dcVmId = "vm-dc01";
        const string domainAdminSlot = "smoke-domain-admin";
        const string dsrmSlot = "smoke-dsrm";

        var template = new LabTemplate
        {
            Name = "Harness Domain Controller Smoke",
            Description = "Single first domain controller promoting a new forest through the V2 planner + runtime.",
            SchemaVersion = TemplateSchemaVersionCatalog.V2SchemaVersion,
            TemplateType = LabTemplate.SupportedTemplateType,
            DeploymentProfile = "Balanced",
            LabNetworks = new List<LabNetworkTemplate>
            {
                new() { NetworkId = networkId, Name = "Core", SwitchName = switchName, SwitchType = "Internal" }
            },
            DirectoryTopology = new V2DirectoryTopologyTemplate
            {
                Forests = new List<V2ForestTemplate>
                {
                    new() { ForestId = forestId, RootDomainId = domainId }
                },
                Domains = new List<V2DomainTemplate>
                {
                    new()
                    {
                        DomainId = domainId,
                        DnsName = "smoke.lab",
                        NetBiosName = "SMOKE",
                        ForestId = forestId,
                        RelationKind = V2DomainRelationKind.Root,
                        FirstDomainControllerVmId = dcVmId
                    }
                }
            },
            VmTemplates = new List<VmTemplate>
            {
                new()
                {
                    VmId = dcVmId,
                    Name = vmName,
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = options.BaseImageId,
                    VhdPath = options.BaseImagePath,
                    TopologyRole = "RootDomainController",
                    DomainId = domainId,
                    CredentialSlots = new VmCredentialSlotBindings
                    {
                        LocalBootstrap = HarnessOptions.BootstrapSlotKey,
                        DomainAdmin = domainAdminSlot,
                        Dsrm = dsrmSlot
                    },
                    Nics = new List<VmNetworkInterfaceTemplate>
                    {
                        new()
                        {
                            NicId = "nic-dc",
                            NetworkId = networkId,
                            IpAddress = "10.0.0.10",
                            PrefixLength = 24,
                            DnsServers = new List<string> { "10.0.0.10" }
                        }
                    }
                }
            },
            NetworkConfig = new NetworkConfig { SwitchName = switchName }
        };

        var password = NonEmptyPassword(options);
        return new LabScenario
        {
            Name = "DomainController",
            Template = template,
            ExtraCredentials = new[]
            {
                new CredentialSeed { SlotKey = domainAdminSlot, Username = options.AdminUser, Password = password },
                new CredentialSeed { SlotKey = dsrmSlot, Username = options.AdminUser, Password = password }
            },
            ExpectedVmNames = new[] { vmName }
        };
    }

    /// <summary>
    /// A first domain controller plus one member server that joins the new forest. Both VMs sit on the same
    /// Internal switch; the member's only DNS server is the domain controller, so the ready-set scheduler must
    /// hold the member's join until the DC's domain is ready. This is the first multi-VM topology and the first
    /// exercise of the JoinDomain / JoinedDomainReady runtime path end to end.
    /// </summary>
    public static LabScenario DomainMember(
        HarnessOptions options,
        string dcVmName = "harness-dc-01",
        string memberVmName = "harness-mem-01")
    {
        const string switchName = "LabCore";
        const string networkId = "lab-core";
        const string forestId = "forest-smoke";
        const string domainId = "domain-smoke";
        const string dcVmId = "vm-dc01";
        const string memberVmId = "vm-mem01";
        const string domainAdminSlot = "smoke-domain-admin";
        const string dsrmSlot = "smoke-dsrm";

        var template = new LabTemplate
        {
            Name = "Harness Domain Member Smoke",
            Description = "First domain controller plus a member server joining the new forest through V2.",
            SchemaVersion = TemplateSchemaVersionCatalog.V2SchemaVersion,
            TemplateType = LabTemplate.SupportedTemplateType,
            DeploymentProfile = "Balanced",
            LabNetworks = new List<LabNetworkTemplate>
            {
                new() { NetworkId = networkId, Name = "Core", SwitchName = switchName, SwitchType = "Internal" }
            },
            DirectoryTopology = new V2DirectoryTopologyTemplate
            {
                Forests = new List<V2ForestTemplate>
                {
                    new() { ForestId = forestId, RootDomainId = domainId }
                },
                Domains = new List<V2DomainTemplate>
                {
                    new()
                    {
                        DomainId = domainId,
                        DnsName = "smoke.lab",
                        NetBiosName = "SMOKE",
                        ForestId = forestId,
                        RelationKind = V2DomainRelationKind.Root,
                        FirstDomainControllerVmId = dcVmId
                    }
                }
            },
            VmTemplates = new List<VmTemplate>
            {
                new()
                {
                    VmId = dcVmId,
                    Name = dcVmName,
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = options.BaseImageId,
                    VhdPath = options.BaseImagePath,
                    TopologyRole = "RootDomainController",
                    DomainId = domainId,
                    CredentialSlots = new VmCredentialSlotBindings
                    {
                        LocalBootstrap = HarnessOptions.BootstrapSlotKey,
                        DomainAdmin = domainAdminSlot,
                        Dsrm = dsrmSlot
                    },
                    Nics = new List<VmNetworkInterfaceTemplate>
                    {
                        new()
                        {
                            NicId = "nic-dc",
                            NetworkId = networkId,
                            IpAddress = "10.0.0.10",
                            PrefixLength = 24,
                            DnsServers = new List<string> { "10.0.0.10" }
                        }
                    }
                },
                new()
                {
                    VmId = memberVmId,
                    Name = memberVmName,
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = options.BaseImageId,
                    VhdPath = options.BaseImagePath,
                    MembershipMode = V2MembershipModeCatalog.DomainMember,
                    DomainId = domainId,
                    CredentialSlots = new VmCredentialSlotBindings
                    {
                        LocalBootstrap = HarnessOptions.BootstrapSlotKey,
                        DomainAdmin = domainAdminSlot,
                        DomainJoin = domainAdminSlot
                    },
                    Nics = new List<VmNetworkInterfaceTemplate>
                    {
                        new()
                        {
                            NicId = "nic-mem",
                            NetworkId = networkId,
                            IpAddress = "10.0.0.20",
                            PrefixLength = 24,
                            DnsServers = new List<string> { "10.0.0.10" }
                        }
                    }
                }
            },
            NetworkConfig = new NetworkConfig { SwitchName = switchName }
        };

        var password = NonEmptyPassword(options);
        return new LabScenario
        {
            Name = "DomainMember",
            Template = template,
            ExtraCredentials = new[]
            {
                new CredentialSeed { SlotKey = domainAdminSlot, Username = options.AdminUser, Password = password },
                new CredentialSeed { SlotKey = dsrmSlot, Username = options.AdminUser, Password = password }
            },
            ExpectedVmNames = new[] { dcVmName, memberVmName }
        };
    }

    /// <summary>
    /// A first domain controller plus a second domain controller that replicates the same forest root domain.
    /// Both DCs sit on the same Internal switch; the replica's only DNS server is the first DC, so the ready-set
    /// scheduler must hold the replica's promotion until the first DC's domain is ready. This is the first
    /// exercise of the PromoteReplicaDomainController / ReplicaDomainReady runtime path end to end, and it
    /// validates the post-promotion domain-credential qualification on a second DC.
    /// </summary>
    public static LabScenario ReplicaDomainController(
        HarnessOptions options,
        string firstDcVmName = "harness-dc-01",
        string replicaDcVmName = "harness-dc-02")
    {
        const string switchName = "LabCore";
        const string networkId = "lab-core";
        const string forestId = "forest-smoke";
        const string domainId = "domain-smoke";
        const string firstDcVmId = "vm-dc01";
        const string replicaDcVmId = "vm-dc02";
        const string domainAdminSlot = "smoke-domain-admin";
        const string dsrmSlot = "smoke-dsrm";

        var template = new LabTemplate
        {
            Name = "Harness Replica Domain Controller Smoke",
            Description = "First domain controller plus a replica domain controller for the same forest via V2.",
            SchemaVersion = TemplateSchemaVersionCatalog.V2SchemaVersion,
            TemplateType = LabTemplate.SupportedTemplateType,
            DeploymentProfile = "Balanced",
            LabNetworks = new List<LabNetworkTemplate>
            {
                new() { NetworkId = networkId, Name = "Core", SwitchName = switchName, SwitchType = "Internal" }
            },
            DirectoryTopology = new V2DirectoryTopologyTemplate
            {
                Forests = new List<V2ForestTemplate>
                {
                    new() { ForestId = forestId, RootDomainId = domainId }
                },
                Domains = new List<V2DomainTemplate>
                {
                    new()
                    {
                        DomainId = domainId,
                        DnsName = "smoke.lab",
                        NetBiosName = "SMOKE",
                        ForestId = forestId,
                        RelationKind = V2DomainRelationKind.Root,
                        FirstDomainControllerVmId = firstDcVmId
                    }
                }
            },
            VmTemplates = new List<VmTemplate>
            {
                new()
                {
                    VmId = firstDcVmId,
                    Name = firstDcVmName,
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = options.BaseImageId,
                    VhdPath = options.BaseImagePath,
                    TopologyRole = "RootDomainController",
                    DomainId = domainId,
                    CredentialSlots = new VmCredentialSlotBindings
                    {
                        LocalBootstrap = HarnessOptions.BootstrapSlotKey,
                        DomainAdmin = domainAdminSlot,
                        Dsrm = dsrmSlot
                    },
                    Nics = new List<VmNetworkInterfaceTemplate>
                    {
                        new()
                        {
                            NicId = "nic-dc",
                            NetworkId = networkId,
                            IpAddress = "10.0.0.10",
                            PrefixLength = 24,
                            DnsServers = new List<string> { "10.0.0.10" }
                        }
                    }
                },
                new()
                {
                    VmId = replicaDcVmId,
                    Name = replicaDcVmName,
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = options.BaseImageId,
                    VhdPath = options.BaseImagePath,
                    TopologyRole = "ReplicaDomainController",
                    DomainId = domainId,
                    CredentialSlots = new VmCredentialSlotBindings
                    {
                        LocalBootstrap = HarnessOptions.BootstrapSlotKey,
                        DomainAdmin = domainAdminSlot,
                        DomainJoin = domainAdminSlot,
                        Dsrm = dsrmSlot
                    },
                    Nics = new List<VmNetworkInterfaceTemplate>
                    {
                        new()
                        {
                            NicId = "nic-dc2",
                            NetworkId = networkId,
                            IpAddress = "10.0.0.11",
                            PrefixLength = 24,
                            DnsServers = new List<string> { "10.0.0.10" }
                        }
                    }
                }
            },
            NetworkConfig = new NetworkConfig { SwitchName = switchName }
        };

        var password = NonEmptyPassword(options);
        return new LabScenario
        {
            Name = "ReplicaDomainController",
            Template = template,
            ExtraCredentials = new[]
            {
                new CredentialSeed { SlotKey = domainAdminSlot, Username = options.AdminUser, Password = password },
                new CredentialSeed { SlotKey = dsrmSlot, Username = options.AdminUser, Password = password }
            },
            ExpectedVmNames = new[] { firstDcVmName, replicaDcVmName }
        };
    }

    /// <summary>
    /// A forest root domain controller plus a child-domain domain controller under it. Both DCs sit on the same
    /// Internal switch (same subnet), so the child can reach the parent for DNS and replication without a router;
    /// the child's only DNS server is the parent DC, so the ready-set scheduler must hold the child promotion
    /// until the parent domain is ready. This is the first live exercise of the child-domain promotion path,
    /// which authenticates to the parent forest with the parent-domain administrator (qualified with the parent
    /// NetBIOS name) before creating the new child domain.
    /// </summary>
    public static LabScenario ChildDomain(
        HarnessOptions options,
        string rootDcVmName = "harness-dc-01",
        string childDcVmName = "harness-dc-02")
    {
        const string switchName = "LabCore";
        const string networkId = "lab-core";
        const string forestId = "forest-smoke";
        const string rootDomainId = "domain-smoke";
        const string childDomainId = "domain-child";
        const string rootDcVmId = "vm-dc01";
        const string childDcVmId = "vm-dc02";
        const string domainAdminSlot = "smoke-domain-admin";
        const string dsrmSlot = "smoke-dsrm";

        var template = new LabTemplate
        {
            Name = "Harness Child Domain Smoke",
            Description = "Forest root DC plus a child-domain DC promoted under it through the V2 planner + runtime.",
            SchemaVersion = TemplateSchemaVersionCatalog.V2SchemaVersion,
            TemplateType = LabTemplate.SupportedTemplateType,
            DeploymentProfile = "Balanced",
            LabNetworks = new List<LabNetworkTemplate>
            {
                new() { NetworkId = networkId, Name = "Core", SwitchName = switchName, SwitchType = "Internal" }
            },
            DirectoryTopology = new V2DirectoryTopologyTemplate
            {
                Forests = new List<V2ForestTemplate>
                {
                    new() { ForestId = forestId, RootDomainId = rootDomainId }
                },
                Domains = new List<V2DomainTemplate>
                {
                    new()
                    {
                        DomainId = rootDomainId,
                        DnsName = "smoke.lab",
                        NetBiosName = "SMOKE",
                        ForestId = forestId,
                        RelationKind = V2DomainRelationKind.Root,
                        FirstDomainControllerVmId = rootDcVmId
                    },
                    new()
                    {
                        DomainId = childDomainId,
                        DnsName = "child.smoke.lab",
                        NetBiosName = "CHILD",
                        ForestId = forestId,
                        RelationKind = V2DomainRelationKind.Child,
                        ParentDomainId = rootDomainId,
                        FirstDomainControllerVmId = childDcVmId
                    }
                }
            },
            VmTemplates = new List<VmTemplate>
            {
                new()
                {
                    VmId = rootDcVmId,
                    Name = rootDcVmName,
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = options.BaseImageId,
                    VhdPath = options.BaseImagePath,
                    TopologyRole = "RootDomainController",
                    DomainId = rootDomainId,
                    CredentialSlots = new VmCredentialSlotBindings
                    {
                        LocalBootstrap = HarnessOptions.BootstrapSlotKey,
                        DomainAdmin = domainAdminSlot,
                        Dsrm = dsrmSlot
                    },
                    Nics = new List<VmNetworkInterfaceTemplate>
                    {
                        new()
                        {
                            NicId = "nic-dc",
                            NetworkId = networkId,
                            IpAddress = "10.0.0.10",
                            PrefixLength = 24,
                            DnsServers = new List<string> { "10.0.0.10" }
                        }
                    }
                },
                new()
                {
                    VmId = childDcVmId,
                    Name = childDcVmName,
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = options.BaseImageId,
                    VhdPath = options.BaseImagePath,
                    TopologyRole = "RootDomainController",
                    DomainId = childDomainId,
                    CredentialSlots = new VmCredentialSlotBindings
                    {
                        LocalBootstrap = HarnessOptions.BootstrapSlotKey,
                        DomainAdmin = domainAdminSlot,
                        ParentDomainAdmin = domainAdminSlot,
                        Dsrm = dsrmSlot
                    },
                    Nics = new List<VmNetworkInterfaceTemplate>
                    {
                        new()
                        {
                            NicId = "nic-dc2",
                            NetworkId = networkId,
                            IpAddress = "10.0.0.11",
                            PrefixLength = 24,
                            DnsServers = new List<string> { "10.0.0.10" }
                        }
                    }
                }
            },
            NetworkConfig = new NetworkConfig { SwitchName = switchName }
        };

        var password = NonEmptyPassword(options);
        return new LabScenario
        {
            Name = "ChildDomain",
            Template = template,
            ExtraCredentials = new[]
            {
                new CredentialSeed { SlotKey = domainAdminSlot, Username = options.AdminUser, Password = password },
                new CredentialSeed { SlotKey = dsrmSlot, Username = options.AdminUser, Password = password }
            },
            ExpectedVmNames = new[] { rootDcVmName, childDcVmName }
        };
    }

    /// <summary>
    /// Returns the supplied admin password when a real deploy password is present, otherwise a throwaway
    /// placeholder. The credential store rejects empty passwords, and plan-only validation seeds slots without a
    /// real password, so extra credential slots must always carry a non-empty value even when unused by planning.
    /// </summary>
    private static string NonEmptyPassword(HarnessOptions options)
        => options.HasPassword ? options.AdminPassword : "placeholder-" + Guid.NewGuid().ToString("N");
}