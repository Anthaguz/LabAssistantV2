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
    /// Returns the supplied admin password when a real deploy password is present, otherwise a throwaway
    /// placeholder. The credential store rejects empty passwords, and plan-only validation seeds slots without a
    /// real password, so extra credential slots must always carry a non-empty value even when unused by planning.
    /// </summary>
    private static string NonEmptyPassword(HarnessOptions options)
        => options.HasPassword ? options.AdminPassword : "placeholder-" + Guid.NewGuid().ToString("N");
}
