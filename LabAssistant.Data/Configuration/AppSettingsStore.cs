using System.Collections.Generic;
using System.Text.Json;
using LabAssistant.Data.Catalog;
using LabAssistant.Models.Configuration;

namespace LabAssistant.Data.Configuration;

public sealed class AppSettingsStore : IAppSettingsStore
{
    private readonly IAppPaths _paths;
    private readonly string _appRoot;
    private readonly string _appConfigFolder;
    private readonly string _catalogFolder;
    private readonly string _settingsFilePath;

    public AppSettingsStore(IAppPaths? paths = null)
    {
        _paths = paths ?? new AppPaths();
        _appRoot = _paths.AppRoot;
        _appConfigFolder = _paths.ConfigFolder;
        _catalogFolder = _paths.CatalogFolder;
        _settingsFilePath = Path.Combine(_appConfigFolder, "settings.json");
    }

    public AppSettings Settings { get; private set; } = new();

    public string SettingsPath => _settingsFilePath;

    public void LoadOrCreate()
    {
        try
        {
            if (!Directory.Exists(_appConfigFolder))
            {
                Directory.CreateDirectory(_appConfigFolder);
            }

            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                Settings = GetDefaultSettings();
                Save();
            }

            ValidateSettings();
            EnsureAllConfiguredDirectoriesExist();
            EnsureCatalogFileExists();
        }
        catch (Exception)
        {
            Settings = GetDefaultSettings();
            EnsureAllConfiguredDirectoriesExist();
            EnsureCatalogFileExists();
            Save();
        }
    }

    public void Reload()
    {
        LoadOrCreate();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsFilePath, json);
    }

    public void ResetToDefault()
    {
        Settings = GetDefaultSettings();
        EnsureAllConfiguredDirectoriesExist();
        Save();
    }

    public void SetTemplateFolder(string path)
    {
        Settings.TemplateFolder = path;
        EnsureDirectoryExists(path);
        Save();
    }

    public void SetLogFolder(string path)
    {
        Settings.LogFolder = path;
        EnsureDirectoryExists(path);
        Save();
    }

    public void SetVmBasePath(string path)
    {
        Settings.VmBasePath = path;
        EnsureDirectoryExists(path);
        Save();
    }

    public void SetDifferencingDiskBasePath(string path)
    {
        Settings.DifferencingDiskBasePath = path;
        EnsureDirectoryExists(path);
        Save();
    }

    private AppSettings GetDefaultSettings()
    {
        return new AppSettings
        {
            TemplateFolder = _paths.TemplatesFolder,
            LogFolder = _paths.LogsFolder,
            VmBasePath = _paths.VmBasePath,
            DifferencingDiskBasePath = _paths.DifferencingDiskBasePath,
            CatalogPath = _paths.CatalogPath,
            DefaultVmMemoryMb = 2048,
            DefaultCpuCount = 2,
            PerVmFailFast = true,
            StopAllOnAnyVmFailure = false,
            NonBlockingOptionalSteps = new List<string>()
        };
    }

    private void EnsureAllConfiguredDirectoriesExist()
    {
        EnsureDirectoryExists(Settings.TemplateFolder);
        EnsureDirectoryExists(Settings.LogFolder);
        EnsureDirectoryExists(Settings.VmBasePath);
        EnsureDirectoryExists(Settings.DifferencingDiskBasePath);
        EnsureDirectoryExists(Path.GetDirectoryName(Settings.CatalogPath) ?? string.Empty);
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private void EnsureCatalogFileExists()
    {
        var store = new VhdxCatalogStore();
        store.EnsureCatalogFileExists(Settings.CatalogPath);
    }

    private void ValidateSettings()
    {
        var defaults = GetDefaultSettings();

        if (string.IsNullOrWhiteSpace(Settings.TemplateFolder))
        {
            Settings.TemplateFolder = defaults.TemplateFolder;
        }

        if (string.IsNullOrWhiteSpace(Settings.LogFolder))
        {
            Settings.LogFolder = defaults.LogFolder;
        }

        if (string.IsNullOrWhiteSpace(Settings.VmBasePath))
        {
            Settings.VmBasePath = defaults.VmBasePath;
        }

        if (string.IsNullOrWhiteSpace(Settings.DifferencingDiskBasePath))
        {
            Settings.DifferencingDiskBasePath = defaults.DifferencingDiskBasePath;
        }

        if (Settings.DefaultVmMemoryMb <= 0)
        {
            Settings.DefaultVmMemoryMb = defaults.DefaultVmMemoryMb;
        }

        if (Settings.DefaultCpuCount <= 0)
        {
            Settings.DefaultCpuCount = defaults.DefaultCpuCount;
        }

        if (string.IsNullOrWhiteSpace(Settings.CatalogPath))
        {
            Settings.CatalogPath = defaults.CatalogPath;
        }

        if (Settings.NonBlockingOptionalSteps == null)
        {
            Settings.NonBlockingOptionalSteps = new List<string>();
        }
    }
}
