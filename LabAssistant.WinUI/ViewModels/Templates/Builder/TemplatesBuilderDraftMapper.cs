using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

internal static class TemplatesBuilderDraftMapper
{
    private const string DefaultCreatedWithAppVersion = "1.0.0";

    public static TemplatesBuilderDraftSnapshot CreateSuggestedDraft(TemplatesBuilderReferenceData referenceData)
    {
        var switchName = referenceData.AvailableVmSwitches.FirstOrDefault() ?? "vSwitch-Core";
        var dcDisk = referenceData.VhdxCatalogOptions.FirstOrDefault()?.Id ?? string.Empty;
        var memberDisk = referenceData.VhdxCatalogOptions.Skip(1).FirstOrDefault()?.Id ?? dcDisk;

        return new TemplatesBuilderDraftSnapshot(
            TemplateName: "V2 Topology Template",
            TemplateDescription: "Topology-first V2 template draft.",
            DeploymentProfile: "Balanced",
            LabNetworksText: $"lab-core|Core|{switchName}|10.0.0.0/24|Core lab network",
            ForestsText: "forest-contoso|domain-contoso",
            DomainsText: "domain-contoso|contoso.com|CONTOSO|forest-contoso|Root||vm-dc01",
            VmsText: string.Join(
                Environment.NewLine,
                $"vm-dc01|dc01|4096|2|{dcDisk}|FirstDomainController||domain-contoso|slot-local|slot-admin||slot-dsrm|",
                $"vm-member01|member01|4096|2|{memberDisk}||DomainMember|domain-contoso|slot-local|slot-admin|slot-join||"),
            NicsText: string.Join(
                Environment.NewLine,
                "vm-dc01|nic-dc|Domain|lab-core||10.0.0.10|24|10.0.0.1|10.0.0.10",
                "vm-member01|nic-member|Domain|lab-core||10.0.0.20|24|10.0.0.1|10.0.0.10"),
            IsSaveConfirmed: false);
    }

    public static TemplatesBuilderDraftSnapshot FromTemplate(LabTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        return new TemplatesBuilderDraftSnapshot(
            TemplateName: template.Name,
            TemplateDescription: template.Description ?? string.Empty,
            DeploymentProfile: template.DeploymentProfile ?? "Balanced",
            LabNetworksText: JoinRows((template.LabNetworks ?? []).Select(network => string.Join(
                "|",
                network.NetworkId,
                network.Name,
                network.SwitchName,
                network.Subnet,
                network.Notes))),
            ForestsText: JoinRows((template.DirectoryTopology?.Forests ?? []).Select(forest => string.Join(
                "|",
                forest.ForestId,
                forest.RootDomainId))),
            DomainsText: JoinRows((template.DirectoryTopology?.Domains ?? []).Select(domain => string.Join(
                "|",
                domain.DomainId,
                domain.DnsName,
                domain.NetBiosName,
                domain.ForestId,
                domain.RelationKind,
                domain.ParentDomainId,
                domain.FirstDomainControllerVmId))),
            VmsText: JoinRows(template.VmTemplates.Select(vm => string.Join(
                "|",
                vm.VmId,
                vm.Name,
                vm.MemoryMb,
                vm.CpuCount,
                vm.VhdxId,
                vm.TopologyRole,
                vm.MembershipMode,
                vm.DomainId,
                vm.CredentialSlots?.LocalBootstrap,
                vm.CredentialSlots?.DomainAdmin,
                vm.CredentialSlots?.DomainJoin,
                vm.CredentialSlots?.Dsrm,
                vm.CredentialSlots?.ParentDomainAdmin))),
            NicsText: JoinRows(template.VmTemplates.SelectMany(vm => (vm.Nics ?? []).Select(nic => string.Join(
                "|",
                vm.VmId,
                nic.NicId,
                nic.Name,
                nic.NetworkId,
                nic.SwitchName,
                nic.IpAddress,
                nic.PrefixLength,
                nic.DefaultGateway,
                nic.DnsServers is null ? null : string.Join(",", nic.DnsServers))))),
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

        template.LabNetworks = ParseLabNetworks(draft.LabNetworksText, errors);
        template.DirectoryTopology = new V2DirectoryTopologyTemplate
        {
            Forests = ParseForests(draft.ForestsText, errors),
            Domains = ParseDomains(draft.DomainsText, errors),
            Trusts = CopyTrusts(preservedTrusts)
        };
        template.VmTemplates = ParseVms(draft.VmsText, errors);
        ApplyNics(template.VmTemplates, draft.NicsText, errors);

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

    private static List<LabNetworkTemplate> ParseLabNetworks(string text, List<string> errors)
    {
        var networks = new List<LabNetworkTemplate>();
        foreach (var (fields, lineNumber) in EnumerateRows(text))
        {
            if (fields.Length < 2)
            {
                errors.Add($"Lab network row {lineNumber} must include networkId and name.");
                continue;
            }

            networks.Add(new LabNetworkTemplate
            {
                NetworkId = fields[0],
                Name = fields[1],
                SwitchName = Optional(fields, 2),
                Subnet = Optional(fields, 3),
                Notes = Optional(fields, 4)
            });
        }

        return networks;
    }

    private static List<V2ForestTemplate> ParseForests(string text, List<string> errors)
    {
        var forests = new List<V2ForestTemplate>();
        foreach (var (fields, lineNumber) in EnumerateRows(text))
        {
            if (fields.Length < 2)
            {
                errors.Add($"Forest row {lineNumber} must include forestId and rootDomainId.");
                continue;
            }

            forests.Add(new V2ForestTemplate
            {
                ForestId = fields[0],
                RootDomainId = fields[1]
            });
        }

        return forests;
    }

    private static List<V2DomainTemplate> ParseDomains(string text, List<string> errors)
    {
        var domains = new List<V2DomainTemplate>();
        foreach (var (fields, lineNumber) in EnumerateRows(text))
        {
            if (fields.Length < 7)
            {
                errors.Add($"Domain row {lineNumber} must include domainId, DNS name, NetBIOS name, forestId, relation kind, parent domain, and first DC VM id.");
                continue;
            }

            if (!Enum.TryParse<V2DomainRelationKind>(fields[4], ignoreCase: true, out var relationKind))
            {
                errors.Add($"Domain row {lineNumber} relation kind must be Root, Child, or Tree.");
                continue;
            }

            domains.Add(new V2DomainTemplate
            {
                DomainId = fields[0],
                DnsName = fields[1],
                NetBiosName = fields[2],
                ForestId = fields[3],
                RelationKind = relationKind,
                ParentDomainId = Optional(fields, 5),
                FirstDomainControllerVmId = fields[6]
            });
        }

        return domains;
    }

    private static List<VmTemplate> ParseVms(string text, List<string> errors)
    {
        var vms = new List<VmTemplate>();
        foreach (var (fields, lineNumber) in EnumerateRows(text))
        {
            if (fields.Length < 13)
            {
                errors.Add($"VM row {lineNumber} must include vmId, name, memory, CPU, disk, topology, membership, domain, and credential slot columns.");
                continue;
            }

            if (!int.TryParse(fields[2], out var memoryMb) || memoryMb <= 0)
            {
                errors.Add($"VM row {lineNumber} memory must be a positive integer.");
                continue;
            }

            if (!int.TryParse(fields[3], out var cpuCount) || cpuCount <= 0)
            {
                errors.Add($"VM row {lineNumber} CPU count must be a positive integer.");
                continue;
            }

            vms.Add(new VmTemplate
            {
                VmId = fields[0],
                Name = fields[1],
                MemoryMb = memoryMb,
                CpuCount = cpuCount,
                VhdxId = Optional(fields, 4),
                TopologyRole = Optional(fields, 5),
                MembershipMode = Optional(fields, 6),
                DomainId = Optional(fields, 7),
                CredentialSlots = CreateCredentialSlots(fields)
            });
        }

        return vms;
    }

    private static void ApplyNics(List<VmTemplate> vms, string text, List<string> errors)
    {
        var vmsById = new Dictionary<string, VmTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (var vm in vms)
        {
            if (string.IsNullOrWhiteSpace(vm.VmId))
            {
                errors.Add($"VM '{vm.Name}' vmId is required before NIC rows can be assigned.");
                continue;
            }

            if (!vmsById.TryAdd(vm.VmId, vm))
            {
                errors.Add($"Duplicate VM id '{vm.VmId}' is not allowed.");
            }
        }

        foreach (var (fields, lineNumber) in EnumerateRows(text))
        {
            if (fields.Length < 9)
            {
                errors.Add($"NIC row {lineNumber} must include vmId, nicId, name, networkId, switchName, IP, prefix, gateway, and DNS servers.");
                continue;
            }

            if (!vmsById.TryGetValue(fields[0], out var vm))
            {
                errors.Add($"NIC row {lineNumber} references unknown VM '{fields[0]}'.");
                continue;
            }

            int? prefixLength = null;
            if (!string.IsNullOrWhiteSpace(fields[6]))
            {
                if (!int.TryParse(fields[6], out var parsedPrefix) || parsedPrefix < 0 || parsedPrefix > 128)
                {
                    errors.Add($"NIC row {lineNumber} prefix length must be 0 through 128.");
                    continue;
                }

                prefixLength = parsedPrefix;
            }

            vm.Nics ??= [];
            vm.Nics.Add(new VmNetworkInterfaceTemplate
            {
                NicId = fields[1],
                Name = Optional(fields, 2),
                NetworkId = Optional(fields, 3),
                SwitchName = Optional(fields, 4),
                IpAddress = Optional(fields, 5),
                PrefixLength = prefixLength,
                DefaultGateway = Optional(fields, 7),
                DnsServers = SplitList(Optional(fields, 8))
            });
        }
    }

    private static VmCredentialSlotBindings? CreateCredentialSlots(string[] fields)
    {
        var slots = new VmCredentialSlotBindings
        {
            LocalBootstrap = Optional(fields, 8),
            DomainAdmin = Optional(fields, 9),
            DomainJoin = Optional(fields, 10),
            Dsrm = Optional(fields, 11),
            ParentDomainAdmin = Optional(fields, 12)
        };

        return slots.LocalBootstrap is null &&
               slots.DomainAdmin is null &&
               slots.DomainJoin is null &&
               slots.Dsrm is null &&
               slots.ParentDomainAdmin is null
            ? null
            : slots;
    }

    private static IEnumerable<(string[] Fields, int LineNumber)> EnumerateRows(string text)
    {
        var lines = (text ?? string.Empty)
            .Split(["\r\n", "\n"], StringSplitOptions.None);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            yield return (line.Split('|').Select(field => field.Trim()).ToArray(), i + 1);
        }
    }

    private static string? Optional(string[] fields, int index)
    {
        if (index >= fields.Length || string.IsNullOrWhiteSpace(fields[index]))
        {
            return null;
        }

        return fields[index].Trim();
    }

    private static List<string>? SplitList(string? value)
    {
        var values = value?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList() ?? [];

        return values.Count == 0 ? null : values;
    }

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

    private static string JoinRows(IEnumerable<string> rows)
        => string.Join(Environment.NewLine, rows);
}
