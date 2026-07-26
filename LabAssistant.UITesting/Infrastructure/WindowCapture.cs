using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>
/// Captures a top-level window's pixels to a PNG using Win32 <c>PrintWindow</c> with
/// the <c>PW_RENDERFULLCONTENT</c> flag.
///
/// This matters because a WinUI 3 desktop window renders through DirectComposition /
/// a DirectX swap chain, which the default GDI <c>BitBlt</c> path (used by FlaUI's
/// <c>Capture.Element</c>) cannot read - it returns a solid blank image. Asking the
/// window to render its full composed content is the documented way to capture such
/// windows, so harness findings can carry real visual evidence.
/// </summary>
internal static class WindowCapture
{
    // Render the window's full content, including DirectComposition/swap-chain visuals,
    // rather than only what GDI would blit.
    private const uint PW_RENDERFULLCONTENT = 0x00000002;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// Renders <paramref name="hwnd"/> into a bitmap via <c>PrintWindow</c>. Returns
    /// null when the handle is invalid, the window has no area, or the call fails. The
    /// caller owns the returned bitmap.
    /// </summary>
    public static Bitmap? Render(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        if (!GetWindowRect(hwnd, out RECT rect))
        {
            return null;
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        bool ok;
        using (var graphics = Graphics.FromImage(bitmap))
        {
            IntPtr hdc = graphics.GetHdc();
            try
            {
                ok = PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT);
            }
            finally
            {
                // ReleaseHdc must always run, or the Graphics is left in an unusable state.
                graphics.ReleaseHdc(hdc);
            }
        }

        if (!ok)
        {
            bitmap.Dispose();
            return null;
        }

        return bitmap;
    }

    /// <summary>
    /// Heuristic blank check: samples a coarse grid and reports true when every sampled
    /// pixel is identical, i.e. the capture almost certainly rendered no real content
    /// (the classic GDI-vs-composition failure). Used to decide whether to fall back to
    /// another capture path.
    /// </summary>
    public static bool LooksBlank(Bitmap bitmap)
    {
        Color first = bitmap.GetPixel(0, 0);
        int stepX = Math.Max(1, bitmap.Width / 48);
        int stepY = Math.Max(1, bitmap.Height / 48);

        for (int y = 0; y < bitmap.Height; y += stepY)
        {
            for (int x = 0; x < bitmap.Width; x += stepX)
            {
                if (bitmap.GetPixel(x, y) != first)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Captures <paramref name="hwnd"/> to <paramref name="filePath"/> as a PNG. Returns
    /// true when a file was written; <paramref name="blank"/> reports whether that image
    /// looks empty so the caller can prefer another capture path.
    /// </summary>
    public static bool TrySaveWindowPng(IntPtr hwnd, string filePath, out bool blank)
    {
        blank = false;
        using Bitmap? bitmap = Render(hwnd);
        if (bitmap is null)
        {
            return false;
        }

        blank = LooksBlank(bitmap);
        bitmap.Save(filePath, ImageFormat.Png);
        return true;
    }
}
