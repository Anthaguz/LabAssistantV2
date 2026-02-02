using System.Text.Json;
using LabAssistant.Data.Catalog;
using LabAssistant.Models.Configuration;

namespace LabAssistant.Data.Configuration;

public sealed class AppSettingsStore : IAppSettingsStore
{
    private static readonly string BaseFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string AppRoot = Path.Combine(BaseFolder, "LabAssistant");
    private static readonly string AppConfigFolder = Path.Combine(AppRoot, "Config");
    private static readonly string CatalogFolder = Path.Combine(AppRoot, "Catalog");
    private static readonly string SettingsFilePath = Path.Combine(AppConfigFolder, "settings.json");

    public AppSettings Settings { get; private set; } = new();

    public string SettingsPath => SettingsFilePath;

    public void LoadOrCreate()
    {
        try
        {
            if (!Directory.Exists(AppConfigFolder))
            {
                Directory.CreateDirectory(AppConfigFolder);
            }

            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
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
        File.WriteAllText(SettingsFilePath, json);
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

    private static AppSettings GetDefaultSettings()
    {
        return new AppSettings
        {
            TemplateFolder = Path.Combine(AppRoot, "Templates"),
            LogFolder = Path.Combine(AppRoot, "Logs"),
            VmBasePath = Path.Combine(AppRoot, "VMs"),
            DifferencingDiskBasePath = Path.Combine(AppRoot, "Disks"),
            CatalogPath = Path.Combine(CatalogFolder, "vhdx-catalog.json"),
            DefaultVmMemoryMb = 2048,
            DefaultCpuCount = 2
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
    }
}
