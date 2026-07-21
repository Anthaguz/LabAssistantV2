using LabAssistant.Models.Configuration;
using LabAssistant.WinUI.Shell;
using Xunit;

namespace LabAssistant.UI.Tests;

/// <summary>
/// Runtime-independent seam tests for <see cref="ShellThemeState"/>: the pure theme-persistence
/// logic that <see cref="ShellThemeManager"/> maps onto the live WinUI shell. These verify the
/// user's dark or light choice is written back to the settings store and restored on the next
/// launch, so the theme no longer resets on every restart. No WinUI types are involved, so these
/// run without a WinUI runtime.
/// </summary>
public sealed class ShellThemeStateTests
{
    [Fact]
    public void Constructor_SeedsThemeFromPersistedSettings()
    {
        var store = new FakeAppSettingsStore { Settings = { Theme = AppTheme.Dark } };

        var state = new ShellThemeState(store);

        Assert.Equal(AppTheme.Dark, state.Theme);
    }

    [Fact]
    public void Constructor_DefaultsToLightWhenNothingPersisted()
    {
        var store = new FakeAppSettingsStore();

        var state = new ShellThemeState(store);

        Assert.Equal(AppTheme.Light, state.Theme);
    }

    [Fact]
    public void Toggle_FromLight_SelectsAndPersistsDark()
    {
        var store = new FakeAppSettingsStore { Settings = { Theme = AppTheme.Light } };
        var state = new ShellThemeState(store);

        var result = state.Toggle();

        Assert.Equal(AppTheme.Dark, result);
        Assert.Equal(AppTheme.Dark, state.Theme);
        Assert.Equal(AppTheme.Dark, store.Settings.Theme);
        Assert.Equal(1, store.SetThemeCallCount);
    }

    [Fact]
    public void Toggle_FromDark_SelectsAndPersistsLight()
    {
        var store = new FakeAppSettingsStore { Settings = { Theme = AppTheme.Dark } };
        var state = new ShellThemeState(store);

        var result = state.Toggle();

        Assert.Equal(AppTheme.Light, result);
        Assert.Equal(AppTheme.Light, store.Settings.Theme);
    }

    [Fact]
    public void Toggle_PersistedChoiceSurvivesRestart()
    {
        // A shared store instance stands in for the on-disk settings file across two app lifetimes.
        var store = new FakeAppSettingsStore { Settings = { Theme = AppTheme.Light } };

        // First launch: user switches to dark.
        new ShellThemeState(store).Toggle();

        // Second launch: a fresh state must come up in the persisted (dark) theme, not reset to light.
        var afterRestart = new ShellThemeState(store);

        Assert.Equal(AppTheme.Dark, afterRestart.Theme);
    }

    /// <summary>
    /// In-memory <see cref="IAppSettingsStore"/> double. Only <see cref="SetTheme"/> is exercised
    /// here; the remaining members satisfy the interface and are intentionally inert.
    /// </summary>
    private sealed class FakeAppSettingsStore : IAppSettingsStore
    {
        public AppSettings Settings { get; set; } = new();

        public string SettingsPath => "in-memory";

        public int SetThemeCallCount { get; private set; }

        public void SetTheme(AppTheme theme)
        {
            Settings.Theme = theme;
            SetThemeCallCount++;
        }

        public void LoadOrCreate() { }
        public void Reload() { }
        public void Save() { }
        public void ResetToDefault() { }
        public void SetTemplateFolder(string path) { }
        public void SetLogFolder(string path) { }
        public void SetVmBasePath(string path) { }
        public void SetDifferencingDiskBasePath(string path) { }
    }
}
