namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// Pure geometry helpers for the directory-topology canvas. Kept free of any WinUI type so the layout
/// and edge math can be unit-tested without a XAML runtime.
/// </summary>
internal static class BuilderCanvasGeometry
{
    /// <summary>
    /// Returns the point on the boundary of the axis-aligned rectangle (top-left <paramref name="x"/>,
    /// <paramref name="y"/> and size <paramref name="width"/> x <paramref name="height"/>) that lies on
    /// the ray from the rectangle center toward (<paramref name="towardX"/>, <paramref name="towardY"/>).
    /// This lets an edge terminate on the visible edge of a node box instead of at its center, so the
    /// connector reads as pointing at the box rather than through it.
    /// </summary>
    public static (double X, double Y) EdgePoint(
        double x,
        double y,
        double width,
        double height,
        double towardX,
        double towardY)
    {
        var centerX = x + (width / 2);
        var centerY = y + (height / 2);
        var deltaX = towardX - centerX;
        var deltaY = towardY - centerY;

        const double epsilon = 1e-9;
        if (Math.Abs(deltaX) < epsilon && Math.Abs(deltaY) < epsilon)
        {
            return (centerX, centerY);
        }

        var halfWidth = width / 2;
        var halfHeight = height / 2;
        var scaleX = Math.Abs(deltaX) < epsilon ? double.PositiveInfinity : halfWidth / Math.Abs(deltaX);
        var scaleY = Math.Abs(deltaY) < epsilon ? double.PositiveInfinity : halfHeight / Math.Abs(deltaY);
        var scale = Math.Min(scaleX, scaleY);

        return (centerX + (deltaX * scale), centerY + (deltaY * scale));
    }
}
