using System.Drawing;
using System.Drawing.Imaging;
using LabAssistant.WinUI.ViewModels.Templates.Builder;

namespace LabAssistant.UI.Tests.Tests.Rendering;

/// <summary>
/// Renders a <see cref="BuilderTopologyCanvasViewModel"/> to a PNG using the exact node and edge geometry the
/// running canvas binds to. This is a developer aid, not an assertion: because a WinUI 3 surface cannot be
/// rendered headless, this draws the same positioned boxes and connectors the production layout and geometry
/// produce so layout regressions (overlaps, mis-centered parents, off-canvas nodes, mis-routed edges) are
/// visible without launching the app. It is Windows-only (GDI+) and used only by the env-gated snapshot test.
/// </summary>
internal static class BuilderCanvasSnapshotRenderer
{
    private static readonly Color Background = Color.FromArgb(15, 24, 48);
    private static readonly Color ForestFill = Color.FromArgb(22, 42, 32);
    private static readonly Color ForestBorder = Color.FromArgb(96, 176, 128);
    private static readonly Color DomainFill = Color.FromArgb(27, 42, 74);
    private static readonly Color DomainBorder = Color.FromArgb(92, 128, 196);
    private static readonly Color SelectedFill = Color.FromArgb(38, 58, 96);
    private static readonly Color AccentBorder = Color.FromArgb(106, 160, 255);
    private static readonly Color LabelColor = Color.FromArgb(226, 234, 252);
    private static readonly Color SubtextColor = Color.FromArgb(150, 166, 200);
    private static readonly Color MissingColor = Color.FromArgb(240, 120, 120);
    private static readonly Color ForestEdge = Color.FromArgb(120, 140, 176);
    private static readonly Color DomainEdge = Color.FromArgb(84, 96, 128);

    public static void Render(BuilderTopologyCanvasViewModel canvas, string caption, string outputPath)
    {
        var width = (int)Math.Ceiling(canvas.CanvasWidth);
        var height = (int)Math.Ceiling(canvas.CanvasHeight) + 28;

        using var bitmap = new Bitmap(Math.Max(1, width), Math.Max(1, height));
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            graphics.Clear(Background);

            using var captionFont = new Font("Segoe UI", 9, FontStyle.Italic);
            using var captionBrush = new SolidBrush(SubtextColor);
            graphics.DrawString(caption, captionFont, captionBrush, 8, 6);
            graphics.TranslateTransform(0, 26);

            DrawEdges(graphics, canvas);
            DrawNodes(graphics, canvas);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        bitmap.Save(outputPath, ImageFormat.Png);
    }

    private static void DrawEdges(Graphics graphics, BuilderTopologyCanvasViewModel canvas)
    {
        foreach (var edge in canvas.Edges)
        {
            using var pen = new Pen(edge.IsForestRoot ? ForestEdge : DomainEdge, 1.6f);
            graphics.DrawLine(pen, (float)edge.X1, (float)edge.Y1, (float)edge.X2, (float)edge.Y2);
        }
    }

    private static void DrawNodes(Graphics graphics, BuilderTopologyCanvasViewModel canvas)
    {
        using var labelFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var subFont = new Font("Segoe UI", 8f);

        foreach (var node in canvas.Nodes)
        {
            var rect = new RectangleF((float)node.X, (float)node.Y, (float)node.Width, (float)node.Height);
            var fill = node.IsSelected ? SelectedFill : node.IsForest ? ForestFill : DomainFill;
            var border = node.IsAccent ? AccentBorder : node.IsForest ? ForestBorder : DomainBorder;

            using (var fillBrush = new SolidBrush(fill))
            {
                graphics.FillRectangle(fillBrush, rect);
            }

            using (var borderPen = new Pen(border, node.IsAccent ? 2.2f : 1.2f))
            {
                graphics.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            using var labelBrush = new SolidBrush(LabelColor);
            using var subBrush = new SolidBrush(node.HasMissingParent ? MissingColor : SubtextColor);
            graphics.DrawString(node.Label, labelFont, labelBrush, rect.X + 10, rect.Y + 8);
            graphics.DrawString(node.Subtext, subFont, subBrush, rect.X + 10, rect.Y + 30);
        }
    }
}
