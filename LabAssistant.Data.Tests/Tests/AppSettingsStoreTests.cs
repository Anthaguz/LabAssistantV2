using System;
using System.IO;
using LabAssistant.Data.Configuration;
using Xunit;

namespace LabAssistant.Data.Tests;

public class AppSettingsStoreTests
{
    [Fact]
    public void LoadOrCreate_CreatesDefaultsAndFile()
    {
        var appRoot = BuildTempRoot();
        var store = new AppSettingsStore(new AppPaths(appRoot));

        store.LoadOrCreate();

        Assert.True(File.Exists(store.SettingsPath));
        Assert.Equal(Path.Combine(appRoot, "Templates"), store.Settings.TemplateFolder);
        Assert.Equal(Path.Combine(appRoot, "Logs"), store.Settings.LogFolder);
        Assert.Equal(Path.Combine(appRoot, "VMs"), store.Settings.VmBasePath);
        Assert.Equal(Path.Combine(appRoot, "Disks"), store.Settings.DifferencingDiskBasePath);
        Assert.Equal(Path.Combine(appRoot, "Catalog", "vhdx-catalog.json"), store.Settings.CatalogPath);
    }

    [Fact]
    public void Save_RoundTripsSettings()
    {
        var appRoot = BuildTempRoot();
        var store = new AppSettingsStore(new AppPaths(appRoot));
        store.LoadOrCreate();

        store.Settings.TemplateFolder = Path.Combine(appRoot, "CustomTemplates");
        store.Save();

        var reloaded = new AppSettingsStore(new AppPaths(appRoot));
        reloaded.LoadOrCreate();

        Assert.Equal(Path.Combine(appRoot, "CustomTemplates"), reloaded.Settings.TemplateFolder);
    }

    [Fact]
    public void LoadOrCreate_WhenInvalidJson_FallsBackToDefaults()
    {
        var appRoot = BuildTempRoot();
        var store = new AppSettingsStore(new AppPaths(appRoot));
        store.LoadOrCreate();
        File.WriteAllText(store.SettingsPath, "{ this is not valid json");

        var reloaded = new AppSettingsStore(new AppPaths(appRoot));
        reloaded.LoadOrCreate();

        Assert.Equal(Path.Combine(appRoot, "Templates"), reloaded.Settings.TemplateFolder);
        Assert.True(File.Exists(reloaded.SettingsPath));
    }

    [Fact]
    public void SetTheme_PersistsAndRoundTripsAcrossReload()
    {
        var appRoot = BuildTempRoot();
        var store = new AppSettingsStore(new AppPaths(appRoot));
        store.LoadOrCreate();

        // Default is Light for a fresh install.
        Assert.Equal(LabAssistant.Models.Configuration.AppTheme.Light, store.Settings.Theme);

        store.SetTheme(LabAssistant.Models.Configuration.AppTheme.Dark);

        var reloaded = new AppSettingsStore(new AppPaths(appRoot));
        reloaded.LoadOrCreate();

        Assert.Equal(LabAssistant.Models.Configuration.AppTheme.Dark, reloaded.Settings.Theme);
    }

    private static string BuildTempRoot()
    {
        var folder = Path.Combine(Path.GetTempPath(), "LabAssistantTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
