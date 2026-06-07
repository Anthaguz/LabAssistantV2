using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Planning;

internal static class V2DirectoryTopologyResolver
{
    public static V2DirectoryTopologyResolution Resolve(
        LabTemplate template,
        IReadOnlyDictionary<string, V2VmTopologyInfo> vmById,
        List<V2PlanIssue> issues)
    {
        var topology = template.DirectoryTopology;
        if (topology == null)
        {
            issues.Add(new V2PlanIssue
            {
                Severity = V2PlanIssueSeverity.Blocking,
                Code = "directory-topology-required",
                Message = "V2 directory topology is required for AD-core orchestration.",
                SuggestedAction = "Define directoryTopology with at least one root domain and forest."
            });

            return V2DirectoryTopologyResolution.Empty;
        }

        var forests = (topology.Forests ?? [])
            .Where(forest => !string.IsNullOrWhiteSpace(forest.ForestId))
            .GroupBy(forest => forest.ForestId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var domains = (topology.Domains ?? [])
            .Where(domain => !string.IsNullOrWhiteSpace(domain.DomainId))
            .GroupBy(domain => domain.DomainId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var forest in forests.Values)
        {
            if (!domains.ContainsKey(forest.RootDomainId))
            {
                issues.Add(new V2PlanIssue
                {
                    Severity = V2PlanIssueSeverity.Blocking,
                    Code = "forest-root-domain-missing",
                    Message = $"Forest '{forest.ForestId}' references missing root domain '{forest.RootDomainId}'.",
                    SuggestedAction = "Define the root domain in directoryTopology.domains."
                });
            }
        }

        foreach (var domain in domains.Values)
        {
            if (!forests.ContainsKey(domain.ForestId))
            {
                issues.Add(new V2PlanIssue
                {
                    Severity = V2PlanIssueSeverity.Blocking,
                    Code = "domain-forest-missing",
                    Message = $"Domain '{domain.DomainId}' references missing forest '{domain.ForestId}'.",
                    SuggestedAction = "Define the referenced forest in directoryTopology.forests."
                });
                continue;
            }

            if (!vmById.TryGetValue(domain.FirstDomainControllerVmId, out var vm))
            {
                issues.Add(new V2PlanIssue
                {
                    Severity = V2PlanIssueSeverity.Blocking,
                    Code = "domain-first-dc-missing",
                    Message = $"Domain '{domain.DomainId}' references missing VM '{domain.FirstDomainControllerVmId}'.",
                    SuggestedAction = "Point firstDomainControllerVmId to an existing VM."
                });
                continue;
            }

            if (!string.Equals(vm.DomainId, domain.DomainId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new V2PlanIssue
                {
                    Severity = V2PlanIssueSeverity.Blocking,
                    Code = "domain-vm-domain-mismatch",
                    VmId = vm.VmId,
                    VmName = vm.VmName,
                    Message = $"VM '{vm.VmName}' is assigned to domain '{vm.DomainId}', but domain '{domain.DomainId}' points to it as first domain controller.",
                    SuggestedAction = "Align the VM domainId with directoryTopology.domains."
                });
            }

            if (domain.RelationKind == V2DomainRelationKind.Root &&
                !string.Equals(vm.TopologyRole, "RootDomainController", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new V2PlanIssue
                {
                    Severity = V2PlanIssueSeverity.Blocking,
                    VmId = vm.VmId,
                    VmName = vm.VmName,
                    Code = "root-domain-first-dc-role-invalid",
                    Message = $"Root domain '{domain.DomainId}' requires first domain controller '{vm.VmName}' to use topology role 'RootDomainController'.",
                    SuggestedAction = "Set the VM topologyRole to RootDomainController or choose another VM."
                });
            }
        }

        return new V2DirectoryTopologyResolution(
            topology.Forests?.Select(forest => new V2ResolvedForestPlanningContext
            {
                ForestId = forest.ForestId,
                RootDomainId = forest.RootDomainId
            }).ToArray() ?? Array.Empty<V2ResolvedForestPlanningContext>(),
            topology.Domains?.Select(domain => new V2ResolvedDomainPlanningContext
            {
                DomainId = domain.DomainId,
                DnsName = domain.DnsName,
                NetBiosName = domain.NetBiosName,
                ForestId = domain.ForestId,
                RelationKind = domain.RelationKind,
                ParentDomainId = domain.ParentDomainId,
                FirstDomainControllerVmId = domain.FirstDomainControllerVmId
            }).ToArray() ?? Array.Empty<V2ResolvedDomainPlanningContext>(),
            topology.Trusts?.ToArray() ?? Array.Empty<V2TrustTemplate>());
    }
}

internal sealed record V2DirectoryTopologyResolution(
    IReadOnlyList<V2ResolvedForestPlanningContext> Forests,
    IReadOnlyList<V2ResolvedDomainPlanningContext> Domains,
    IReadOnlyList<V2TrustTemplate> Trusts)
{
    public static readonly V2DirectoryTopologyResolution Empty =
        new(Array.Empty<V2ResolvedForestPlanningContext>(), Array.Empty<V2ResolvedDomainPlanningContext>(), Array.Empty<V2TrustTemplate>());

    public V2ResolvedDomainPlanningContext? FindDomain(string? domainId)
        => string.IsNullOrWhiteSpace(domainId)
            ? null
            : Domains.FirstOrDefault(domain => string.Equals(domain.DomainId, domainId, StringComparison.OrdinalIgnoreCase));
}

internal sealed record V2VmTopologyInfo(
    string VmId,
    string VmName,
    string? TopologyRole,
    string? DomainId);
