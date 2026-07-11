using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

internal static class TemplatesBuilderDraftMapper
{
    private const string DefaultCreatedWithAppVersion = "1.0.0";

    public static TemplatesBuilderDraftSnapshot CreateSuggestedDraft(TemplatesBuilderReferenceData referenceData)
    {
        var availableSwitches = referenceData.AvailableVmSwitches ?? Array.Empty<string>();
        var vhdxCatalogOptions = referenceData.VhdxCatalogOptions ?? Array.Empty<TemplateVhdxCatalogOption>();
        var switchName = availableSwitches.FirstOrDefault() ?? "vSwitch-Core";
        var dcDisk = vhdxCatalogOptions.FirstOrDefault()?.Id ?? string.Empty;
        var memberDisk = vhdxCatalogOptions.Skip(1).FirstOrDefault()?.Id ?? dcDisk;

        return new TemplatesBuilderDraftSnapshot(
            TemplateName: "V2 Topology Template",
            TemplateDescription: "Topology-first V2 template draft.",
            DeploymentProfile: "Balanced",
            LabNetworks:
            [
                new TemplatesBuilderLabNetworkDraft("lab-core", "Core", switchName, string.Empty, "10.0.0.0/24", "Core lab network")
            ],
            CredentialSlots:
            [
                new TemplatesBuilderCredentialSlotDraft("slot-local", "Local bootstrap", "local bootstrap"),
                new TemplatesBuilderCredentialSlotDraft("slot-admin", "Domain admin", "domain administration"),
                new TemplatesBuilderCredentialSlotDraft("slot-join", "Domain join", "domain join"),
                new TemplatesBuilderCredentialSlotDraft("slot-dsrm", "DSRM", "domain controller recovery")
            ],
            Forests:
            [
                new TemplatesBuilderForestDraft("forest-contoso", "domain-contoso")
            ],
            Domains:
            [
                new TemplatesBuilderDomainDraft("domain-contoso", "contoso.com", "CONTOSO", "forest-contoso", nameof(V2DomainRelationKind.Root), string.Empty)
            ],
            Vms:
            [
                new TemplatesBuilderVmDraft(
                    "vm-dc01",
                    "dc01",
                    "4096",
                    "2",
                    dcDisk,
                    V2MembershipModeCatalog.DomainMember,
                    "domain-contoso",
                    IsActiveDirectoryDomainController: true,
                    new TemplatesBuilderVmCredentialSlotDraft("slot-local", "slot-admin", string.Empty, "slot-dsrm", string.Empty),
                    [
                        new TemplatesBuilderNicDraft("nic-dc", "Domain", "lab-core", string.Empty, "10.0.0.10", "24", "10.0.0.1", ["10.0.0.10"])
                    ]),
                new TemplatesBuilderVmDraft(
                    "vm-member01",
                    "member01",
                    "4096",
                    "2",
                    memberDisk,
                    V2MembershipModeCatalog.DomainMember,
                    "domain-contoso",
                    IsActiveDirectoryDomainController: false,
                    new TemplatesBuilderVmCredentialSlotDraft("slot-local", "slot-admin", "slot-join", string.Empty, string.Empty),
                    [
                        new TemplatesBuilderNicDraft("nic-member", "Domain", "lab-core", string.Empty, "10.0.0.20", "24", "10.0.0.1", ["10.0.0.10"])
                    ])
            ],
            IsSaveConfirmed: false);
    }

    public static TemplatesBuilderDraftSnapshot FromTemplate(LabTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        return new TemplatesBuilderDraftSnapshot(
            TemplateName: template.Name,
            TemplateDescription: template.Description ?? string.Empty,
            DeploymentProfile: template.DeploymentProfile ?? "Balanced",
            LabNetworks: CopyLabNetworks(template.LabNetworks, template.VmTemplates),
            CredentialSlots: BuildCredentialSlotReferences(template.VmTemplates),
            Forests: CopyForests(template.DirectoryTopology?.Forests),
            Domains: CopyDomains(template.DirectoryTopology?.Domains),
            Vms: CopyVms(template.VmTemplates),
            IsSaveConfirmed: false);
    }

    public static TemplatesBuilderDraftBuildResult BuildDocument(
        TemplatesBuilderDraftSnapshot draft,
        string templateId,
        int templateRevision,
        string createdWithAppVersion,
        string? sourceFilePath,
        IReadOnlyList<V2TrustTemplate>? preservedTrusts = null)
    {
        var errors = new List<string>();
        var template = new LabTemplate
        {
            Id = string.IsNullOrWhiteSpace(templateId) ? Guid.NewGuid().ToString("N") : templateId.Trim(),
            Name = draft.TemplateName.Trim(),
            Description = string.IsNullOrWhiteSpace(draft.TemplateDescription) ? null : draft.TemplateDescription.Trim(),
            SchemaVersion = TemplateSchemaVersionCatalog.V2SchemaVersion,
            TemplateRevision = templateRevision <= 0 ? 1 : templateRevision,
            CreatedWithAppVersion = string.IsNullOrWhiteSpace(createdWithAppVersion)
                ? DefaultCreatedWithAppVersion
                : createdWithAppVersion.Trim(),
            TemplateType = LabTemplate.SupportedTemplateType,
            DeploymentProfile = string.IsNullOrWhiteSpace(draft.DeploymentProfile) ? null : draft.DeploymentProfile.Trim(),
            ExecutionEngine = TemplateExecutionEngine.V2UnifiedPlanning
        };

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            errors.Add("Template name is required.");
        }

        if (draft.Vms.Count == 0)
        {
            errors.Add("At least one VM is required before Save.");
        }

        template.LabNetworks = MapLabNetworks(draft.LabNetworks, errors);
        template.DirectoryTopology = new V2DirectoryTopologyTemplate
        {
            Forests = MapForests(draft.Forests, errors),
            Domains = MapDomains(draft.Domains, draft.Vms, errors),
            Trusts = CopyTrusts(preservedTrusts)
        };
        template.VmTemplates = MapVms(draft.Vms, errors);

        if (errors.Count > 0)
        {
            return new TemplatesBuilderDraftBuildResult { Errors = errors };
        }

        return new TemplatesBuilderDraftBuildResult
        {
            Document = new TemplateEditorDocument
            {
                Template = template,
                SourceFilePath = sourceFilePath
            }
        };
    }

    private static List<LabNetworkTemplate> MapLabNetworks(IReadOnlyList<TemplatesBuilderLabNetworkDraft> drafts, List<string> errors)
    {
        var networks = new List<LabNetworkTemplate>();
        foreach (var network in drafts)
        {
            if (string.IsNullOrWhiteSpace(network.NetworkId) || string.IsNullOrWhiteSpace(network.Name))
            {
                errors.Add("Each lab network requires a network id and name.");
                continue;
            }

            networks.Add(new LabNetworkTemplate
            {
                NetworkId = network.NetworkId.Trim(),
                Name = network.Name.Trim(),
                SwitchName = Optional(network.SwitchName),
                SwitchType = Optional(network.SwitchType),
                Subnet = Optional(network.Subnet),
                Notes = Optional(network.Notes)
            });
        }

        return networks;
    }

    private static List<V2ForestTemplate> MapForests(IReadOnlyList<TemplatesBuilderForestDraft> drafts, List<string> errors)
    {
        var forests = new List<V2ForestTemplate>();
        foreach (var forest in drafts)
        {
            if (string.IsNullOrWhiteSpace(forest.ForestId) || string.IsNullOrWhiteSpace(forest.RootDomainId))
            {
                errors.Add("Each forest requires a forest id and root domain id.");
                continue;
            }

            forests.Add(new V2ForestTemplate
            {
                ForestId = forest.ForestId.Trim(),
                RootDomainId = forest.RootDomainId.Trim()
            });
        }

        return forests;
    }

    private static List<V2DomainTemplate> MapDomains(
        IReadOnlyList<TemplatesBuilderDomainDraft> domainDrafts,
        IReadOnlyList<TemplatesBuilderVmDraft> vmDrafts,
        List<string> errors)
    {
        if (domainDrafts.Count == 0)
        {
            foreach (var vm in vmDrafts)
            {
                var membershipMode = V2MembershipModeCatalog.Normalize(vm.MembershipMode);
                if (V2MembershipModeCatalog.IsDomainMember(membershipMode))
                {
                    errors.Add($"VM '{vm.Name}' cannot use DomainMember membership without a declared domain.");
                }

                if (vm.IsActiveDirectoryDomainController)
                {
                    errors.Add($"VM '{vm.Name}' cannot be assigned the Active Directory Domain Controller role without a declared domain.");
                }
            }

            return [];
        }

        var dcByDomain = vmDrafts
            .Where(vm => vm.IsActiveDirectoryDomainController && !string.IsNullOrWhiteSpace(vm.DomainId))
            .GroupBy(vm => vm.DomainId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var domains = new List<V2DomainTemplate>();

        foreach (var domain in domainDrafts)
        {
            if (string.IsNullOrWhiteSpace(domain.DomainId) ||
                string.IsNullOrWhiteSpace(domain.DnsName) ||
                string.IsNullOrWhiteSpace(domain.NetBiosName) ||
                string.IsNullOrWhiteSpace(domain.ForestId))
            {
                errors.Add("Each domain requires domain id, DNS name, NetBIOS name, and forest id.");
                continue;
            }

            if (!Enum.TryParse<V2DomainRelationKind>(domain.RelationKind, ignoreCase: true, out var relationKind))
            {
                errors.Add($"Domain '{domain.DomainId}' relation kind must be Root, Child, or Tree.");
                continue;
            }

            if (!dcByDomain.TryGetValue(domain.DomainId.Trim(), out var firstDc))
            {
                errors.Add($"Domain '{domain.DomainId}' requires at least one VM assigned the Active Directory Domain Controller role.");
                continue;
            }

            domains.Add(new V2DomainTemplate
            {
                DomainId = domain.DomainId.Trim(),
                DnsName = domain.DnsName.Trim(),
                NetBiosName = domain.NetBiosName.Trim(),
                ForestId = domain.ForestId.Trim(),
                RelationKind = relationKind,
                ParentDomainId = Optional(domain.ParentDomainId),
                FirstDomainControllerVmId = firstDc.VmId.Trim()
            });
        }

        return domains;
    }

    private static List<VmTemplate> MapVms(IReadOnlyList<TemplatesBuilderVmDraft> drafts, List<string> errors)
    {
        var vms = new List<VmTemplate>();
        foreach (var vm in drafts)
        {
            if (string.IsNullOrWhiteSpace(vm.VmId) || string.IsNullOrWhiteSpace(vm.Name))
            {
                errors.Add("Each VM requires a VM id and name.");
                continue;
            }

            if (!TryParsePositiveInt(vm.MemoryMb, out var memoryMb))
            {
                errors.Add($"VM '{vm.Name}' memory must be a positive integer.");
                continue;
            }

            if (!TryParsePositiveInt(vm.CpuCount, out var cpuCount))
            {
                errors.Add($"VM '{vm.Name}' CPU count must be a positive integer.");
                continue;
            }

            var membershipMode = V2MembershipModeCatalog.Normalize(vm.MembershipMode);
            if (membershipMode is null)
            {
                errors.Add($"VM '{vm.Name}' membership must be DomainMember or Standalone.");
                continue;
            }

            if (vm.IsActiveDirectoryDomainController && !V2MembershipModeCatalog.IsDomainMember(membershipMode))
            {
                errors.Add($"VM '{vm.Name}' must use DomainMember membership when assigned the Active Directory Domain Controller role.");
                continue;
            }

            if (V2MembershipModeCatalog.IsDomainMember(membershipMode) && string.IsNullOrWhiteSpace(vm.DomainId))
            {
                errors.Add($"VM '{vm.Name}' requires a domain assignment when membership is DomainMember.");
                continue;
            }

            if (V2MembershipModeCatalog.IsStandalone(membershipMode) && !string.IsNullOrWhiteSpace(vm.DomainId))
            {
                errors.Add($"VM '{vm.Name}' must not carry a domain assignment when membership is Standalone.");
                continue;
            }

            var nics = MapNics(vm, errors);
            vms.Add(new VmTemplate
            {
                VmId = vm.VmId.Trim(),
                Name = vm.Name.Trim(),
                MemoryMb = memoryMb,
                CpuCount = cpuCount,
                VhdxId = Optional(vm.VhdxId),
                TopologyRole = ResolveTopologyRole(vm),
                MembershipMode = membershipMode,
                DomainId = V2MembershipModeCatalog.IsDomainMember(membershipMode) ? vm.DomainId.Trim() : null,
                CredentialSlots = CreateCredentialSlots(vm.CredentialSlots),
                Nics = nics.Count == 0 ? null : nics
            });
        }

        return vms;
    }

    private static string? ResolveTopologyRole(TemplatesBuilderVmDraft vm)
    {
        if (vm.IsRouter)
        {
            return TemplatesBuilderRoleProjectionCatalog.RouterTopologyRole;
        }

        return vm.IsActiveDirectoryDomainController
            ? TemplatesBuilderRoleProjectionCatalog.ActiveDirectoryDomainControllerTopologyRole
            : null;
    }

    private static List<VmNetworkInterfaceTemplate> MapNics(TemplatesBuilderVmDraft vm, List<string> errors)
    {
        var nics = new List<VmNetworkInterfaceTemplate>();
        foreach (var nic in vm.Nics ?? Array.Empty<TemplatesBuilderNicDraft>())
        {
            if (string.IsNullOrWhiteSpace(nic.NicId))
            {
                errors.Add($"VM '{vm.Name}' has a NIC without a NIC id.");
                continue;
            }

            if (!TryParseOptionalPrefixLength(nic.PrefixLength, out var prefixLength))
            {
                errors.Add($"VM '{vm.Name}' NIC '{nic.NicId}' prefix length must be 0 through 128.");
                continue;
            }

            nics.Add(new VmNetworkInterfaceTemplate
            {
                NicId = nic.NicId.Trim(),
                Name = Optional(nic.Name),
                NetworkId = Optional(nic.NetworkId),
                SwitchName = Optional(nic.SwitchName),
                IpAddress = Optional(nic.IpAddress),
                PrefixLength = prefixLength,
                DefaultGateway = Optional(nic.DefaultGateway),
                DnsServers = CopyList(nic.DnsServers)
            });
        }

        return nics;
    }

    private static VmCredentialSlotBindings? CreateCredentialSlots(TemplatesBuilderVmCredentialSlotDraft draft)
    {
        var slots = new VmCredentialSlotBindings
        {
            LocalBootstrap = Optional(draft.LocalBootstrap),
            DomainAdmin = Optional(draft.DomainAdmin),
            DomainJoin = Optional(draft.DomainJoin),
            Dsrm = Optional(draft.Dsrm),
            ParentDomainAdmin = Optional(draft.ParentDomainAdmin)
        };

        return slots.LocalBootstrap is null &&
               slots.DomainAdmin is null &&
               slots.DomainJoin is null &&
               slots.Dsrm is null &&
               slots.ParentDomainAdmin is null
            ? null
            : slots;
    }

    private static IReadOnlyList<TemplatesBuilderLabNetworkDraft> CopyLabNetworks(
        IEnumerable<LabNetworkTemplate>? networks,
        IEnumerable<VmTemplate>? vms)
    {
        var vmList = vms?.ToList() ?? [];
        return networks?
            .Select(network => new TemplatesBuilderLabNetworkDraft(
                network.NetworkId,
                network.Name,
                network.SwitchName ?? string.Empty,
                network.SwitchType ?? string.Empty,
                network.Subnet ?? string.Empty,
                network.Notes ?? string.Empty)
            {
                DomainId = ReconstructNetworkDomainId(network.NetworkId, vmList)
            })
            .ToList() ?? [];
    }

    // A network's owning domain is not persisted on the network; it is recovered from the domain-member VMs
    // whose NICs sit on the network. Empty when no domain member references it (the shared standalone switch).
    private static string ReconstructNetworkDomainId(string? networkId, IReadOnlyList<VmTemplate> vms)
    {
        if (string.IsNullOrWhiteSpace(networkId))
        {
            return string.Empty;
        }

        foreach (var vm in vms)
        {
            if (!V2MembershipModeCatalog.IsDomainMember(vm.MembershipMode) || string.IsNullOrWhiteSpace(vm.DomainId))
            {
                continue;
            }

            var onNetwork = (vm.Nics ?? Enumerable.Empty<VmNetworkInterfaceTemplate>())
                .Any(nic => string.Equals(nic.NetworkId, networkId, StringComparison.OrdinalIgnoreCase));
            if (onNetwork)
            {
                return vm.DomainId.Trim();
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<TemplatesBuilderForestDraft> CopyForests(IEnumerable<V2ForestTemplate>? forests)
        => forests?
            .Select(forest => new TemplatesBuilderForestDraft(forest.ForestId, forest.RootDomainId))
            .ToList() ?? [];

    private static IReadOnlyList<TemplatesBuilderDomainDraft> CopyDomains(IEnumerable<V2DomainTemplate>? domains)
        => domains?
            .Select(domain => new TemplatesBuilderDomainDraft(
                domain.DomainId,
                domain.DnsName,
                domain.NetBiosName,
                domain.ForestId,
                domain.RelationKind.ToString(),
                domain.ParentDomainId ?? string.Empty))
            .ToList() ?? [];

    private static IReadOnlyList<TemplatesBuilderVmDraft> CopyVms(IEnumerable<VmTemplate> vms)
        => vms
            .Select(vm => new TemplatesBuilderVmDraft(
                vm.VmId,
                vm.Name,
                vm.MemoryMb.ToString(System.Globalization.CultureInfo.InvariantCulture),
                vm.CpuCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                vm.VhdxId ?? string.Empty,
                ResolveMembershipMode(vm),
                vm.DomainId ?? string.Empty,
                IsActiveDirectoryDomainController(vm.TopologyRole),
                new TemplatesBuilderVmCredentialSlotDraft(
                    vm.CredentialSlots?.LocalBootstrap ?? string.Empty,
                    vm.CredentialSlots?.DomainAdmin ?? string.Empty,
                    vm.CredentialSlots?.DomainJoin ?? string.Empty,
                    vm.CredentialSlots?.Dsrm ?? string.Empty,
                    vm.CredentialSlots?.ParentDomainAdmin ?? string.Empty),
                CopyNics(vm.Nics))
            {
                IsRouter = TemplatesBuilderRoleProjectionCatalog.IsRouterTopologyRole(vm.TopologyRole)
            })
            .ToList();

    private static IReadOnlyList<TemplatesBuilderNicDraft> CopyNics(IEnumerable<VmNetworkInterfaceTemplate>? nics)
        => nics?
            .Select(nic => new TemplatesBuilderNicDraft(
                nic.NicId,
                nic.Name ?? string.Empty,
                nic.NetworkId ?? string.Empty,
                nic.SwitchName ?? string.Empty,
                nic.IpAddress ?? string.Empty,
                nic.PrefixLength?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                nic.DefaultGateway ?? string.Empty,
                nic.DnsServers ?? []))
            .ToList() ?? [];

    private static IReadOnlyList<TemplatesBuilderCredentialSlotDraft> BuildCredentialSlotReferences(IEnumerable<VmTemplate> vms)
    {
        var slots = new Dictionary<string, TemplatesBuilderCredentialSlotDraft>(StringComparer.OrdinalIgnoreCase);
        foreach (var vm in vms)
        {
            AddSlot(slots, vm.CredentialSlots?.LocalBootstrap, "Local bootstrap", "local bootstrap");
            AddSlot(slots, vm.CredentialSlots?.DomainAdmin, "Domain admin", "domain administration");
            AddSlot(slots, vm.CredentialSlots?.DomainJoin, "Domain join", "domain join");
            AddSlot(slots, vm.CredentialSlots?.Dsrm, "DSRM", "domain controller recovery");
            AddSlot(slots, vm.CredentialSlots?.ParentDomainAdmin, "Parent domain admin", "dependent-domain creation");
        }

        return slots.Values.OrderBy(slot => slot.SlotKey, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddSlot(
        IDictionary<string, TemplatesBuilderCredentialSlotDraft> slots,
        string? key,
        string label,
        string scopeHint)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        slots.TryAdd(key.Trim(), new TemplatesBuilderCredentialSlotDraft(key.Trim(), label, scopeHint));
    }

    private static string ResolveMembershipMode(VmTemplate vm)
    {
        var normalized = V2MembershipModeCatalog.Normalize(vm.MembershipMode);
        if (normalized is not null)
        {
            return normalized;
        }

        return !string.IsNullOrWhiteSpace(vm.DomainId) || IsActiveDirectoryDomainController(vm.TopologyRole)
            ? V2MembershipModeCatalog.DomainMember
            : V2MembershipModeCatalog.Standalone;
    }

    private static bool IsActiveDirectoryDomainController(string? topologyRole)
        => TemplatesBuilderRoleProjectionCatalog.IsActiveDirectoryDomainControllerTopologyRole(topologyRole);

    private static List<V2TrustTemplate>? CopyTrusts(IReadOnlyList<V2TrustTemplate>? trusts)
    {
        if (trusts is not { Count: > 0 })
        {
            return null;
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

    private static List<string>? CopyList(IEnumerable<string>? values)
    {
        var result = values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList() ?? [];

        return result.Count == 0 ? null : result;
    }

    private static bool TryParsePositiveInt(string value, out int result)
        => int.TryParse(value, out result) && result > 0;

    private static bool TryParseOptionalPrefixLength(string value, out int? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!int.TryParse(value, out var parsed) ||
            parsed is < 0 or > 128)
        {
            return false;
        }

        result = parsed;
        return true;
    }

    private static string? Optional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
