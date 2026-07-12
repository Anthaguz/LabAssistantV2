using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

internal readonly record struct TemplatesBuilderDirectoryTopologyProjection(
    IReadOnlyList<TemplatesBuilderForestTopologyProjection> Forests,
    IReadOnlyList<TemplatesBuilderTopologyEdgeProjection> Edges,
    TemplatesBuilderStandaloneContainerProjection? Standalone,
    IReadOnlyList<TemplatesBuilderTrustEdgeProjection> TrustEdges);

/// <summary>
/// The Level 1 "Standalone" container: a gray, dashed box shown next to the forests that holds every machine
/// whose domain is unset (a router, a root CA, any workgroup box). It is only present when the draft actually
/// has standalone machines; opening it zooms to Level 2 and lists those machines the same way a domain does.
/// </summary>
internal readonly record struct TemplatesBuilderStandaloneContainerProjection(
    string NodeId,
    string Label,
    int MachineCount,
    bool IsSelected);

internal readonly record struct TemplatesBuilderForestTopologyProjection(
    string NodeId,
    int ForestIndex,
    string ForestId,
    string RootDomainId,
    string Label,
    bool CanSelect,
    bool IsSelected,
    IReadOnlyList<TemplatesBuilderDomainTopologyNodeProjection> RootNodes);

internal readonly record struct TemplatesBuilderDomainTopologyNodeProjection(
    string NodeId,
    int DomainIndex,
    string DomainId,
    string Label,
    string RelationLabel,
    int Depth,
    bool IsRootDomain,
    bool IsTreeRoot,
    bool HasMissingParent,
    bool IsSelected,
    IReadOnlyList<TemplatesBuilderDomainTopologyNodeProjection> Children,
    string Subnet);

internal readonly record struct TemplatesBuilderTopologyEdgeProjection(
    string EdgeId,
    string SourceNodeId,
    string TargetNodeId,
    string EdgeKind);

internal static class TemplatesBuilderDirectoryTopologyProjector
{
    public static TemplatesBuilderDirectoryTopologyProjection Project(
        TemplatesBuilderDraftSnapshot draft,
        BuilderForestDomainResourceKind selectedKind,
        int selectedIndex)
    {
        var forestProjections = new List<TemplatesBuilderForestTopologyProjection>(draft.Forests.Count);
        var edges = new List<TemplatesBuilderTopologyEdgeProjection>();
        // Each domain owns exactly one switch under the one-switch-per-domain model; surface that switch's
        // subnet on the domain node so the CIDR the reconciler auto-allocated is visible on the canvas.
        var subnetByDomainId = draft.LabNetworks
            .Where(network => !string.IsNullOrWhiteSpace(network.DomainId) && !string.IsNullOrWhiteSpace(network.Subnet))
            .GroupBy(network => network.DomainId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Subnet.Trim(), StringComparer.OrdinalIgnoreCase);
        var indexedDomains = draft.Domains
            .Select((domain, index) => new IndexedDomain(index, domain))
            .ToList();
        var domainsByForest = indexedDomains
            .GroupBy(item => item.Domain.ForestId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var forestIds = draft.Forests
            .Select(forest => forest.ForestId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (forest, forestIndex) in draft.Forests.Select((forest, index) => (forest, index)))
        {
            domainsByForest.TryGetValue(forest.ForestId, out var domains);
            domains ??= [];
            var forestNodeId = CreateForestNodeId(forest, forestIndex);
            // The forest renders as an enclosing frame around its domain nodes, so it emits no forest->domain
            // edge; only the parent-child domain edges (added inside BuildForestRoots) connect the nodes.
            var rootNodes = BuildForestRoots(forest, domains, selectedKind, selectedIndex, edges, subnetByDomainId);

            forestProjections.Add(new TemplatesBuilderForestTopologyProjection(
                forestNodeId,
                forestIndex,
                forest.ForestId,
                forest.RootDomainId,
                FormatForestLabel(forest, domains, forestIndex),
                true,
                selectedKind == BuilderForestDomainResourceKind.Forest && selectedIndex == forestIndex,
                rootNodes));
        }

        var unassignedDomains = indexedDomains
            .Where(item => !forestIds.Contains(item.Domain.ForestId))
            .ToList();
        if (unassignedDomains.Count > 0)
        {
            var unassignedForest = new TemplatesBuilderForestDraft("__unassigned__", string.Empty);
            var forestNodeId = "forest:unassigned-domains";
            var rootNodes = BuildForestRoots(unassignedForest, unassignedDomains, selectedKind, selectedIndex, edges, subnetByDomainId);

            forestProjections.Add(new TemplatesBuilderForestTopologyProjection(
                forestNodeId,
                -1,
                string.Empty,
                string.Empty,
                "Unassigned domains",
                false,
                false,
                rootNodes));
        }

        return new TemplatesBuilderDirectoryTopologyProjection(
            forestProjections,
            edges,
            ProjectStandaloneContainer(draft, selectedKind),
            ProjectTrustEdges(draft));
    }

    /// <summary>
    /// Projects each authored forest trust as a single frame-to-frame edge. A trust is anchored on two forests'
    /// root domain ids; this maps each endpoint to its owning forest, then to that forest's node id (the same id
    /// the frame uses), so the canvas can draw the dashed connector between the two green boxes. A trust whose
    /// endpoints do not resolve to two distinct rendered forests is skipped (nothing to connect).
    /// </summary>
    private static IReadOnlyList<TemplatesBuilderTrustEdgeProjection> ProjectTrustEdges(TemplatesBuilderDraftSnapshot draft)
    {
        var trusts = draft.Trusts;
        if (trusts is not { Count: > 0 })
        {
            return [];
        }

        // Root domain id -> forest node id, using the same id helper the frame is built from.
        var forestNodeIdByRoot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (forest, forestIndex) in draft.Forests.Select((forest, index) => (forest, index)))
        {
            var root = forest.RootDomainId?.Trim();
            if (!string.IsNullOrWhiteSpace(root) && !forestNodeIdByRoot.ContainsKey(root))
            {
                forestNodeIdByRoot[root] = CreateForestNodeId(forest, forestIndex);
            }
        }

        var trustEdges = new List<TemplatesBuilderTrustEdgeProjection>(trusts.Count);
        foreach (var trust in trusts)
        {
            var source = trust.SourceDomainId?.Trim() ?? string.Empty;
            var target = trust.TargetDomainId?.Trim() ?? string.Empty;
            if (!forestNodeIdByRoot.TryGetValue(source, out var sourceNodeId) ||
                !forestNodeIdByRoot.TryGetValue(target, out var targetNodeId) ||
                string.Equals(sourceNodeId, targetNodeId, StringComparison.Ordinal))
            {
                continue;
            }

            var trustEdgeId = string.IsNullOrWhiteSpace(trust.TrustId)
                ? $"trust:{sourceNodeId}->{targetNodeId}"
                : $"trust:{trust.TrustId.Trim()}";
            trustEdges.Add(new TemplatesBuilderTrustEdgeProjection(trustEdgeId, sourceNodeId, targetNodeId));
        }

        return trustEdges;
    }

    /// <summary>
    /// Emits the Level 1 Standalone container when the draft has at least one standalone machine (a machine
    /// whose membership mode is Standalone). Returns null otherwise so the container only appears when it has
    /// something to hold.
    /// </summary>
    private static TemplatesBuilderStandaloneContainerProjection? ProjectStandaloneContainer(
        TemplatesBuilderDraftSnapshot draft,
        BuilderForestDomainResourceKind selectedKind)
    {
        var machineCount = draft.Vms.Count(vm =>
            V2MembershipModeCatalog.IsStandalone(vm.MembershipMode) ||
            (string.IsNullOrWhiteSpace(vm.DomainId) && !vm.IsActiveDirectoryDomainController));
        if (machineCount == 0)
        {
            return null;
        }

        return new TemplatesBuilderStandaloneContainerProjection(
            TemplatesBuilderMachineProjector.StandaloneContainerNodeId,
            "Standalone",
            machineCount,
            selectedKind == BuilderForestDomainResourceKind.Standalone);
    }

    private static IReadOnlyList<TemplatesBuilderDomainTopologyNodeProjection> BuildForestRoots(
        TemplatesBuilderForestDraft forest,
        IReadOnlyList<IndexedDomain> domains,
        BuilderForestDomainResourceKind selectedKind,
        int selectedIndex,
        List<TemplatesBuilderTopologyEdgeProjection> edges,
        IReadOnlyDictionary<string, string> subnetByDomainId)
    {
        var byParent = domains
            .Where(item => !string.IsNullOrWhiteSpace(item.Domain.ParentDomainId))
            .GroupBy(item => item.Domain.ParentDomainId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var domainIds = domains
            .Select(item => item.Domain.DomainId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rootCandidates = domains
            .Where(item => IsRootCandidate(forest, item.Domain, domainIds))
            .OrderBy(item => GetRootSortOrder(forest, item.Domain))
            .ThenBy(item => item.Index)
            .ToList();

        return rootCandidates
            .Select(item => BuildDomainNode(item, forest, byParent, domainIds, selectedKind, selectedIndex, edges, subnetByDomainId, 0))
            .ToList();
    }

    private static TemplatesBuilderDomainTopologyNodeProjection BuildDomainNode(
        IndexedDomain item,
        TemplatesBuilderForestDraft forest,
        IReadOnlyDictionary<string, List<IndexedDomain>> byParent,
        ISet<string> domainIds,
        BuilderForestDomainResourceKind selectedKind,
        int selectedIndex,
        List<TemplatesBuilderTopologyEdgeProjection> edges,
        IReadOnlyDictionary<string, string> subnetByDomainId,
        int depth)
    {
        var nodeId = CreateDomainNodeId(item.Domain, item.Index);
        var children = new List<TemplatesBuilderDomainTopologyNodeProjection>();
        if (!string.IsNullOrWhiteSpace(item.Domain.DomainId) &&
            byParent.TryGetValue(item.Domain.DomainId, out var childDomains))
        {
            foreach (var child in childDomains.OrderBy(child => child.Index))
            {
                var childNode = BuildDomainNode(child, forest, byParent, domainIds, selectedKind, selectedIndex, edges, subnetByDomainId, depth + 1);
                edges.Add(new TemplatesBuilderTopologyEdgeProjection(
                    $"edge:{nodeId}->{childNode.NodeId}",
                    nodeId,
                    childNode.NodeId,
                    "ParentChildDomain"));
                children.Add(childNode);
            }
        }

        var relationKind = NormalizeRelationKind(item.Domain.RelationKind);
        var isRootDomain = string.Equals(item.Domain.DomainId, forest.RootDomainId, StringComparison.OrdinalIgnoreCase) ||
            relationKind == V2DomainRelationKind.Root;
        var isTreeRoot = relationKind == V2DomainRelationKind.Tree;
        var hasMissingParent = relationKind == V2DomainRelationKind.Child &&
            !string.IsNullOrWhiteSpace(item.Domain.ParentDomainId) &&
            !domainIds.Contains(item.Domain.ParentDomainId);

        var subnet = !string.IsNullOrWhiteSpace(item.Domain.DomainId) &&
            subnetByDomainId.TryGetValue(item.Domain.DomainId, out var domainSubnet)
                ? domainSubnet
                : string.Empty;

        return new TemplatesBuilderDomainTopologyNodeProjection(
            nodeId,
            item.Index,
            item.Domain.DomainId,
            FormatDomainLabel(item.Domain, item.Index),
            isRootDomain ? "Root domain" : isTreeRoot ? "Tree root" : "Child domain",
            depth,
            isRootDomain,
            isTreeRoot,
            hasMissingParent,
            selectedKind == BuilderForestDomainResourceKind.Domain && selectedIndex == item.Index,
            children,
            subnet);
    }

    private static bool IsRootCandidate(
        TemplatesBuilderForestDraft forest,
        TemplatesBuilderDomainDraft domain,
        ISet<string> domainIds)
    {
        var relationKind = NormalizeRelationKind(domain.RelationKind);
        return relationKind == V2DomainRelationKind.Root ||
            relationKind == V2DomainRelationKind.Tree ||
            string.Equals(domain.DomainId, forest.RootDomainId, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(domain.ParentDomainId) ||
            !domainIds.Contains(domain.ParentDomainId);
    }

    private static int GetRootSortOrder(TemplatesBuilderForestDraft forest, TemplatesBuilderDomainDraft domain)
    {
        if (string.Equals(domain.DomainId, forest.RootDomainId, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return NormalizeRelationKind(domain.RelationKind) switch
        {
            V2DomainRelationKind.Root => 1,
            V2DomainRelationKind.Tree => 2,
            _ => 3
        };
    }

    private static V2DomainRelationKind NormalizeRelationKind(string value)
        => Enum.TryParse<V2DomainRelationKind>(value, ignoreCase: true, out var relationKind)
            ? relationKind
            : V2DomainRelationKind.Root;

    private static string CreateForestNodeId(TemplatesBuilderForestDraft forest, int index)
        => $"forest:{FormatStableSegment(forest.ForestId, index)}";

    private static string CreateDomainNodeId(TemplatesBuilderDomainDraft domain, int index)
        => $"domain:{FormatStableSegment(domain.DomainId, index)}";

    private static string FormatStableSegment(string value, int index)
        => string.IsNullOrWhiteSpace(value) ? $"draft-{index}" : value.Trim();

    private static string FormatForestLabel(
        TemplatesBuilderForestDraft forest,
        IReadOnlyList<IndexedDomain> domains,
        int index)
    {
        // The forest name follows its root domain's DNS name (the forest is named for its root domain);
        // ResolveForestName falls back to the forest id, then we fall back to an ordinal label.
        var resolved = TemplatesBuilderTopologyAuthoring.ResolveForestName(
            domains.Select(item => item.Domain).ToList(),
            forest);
        return string.IsNullOrWhiteSpace(resolved) ? $"Forest {index + 1}" : resolved;
    }

    private static string FormatDomainLabel(TemplatesBuilderDomainDraft domain, int index)
        => !string.IsNullOrWhiteSpace(domain.DnsName)
            ? domain.DnsName.Trim()
            : string.IsNullOrWhiteSpace(domain.DomainId)
                ? $"Domain {index + 1}"
                : domain.DomainId.Trim();

    private readonly record struct IndexedDomain(int Index, TemplatesBuilderDomainDraft Domain);
}
