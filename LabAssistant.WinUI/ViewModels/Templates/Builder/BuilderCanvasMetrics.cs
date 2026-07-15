namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// Shared geometry constants for the directory-topology canvas. A forest is drawn as an enclosing frame
/// auto-sized around the bounding box of its member domain nodes (the domains are the nodes; the forest is
/// the frame), so the layout that positions the domains and the canvas view model that sizes the frame must
/// agree on the padding and header allowance. Keeping the numbers here prevents that pair from drifting apart.
/// </summary>
internal static class BuilderCanvasMetrics
{
    /// <summary>Gap between a forest frame's border and the bounding box of its member domain nodes.</summary>
    public const double FramePadding = 22;

    /// <summary>Height of the forest header pill that straddles the frame's top border.</summary>
    public const double FrameHeaderHeight = 26;

    /// <summary>Horizontal gap inserted between adjacent forest bands so their frames never overlap.</summary>
    public const double ForestGap = 64;

    /// <summary>
    /// Vertical offset applied to the top row of domain nodes so the enclosing frame - which extends
    /// <see cref="FramePadding"/> above its top domain and carries a header pill above that - stays on-canvas.
    /// </summary>
    public const double FrameTopAllowance = FramePadding + FrameHeaderHeight;
}
