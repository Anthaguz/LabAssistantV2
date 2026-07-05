namespace LabAssistant.WinUI.Infrastructure;

/// <summary>
/// Opens a structured-log file (or its containing folder) in the OS shell. Sits behind an
/// interface so diagnostics view models stay unit-testable without launching the file explorer.
/// </summary>
public interface ILogLocationLauncher
{
    /// <summary>
    /// Reveals <paramref name="filePath"/> in the file explorer, falling back to its folder when
    /// the file does not exist yet.
    /// </summary>
    /// <returns>
    /// <c>null</c> when the exact file was revealed; otherwise a human-readable status describing
    /// the fallback taken (folder opened, or nothing to open).
    /// </returns>
    string? TryOpen(string filePath);
}
