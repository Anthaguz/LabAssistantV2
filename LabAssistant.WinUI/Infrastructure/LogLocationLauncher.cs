using System.Diagnostics;
using System.IO;

namespace LabAssistant.WinUI.Infrastructure;

/// <summary>
/// Default <see cref="ILogLocationLauncher"/> that reveals a log file (or its folder) using the
/// Windows file explorer. Relocated from the shell so the behavior lives behind a seam rather than
/// in <c>MainWindow</c>.
/// </summary>
public sealed class LogLocationLauncher : ILogLocationLauncher
{
    public string? TryOpen(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"")
                {
                    UseShellExecute = true
                });
                return null;
            }

            var folderPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folderPath}\"")
                {
                    UseShellExecute = true
                });
                return $"Active structured log file not found. Opened log folder: {folderPath}";
            }

            return $"Structured log path does not exist yet: {filePath}";
        }
        catch (Exception ex)
        {
            return $"Failed to open structured log location. {ex.Message}";
        }
    }
}
