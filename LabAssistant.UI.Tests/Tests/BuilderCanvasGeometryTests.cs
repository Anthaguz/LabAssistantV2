using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Covers the pure geometry and layout math behind the directory-topology canvas. These run without a XAML
/// host because the geometry and layout helpers deliberately traffic only in plain doubles.
/// </summary>
public sealed class BuilderCanvasGeometryTests
{
    private const double NodeWidth = 190;
    private const double NodeHeight = 60;
    private const double GapX = 36;
    private const double GapY = 54;
    private const double Margin = 28;

    [Fact]
    public void EdgePoint_TowardTheRight_LandsOnTheRightEdgeMidpoint()
    {
        var (x, y) = BuilderCanvasGeometry.EdgePoint(0, 0, 100, 40, towardX: 1000, towardY: 20);

        Assert.Equal(100, x, 6);
        Assert.Equal(20, y, 6);
    }

    [Fact]
    public void EdgePoint_TowardStraightUp_LandsOnTheTopEdgeMidpoint()
    {
        var (x, y) = BuilderCanvasGeometry.EdgePoint(0, 0, 100, 40, towardX: 50, towardY: -1000);

        Assert.Equal(50, x, 6);
        Assert.Equal(0, y, 6);
    }

    [Fact]
    public void EdgePoint_TowardBelowAndRight_ClampsToTheNearerBottomEdge()
    {
        var (x, y) = BuilderCanvasGeometry.EdgePoint(0, 0, 100, 40, towardX: 1000, towardY: 1000);

        // The vertical half-extent is reached first, so the point sits on the bottom edge.
        Assert.Equal(40, y, 6);
        Assert.InRange(x, 50, 100);
    }

    [Fact]
    public void EdgePoint_TowardOwnCenter_ReturnsCenter()
    {
        var (x, y) = BuilderCanvasGeometry.EdgePoint(10, 20, 100, 40, towardX: 60, towardY: 40);

        Assert.Equal(60, x, 6);
        Assert.Equal(40, y, 6);
    }

    [Fact]
    public void Layout_PlacesRootDomainsOnTheTopRowWithChildrenOneRowBelow()
    {
        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(
            CreateTopologyDraft(),
            BuilderForestDomainResourceKind.Forest,
            selectedIndex: 0);

        var positions = BuilderTopologyCanvasLayout.Compute(projection, NodeWidth, NodeHeight, GapX, GapY, Margin, Margin);

        var rowHeight = NodeHeight + GapY;
        // Forests are frames, not nodes, so no forest position is emitted; the top domain row is pushed down by
        // the frame's top allowance (padding + header pill) so the enclosing frame stays on-canvas.
        var topRowY = Margin + BuilderCanvasMetrics.FrameTopAllowance;
        Assert.False(positions.ContainsKey("forest:forest-contoso"));
        Assert.Equal(topRowY, positions["domain:domain-contoso"].Y, 6);
        Assert.Equal(topRowY + rowHeight, positions["domain:domain-child"].Y, 6);
        Assert.Equal(topRowY, positions["domain:domain-tree"].Y, 6);
    }

    [Fact]
    public void Layout_CentersAnInternalDomainOverItsSingleChild()
    {
        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(
            CreateTopologyDraft(),
            BuilderForestDomainResourceKind.Forest,
            selectedIndex: 0);

        var positions = BuilderTopologyCanvasLayout.Compute(projection, NodeWidth, NodeHeight, GapX, GapY, Margin, Margin);

        // domain-contoso has exactly one child (domain-child), so it must be centered directly over it.
        Assert.Equal(positions["domain:domain-child"].X, positions["domain:domain-contoso"].X, 6);
    }

    [Fact]
    public void Layout_ReturnsAPositionForEveryDomainAndNoForest()
    {
        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(
            CreateTopologyDraft(),
            BuilderForestDomainResourceKind.Forest,
            selectedIndex: 0);

        var positions = BuilderTopologyCanvasLayout.Compute(projection, NodeWidth, NodeHeight, GapX, GapY, Margin, Margin);

        foreach (var nodeId in new[]
                 {
                     "domain:domain-contoso",
                     "domain:domain-child",
                     "domain:domain-tree",
                     "domain:domain-fabrikam"
                 })
        {
            Assert.True(positions.ContainsKey(nodeId), $"missing layout position for {nodeId}");
        }

        // Forests are frames sized by the canvas view model, so the layout must not position them as nodes.
        Assert.False(positions.ContainsKey("forest:forest-contoso"));
        Assert.False(positions.ContainsKey("forest:forest-fabrikam"));
    }

    private static TemplatesBuilderDraftSnapshot CreateTopologyDraft()
        => new(
            "Topology draft",
            "Draft for canvas layout tests.",
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
}
