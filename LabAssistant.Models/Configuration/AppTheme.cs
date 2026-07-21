namespace LabAssistant.Models.Configuration;

/// <summary>
/// User-selected application colour theme. Persisted in <see cref="AppSettings"/> so the choice
/// survives an app restart. Kept in Models (free of any WinUI type) so persistence and business
/// code can reason about the theme without depending on the UI framework; the WinUI shell maps
/// this onto its own <c>ElementTheme</c>.
/// </summary>
public enum AppTheme
{
    /// <summary>Light colour theme. The default for a fresh install.</summary>
    Light = 0,

    /// <summary>Dark colour theme.</summary>
    Dark = 1
}
