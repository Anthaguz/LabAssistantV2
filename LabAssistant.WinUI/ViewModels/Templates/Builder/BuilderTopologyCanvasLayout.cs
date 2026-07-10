namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>Top-left position of a node in canvas coordinates.</summary>
internal readonly record struct BuilderCanvasNodePosition(double X, double Y);

/// <summary>
/// Computes an initial tidy-tree layout for the directory-topology projection. Each forest owns a band of
/// leaf columns; a forest node sits above its root domains, every domain sits one level below its parent,
/// and an internal node is centered over its children. A single global leaf cursor advances left to right
/// across every forest so bands never overlap. The result is deterministic, so it is unit-testable, and it
/// is only a starting position: once the user drags a node, the owning canvas view model pins that node and
/// this layout no longer moves it.
/// </summary>
internal static class BuilderTopologyCanvasLayout
{
    public static IReadOnlyDictionary<string, BuilderCanvasNodePosition> Compute(
        TemplatesBuilderDirectoryTopologyProjection projection,
        double nodeWidth,
        double nodeHeight,
        double gapX,
        double gapY,
        double marginX,
        double marginY)
    {
        var positions = new Dictionary<string, BuilderCanvasNodePosition>(StringComparer.Ordinal);
        var slotWidth = nodeWidth + gapX;
        var rowHeight = nodeHeight + gapY;
        var leafCursor = 0;

        double NextLeafCenter() => marginX + (leafCursor++ * slotWidth) + (nodeWidth / 2);

        // Places a domain subtree and returns the node center X so the parent can center over it.
        double PlaceDomain(TemplatesBuilderDomainTopologyNodeProjection domain)
        {
            // Forest sits at level 0, so a domain at projection depth d lives at level d + 1.
            var y = marginY + ((domain.Depth + 1) * rowHeight);
            double centerX;
            if (domain.Children.Count == 0)
            {
                centerX = NextLeafCenter();
            }
            else
            {
                var childCenters = domain.Children.Select(PlaceDomain).ToList();
                centerX = childCenters.Average();
            }

            positions[domain.NodeId] = new BuilderCanvasNodePosition(centerX - (nodeWidth / 2), y);
            return centerX;
        }

        foreach (var forest in projection.Forests)
        {
            var rootCenters = forest.RootNodes.Select(PlaceDomain).ToList();
            var forestCenterX = rootCenters.Count > 0 ? rootCenters.Average() : NextLeafCenter();
            positions[forest.NodeId] = new BuilderCanvasNodePosition(forestCenterX - (nodeWidth / 2), marginY);
        }

        // The Standalone container is a peer of the forests: give it the next free leaf column at the top row,
        // so it sits to the right of every forest band without overlapping them.
        if (projection.Standalone is { } standalone)
        {
            var centerX = NextLeafCenter();
            positions[standalone.NodeId] = new BuilderCanvasNodePosition(centerX - (nodeWidth / 2), marginY);
        }

        return positions;
    }
}
