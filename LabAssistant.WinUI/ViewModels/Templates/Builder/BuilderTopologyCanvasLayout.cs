namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>Top-left position of a node in canvas coordinates.</summary>
internal readonly record struct BuilderCanvasNodePosition(double X, double Y);

/// <summary>
/// Computes an initial tidy-tree layout for the directory-topology projection. Domains are the nodes: every
/// domain sits one level below its parent and an internal node is centered over its children. Forests are not
/// nodes - each forest is drawn as an enclosing frame the canvas view model sizes around these domain
/// positions - so this layout only positions domains (and the Standalone box), leaving the top row a
/// <see cref="BuilderCanvasMetrics.FrameTopAllowance"/> gap for the frame border and header pill. A single
/// pixel cursor advances left to right, inserting <see cref="BuilderCanvasMetrics.ForestGap"/> between bands
/// so adjacent forest frames never overlap. The result is deterministic, so it is unit-testable, and it is
/// only a starting position: once the user drags a node the owning canvas view model pins it and this layout
/// no longer moves it.
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
        // Leave room above the top domain row for the enclosing frame's top padding and its header pill.
        var topRowY = marginY + BuilderCanvasMetrics.FrameTopAllowance;
        var xCursor = marginX;

        double NextLeafCenter()
        {
            var center = xCursor + (nodeWidth / 2);
            xCursor += slotWidth;
            return center;
        }

        // Places a domain subtree and returns the node center X so the parent can center over it.
        double PlaceDomain(TemplatesBuilderDomainTopologyNodeProjection domain)
        {
            var y = topRowY + (domain.Depth * rowHeight);
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

        var firstBand = true;
        foreach (var forest in projection.Forests)
        {
            if (forest.RootNodes.Count == 0)
            {
                continue;
            }

            if (!firstBand)
            {
                xCursor += BuilderCanvasMetrics.ForestGap;
            }

            firstBand = false;
            foreach (var root in forest.RootNodes)
            {
                PlaceDomain(root);
            }
        }

        // The Standalone box is a peer of the forests: give it the next free column on the top domain row so it
        // sits to the right of every forest band, past the inter-band gap, without overlapping a frame.
        if (projection.Standalone is { } standalone)
        {
            if (!firstBand)
            {
                xCursor += BuilderCanvasMetrics.ForestGap;
            }

            var centerX = NextLeafCenter();
            positions[standalone.NodeId] = new BuilderCanvasNodePosition(centerX - (nodeWidth / 2), topRowY);
        }

        return positions;
    }
}
