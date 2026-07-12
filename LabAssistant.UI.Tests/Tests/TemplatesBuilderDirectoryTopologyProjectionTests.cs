using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class TemplatesBuilderDirectoryTopologyProjectionTests
{
    [Fact]
    public void DirectoryTopologyProjection_HighlightsForestRootDomain()
    {
        var draft = CreateTopologyDraft();

        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(draft, BuilderForestDomainResourceKind.Domain, selectedIndex: 0);

        var forest = Assert.Single(projection.Forests, forest => forest.ForestId == "forest-contoso");
        var root = Assert.Single(forest.RootNodes, node => node.DomainId == "domain-contoso");
        Assert.Equal("forest:forest-contoso", forest.NodeId);
        Assert.Equal("domain:domain-contoso", root.NodeId);
        Assert.True(root.IsRootDomain);
        Assert.False(root.IsTreeRoot);
        Assert.True(root.IsSelected);
        // The forest is now an enclosing frame, not a node, so it emits no forest->root-domain edge.
        Assert.DoesNotContain(projection.Edges, edge => edge.EdgeKind == "ForestDomainRoot");
    }

    [Fact]
    public void DirectoryTopologyProjection_NestsChildDomainsUnderParents()
    {
        var draft = CreateTopologyDraft();

        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(draft, BuilderForestDomainResourceKind.Domain, selectedIndex: 1);

        var root = Assert.Single(Assert.Single(projection.Forests, forest => forest.ForestId == "forest-contoso").RootNodes, node => node.DomainId == "domain-contoso");
        var child = Assert.Single(root.Children);
        Assert.Equal("domain:domain-child", child.NodeId);
        Assert.Equal(1, child.Depth);
        Assert.Equal("Child domain", child.RelationLabel);
        Assert.True(child.IsSelected);
        Assert.Contains(projection.Edges, edge =>
            edge.EdgeKind == "ParentChildDomain" &&
            edge.SourceNodeId == "domain:domain-contoso" &&
            edge.TargetNodeId == "domain:domain-child");
    }

    [Fact]
    public void DirectoryTopologyProjection_KeepsTreeDomainRootsSeparateWithinForest()
    {
        var draft = CreateTopologyDraft();

        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(draft, BuilderForestDomainResourceKind.Domain, selectedIndex: 2);

        var forest = Assert.Single(projection.Forests, forest => forest.ForestId == "forest-contoso");
        Assert.Equal(2, forest.RootNodes.Count);
        var treeRoot = Assert.Single(forest.RootNodes, node => node.DomainId == "domain-tree");
        Assert.True(treeRoot.IsTreeRoot);
        Assert.Equal(0, treeRoot.Depth);
        Assert.True(treeRoot.IsSelected);
    }

    [Fact]
    public void DirectoryTopologyProjection_ForestLabelFollowsRootDomainDnsName()
    {
        var draft = CreateTopologyDraft();

        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(draft, BuilderForestDomainResourceKind.Forest, selectedIndex: 0);

        // The forest is named for its root domain, so its label must be the root domain DNS name, not the forest id.
        var contoso = Assert.Single(projection.Forests, forest => forest.ForestId == "forest-contoso");
        Assert.Equal("contoso.com", contoso.Label);
        var fabrikam = Assert.Single(projection.Forests, forest => forest.ForestId == "forest-fabrikam");
        Assert.Equal("fabrikam.com", fabrikam.Label);
    }

    [Fact]
    public void DirectoryTopologyProjection_RendersSeparateForestsAsSeparateContainers()
    {
        var draft = CreateTopologyDraft();

        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(draft, BuilderForestDomainResourceKind.Forest, selectedIndex: 1);

        Assert.Equal(2, projection.Forests.Count);
        Assert.Contains(projection.Forests, forest => forest.NodeId == "forest:forest-contoso" && !forest.IsSelected);
        Assert.Contains(projection.Forests, forest => forest.NodeId == "forest:forest-fabrikam" && forest.IsSelected);
        var fabrikamRoot = Assert.Single(Assert.Single(projection.Forests, forest => forest.ForestId == "forest-fabrikam").RootNodes);
        Assert.Equal("domain:domain-fabrikam", fabrikamRoot.NodeId);
        Assert.True(fabrikamRoot.IsRootDomain);
    }

    [Fact]
    public void DirectoryTopologyProjection_KeepsDraftVisibleWhenRootOrParentReferencesAreMissing()
    {
        var draft = new TemplatesBuilderDraftSnapshot(
            "Missing references",
            "Draft keeps invalid topology rows visible.",
            "Balanced",
            [],
            [],
            [new TemplatesBuilderForestDraft("forest-lab", "domain-missing-root")],
            [
                new TemplatesBuilderDomainDraft("domain-orphan", "orphan.lab", "ORPHAN", "forest-lab", nameof(V2DomainRelationKind.Child), "domain-missing-parent"),
                new TemplatesBuilderDomainDraft("domain-unassigned", "unassigned.lab", "UNASSIGNED", "forest-missing", nameof(V2DomainRelationKind.Root), string.Empty)
            ],
            [],
            false);

        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(draft, BuilderForestDomainResourceKind.Domain, selectedIndex: 0);

        var forest = Assert.Single(projection.Forests, forest => forest.ForestId == "forest-lab");
        var orphan = Assert.Single(forest.RootNodes);
        Assert.Equal("domain:domain-orphan", orphan.NodeId);
        Assert.True(orphan.HasMissingParent);
        Assert.Equal("Child domain", orphan.RelationLabel);
        Assert.True(orphan.IsSelected);

        var unassignedForest = Assert.Single(projection.Forests, forest => forest.NodeId == "forest:unassigned-domains");
        Assert.False(unassignedForest.CanSelect);
        var unassignedDomain = Assert.Single(unassignedForest.RootNodes);
        Assert.Equal("domain:domain-unassigned", unassignedDomain.NodeId);
    }

    [Fact]
    public void DirectoryTopologyProjection_NodeIdsRemainStableWhenDraftOrderChanges()
    {
        var draft = CreateTopologyDraft();
        var reorderedDraft = draft with
        {
            Domains = [draft.Domains[2], draft.Domains[0], draft.Domains[1], draft.Domains[3]]
        };

        var original = TemplatesBuilderDirectoryTopologyProjector.Project(draft, BuilderForestDomainResourceKind.Domain, selectedIndex: 0);
        var reordered = TemplatesBuilderDirectoryTopologyProjector.Project(reorderedDraft, BuilderForestDomainResourceKind.Domain, selectedIndex: 1);

        Assert.Contains(Flatten(original), node => node.NodeId == "domain:domain-contoso");
        Assert.Contains(Flatten(reordered), node => node.NodeId == "domain:domain-contoso");
        Assert.Contains(original.Edges, edge => edge.EdgeId == "edge:domain:domain-contoso->domain:domain-child");
        Assert.Contains(reordered.Edges, edge => edge.EdgeId == "edge:domain:domain-contoso->domain:domain-child");
    }

    private static TemplatesBuilderDraftSnapshot CreateTopologyDraft()
        => new(
            "Topology draft",
            "Draft for topology projection tests.",
            "Balanced",
            [],
            [],
            [
                new TemplatesBuilderForestDraft("forest-contoso", "domain-contoso"),
                new TemplatesBuilderForestDraft("forest-fabrikam", "domain-fabrikam")
            ],
            [
                new TemplatesBuilderDomainDraft("domain-contoso", "contoso.com", "CONTOSO", "forest-contoso", nameof(V2DomainRelationKind.Root), string.Empty),
                new TemplatesBuilderDomainDraft("domain-child", "child.contoso.com", "CHILD", "forest-contoso", nameof(V2DomainRelationKind.Child), "domain-contoso"),
                new TemplatesBuilderDomainDraft("domain-tree", "tailspintoys.com", "TAILSPIN", "forest-contoso", nameof(V2DomainRelationKind.Tree), string.Empty),
                new TemplatesBuilderDomainDraft("domain-fabrikam", "fabrikam.com", "FABRIKAM", "forest-fabrikam", nameof(V2DomainRelationKind.Root), string.Empty)
            ],
            [],
            false);

    [Fact]
    public void DirectoryTopologyProjection_EmitsOneTrustEdgeBetweenTheOwningForestNodes()
    {
        var draft = CreateTopologyDraft() with
        {
            Trusts =
            [
                new TemplatesBuilderTrustDraft(
                    "trust-contoso-fabrikam",
                    "domain-contoso",
                    "domain-fabrikam",
                    nameof(V2TrustType.Forest),
                    nameof(V2TrustDirection.Bidirectional))
            ]
        };

        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(draft, BuilderForestDomainResourceKind.Domain, selectedIndex: 0);

        var edge = Assert.Single(projection.TrustEdges);
        Assert.Equal("trust:trust-contoso-fabrikam", edge.TrustEdgeId);
        Assert.True(
            (edge.SourceForestNodeId == "forest:forest-contoso" && edge.TargetForestNodeId == "forest:forest-fabrikam") ||
            (edge.SourceForestNodeId == "forest:forest-fabrikam" && edge.TargetForestNodeId == "forest:forest-contoso"));
    }

    [Fact]
    public void DirectoryTopologyProjection_SkipsTrustWhoseEndpointsDoNotResolveToTwoForests()
    {
        // A trust whose endpoints land in the same forest (or an unknown domain) cannot draw a frame-to-frame
        // edge, so the projector emits nothing for it.
        var draft = CreateTopologyDraft() with
        {
            Trusts =
            [
                new TemplatesBuilderTrustDraft(
                    "trust-dangling",
                    "domain-contoso",
                    "domain-unknown",
                    nameof(V2TrustType.Forest),
                    nameof(V2TrustDirection.Bidirectional))
            ]
        };

        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(draft, BuilderForestDomainResourceKind.Domain, selectedIndex: 0);

        Assert.Empty(projection.TrustEdges);
    }

    private static IEnumerable<TemplatesBuilderDomainTopologyNodeProjection> Flatten(TemplatesBuilderDirectoryTopologyProjection projection)
        => projection.Forests.SelectMany(forest => Flatten(forest.RootNodes));

    private static IEnumerable<TemplatesBuilderDomainTopologyNodeProjection> Flatten(IEnumerable<TemplatesBuilderDomainTopologyNodeProjection> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }
}
