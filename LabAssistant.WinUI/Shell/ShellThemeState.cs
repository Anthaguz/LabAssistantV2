using LabAssistant.Models.Configuration;

namespace LabAssistant.WinUI.Shell;

/// <summary>
/// Runtime-independent theme-selection state. Loads the persisted theme from the app settings
/// store on construction and writes every subsequent change straight back through the store, so
/// the user's dark or light choice survives an app restart instead of resetting on each launch.
/// Kept free of <c>Microsoft.UI.Xaml</c> types so it is unit-testable without a WinUI runtime;
/// <see cref="ShellThemeManager"/> maps the resulting <see cref="AppTheme"/> onto the live
/// <c>ElementTheme</c> and shell chrome.
/// </summary>
internal sealed class ShellThemeState
{
    private readonly IAppSettingsStore _settingsStore;

    public ShellThemeState(IAppSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        Theme = settingsStore.Settings.Theme;
    }

    /// <summary>The currently selected theme, seeded from persisted settings.</summary>
    public AppTheme Theme { get; private set; }

    /// <summary>
    /// Flips between light and dark, persists the new choice, and returns it. Persisting on every
    /// toggle (rather than only on shutdown) means the selection is durable even if the process is
    /// terminated abnormally before a clean exit.
    /// </summary>
    public AppTheme Toggle()
    {
        Theme = Theme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        _settingsStore.SetTheme(Theme);
        return Theme;
    }
}
