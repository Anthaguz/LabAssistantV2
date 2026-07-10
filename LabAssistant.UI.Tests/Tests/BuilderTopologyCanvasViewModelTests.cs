using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Covers the directory-topology canvas view model: how it materializes nodes and edges from a projection,
/// routes node taps back to the owning Builder, and keeps drag-pinned positions stable across rebuilds. All
/// runtime-independent, so it runs without a XAML host.
/// </summary>
public sealed class BuilderTopologyCanvasViewModelTests
{
    [Fact]
    public void Build_ProducesOneNodePerForestAndDomainPlusEveryProjectionEdge()
    {
        var canvas = CreateCanvas(CreateTopologyDraft(), out _);

        Assert.True(canvas.HasNodes);
        Assert.Equal(6, canvas.Nodes.Count);
        Assert.Equal(4, canvas.Edges.Count);
        Assert.Contains(canvas.Nodes, node => node.NodeId == "forest:forest-contoso" && node.IsForest);
        Assert.Contains(canvas.Nodes, node => node.NodeId == "domain:domain-child" && !node.IsForest);
    }

    [Fact]
    public void Build_MarksSelectedDomainAsSelectedAndAccented()
    {
        var canvas = CreateCanvas(CreateTopologyDraft(BuilderForestDomainResourceKind.Domain, selectedIndex: 1), out _);

        var child = Assert.Single(canvas.Nodes, node => node.NodeId == "domain:domain-child");
        Assert.True(child.IsSelected);
        Assert.True(child.IsAccent);
    }

    [Fact]
    public void TappingADomainNode_RoutesSelectionWithItsDraftIndex()
    {
        var canvas = CreateCanvas(CreateTopologyDraft(), out var selections);

        var child = Assert.Single(canvas.Nodes, node => node.NodeId == "domain:domain-child");
        child.SelectCommand!.Execute(null);

        var selection = Assert.Single(selections);
        Assert.Equal(BuilderForestDomainResourceKind.Domain, selection.Kind);
        Assert.Equal(1, selection.Index);
    }

    [Fact]
    public void TappingAForestNode_RoutesForestSelection()
    {
        var canvas = CreateCanvas(CreateTopologyDraft(), out var selections);

        var forest = Assert.Single(canvas.Nodes, node => node.NodeId == "forest:forest-fabrikam");
        forest.SelectCommand!.Execute(null);

        var selection = Assert.Single(selections);
        Assert.Equal(BuilderForestDomainResourceKind.Forest, selection.Kind);
        Assert.Equal(1, selection.Index);
    }

    [Fact]
    public void UnassignedPseudoForest_IsNotSelectable()
    {
        var canvas = CreateCanvas(CreateMissingReferenceDraft(), out _);

        var unassigned = Assert.Single(canvas.Nodes, node => node.NodeId == "forest:unassigned-domains");
        Assert.Null(unassigned.SelectCommand);
    }

    [Fact]
    public void MissingParentDomain_SurfacesTheMissingParentSubtext()
    {
        var canvas = CreateCanvas(CreateMissingReferenceDraft(), out _);

        var orphan = Assert.Single(canvas.Nodes, node => node.NodeId == "domain:domain-orphan");
        Assert.True(orphan.HasMissingParent);
        Assert.Equal("Missing parent reference", orphan.Subtext);
    }

    [Fact]
    public void MoveNode_UpdatesPositionAndClampsToTheOrigin()
    {
        var canvas = CreateCanvas(CreateTopologyDraft(), out _);

        canvas.MoveNode("forest:forest-contoso", 480, 260);
        var moved = Assert.Single(canvas.Nodes, node => node.NodeId == "forest:forest-contoso");
        Assert.Equal(480, moved.X, 6);
        Assert.Equal(260, moved.Y, 6);

        canvas.MoveNode("forest:forest-contoso", -50, -20);
        Assert.Equal(0, moved.X, 6);
        Assert.Equal(0, moved.Y, 6);
    }

    [Fact]
    public void MoveNode_RecomputesEveryEdgeTouchingTheMovedNode()
    {
        var canvas = CreateCanvas(CreateTopologyDraft(), out _);
        var edge = Assert.Single(canvas.Edges, e => e.SourceNodeId == "forest:forest-contoso" && e.TargetNodeId == "domain:domain-contoso");
        var beforeX1 = edge.X1;
        var beforeY1 = edge.Y1;

        canvas.MoveNode("forest:forest-contoso", 600, 400);

        Assert.True(Math.Abs(edge.X1 - beforeX1) > 0.5 || Math.Abs(edge.Y1 - beforeY1) > 0.5,
            "the edge endpoint on the moved node should have been recomputed");
    }

    [Fact]
    public void MoveNode_GrowsTheCanvasExtentToFitTheMovedNode()
    {
        var canvas = CreateCanvas(CreateTopologyDraft(), out _);
        var beforeWidth = canvas.CanvasWidth;

        canvas.MoveNode("forest:forest-contoso", beforeWidth + 400, 40);

        Assert.True(canvas.CanvasWidth > beforeWidth, "moving a node past the right edge must grow the canvas width");
    }

    [Fact]
    public void Rebuild_KeepsDraggedNodePinnedButReLaysOutUntouchedNodes()
    {
        var draft = CreateTopologyDraft();
        var canvas = CreateCanvas(draft, out _);
        var fabrikamBefore = Assert.Single(canvas.Nodes, node => node.NodeId == "domain:domain-fabrikam").X;

        canvas.MoveNode("forest:forest-contoso", 777, 333);
        canvas.Rebuild(TemplatesBuilderDirectoryTopologyProjector.Project(draft, BuilderForestDomainResourceKind.Domain, selectedIndex: 0));

        var contosoAfter = Assert.Single(canvas.Nodes, node => node.NodeId == "forest:forest-contoso");
        Assert.Equal(777, contosoAfter.X, 6);
        Assert.Equal(333, contosoAfter.Y, 6);
        var fabrikamAfter = Assert.Single(canvas.Nodes, node => node.NodeId == "domain:domain-fabrikam").X;
        Assert.Equal(fabrikamBefore, fabrikamAfter, 6);
    }

    private static BuilderTopologyCanvasViewModel CreateCanvas(
        TemplatesBuilderDraftSnapshot draft,
        out List<(BuilderForestDomainResourceKind Kind, int Index)> selections)
    {
        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(draft, BuilderForestDomainResourceKind.Forest, selectedIndex: 0);
        var recorded = new List<(BuilderForestDomainResourceKind Kind, int Index)>();
        selections = recorded;
        return new BuilderTopologyCanvasViewModel(projection, (kind, index) => recorded.Add((kind, index)));
    }

    private static BuilderTopologyCanvasViewModel CreateCanvas(
        (TemplatesBuilderDraftSnapshot Draft, BuilderForestDomainResourceKind Kind, int Index) selected,
        out List<(BuilderForestDomainResourceKind Kind, int Index)> selections)
    {
        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(selected.Draft, selected.Kind, selected.Index);
        var recorded = new List<(BuilderForestDomainResourceKind Kind, int Index)>();
        selections = recorded;
        return new BuilderTopologyCanvasViewModel(projection, (kind, index) => recorded.Add((kind, index)));
    }

    private static (TemplatesBuilderDraftSnapshot Draft, BuilderForestDomainResourceKind Kind, int Index) CreateTopologyDraft(
        BuilderForestDomainResourceKind kind,
        int selectedIndex)
        => (CreateTopologyDraft(), kind, selectedIndex);

    private static TemplatesBuilderDraftSnapshot CreateTopologyDraft()
        => new(
            "Topology draft",
            "Draft for canvas view-model tests.",
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

    private static TemplatesBuilderDraftSnapshot CreateMissingReferenceDraft()
        => new(
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
}
