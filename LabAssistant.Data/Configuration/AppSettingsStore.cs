using System;
using System.Collections.Generic;
using System.Text.Json;
using LabAssistant.Data.Catalog;
using LabAssistant.Data.IO;
using LabAssistant.Models.Configuration;

namespace LabAssistant.Data.Configuration;

public sealed class AppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions LoadOptions = new() { PropertyNameCaseInsensitive = true };

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
                if (TryLoadSettingsFile(out var loaded))
                {
                    Settings = loaded;
                }
                else
                {
                    // The file exists but could not be read/parsed. Preserve it for recovery instead of
                    // silently overwriting the user's real settings, then fall back to defaults.
                    QuarantineCorruptSettingsFile();
                    Settings = GetDefaultSettings();
                    Save();
                }
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
            // Last-resort environmental failure (e.g. cannot create the config directory). Use in-memory
            // defaults but do NOT Save() over any on-disk settings file that may still be intact.
            Settings = GetDefaultSettings();
            EnsureAllConfiguredDirectoriesExist();
            EnsureCatalogFileExists();
        }
    }

    private bool TryLoadSettingsFile(out AppSettings settings)
    {
        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            var parsed = JsonSerializer.Deserialize<AppSettings>(json, LoadOptions);
            if (parsed is null)
            {
                settings = new AppSettings();
                return false;
            }

            settings = parsed;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            settings = new AppSettings();
            return false;
        }
    }

    private void QuarantineCorruptSettingsFile()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var quarantinePath = $"{_settingsFilePath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
                File.Move(_settingsFilePath, quarantinePath);
            }
        }
        catch
        {
            // Best-effort preservation. If the corrupt file cannot be moved, the subsequent Save()
            // will overwrite it; there is nothing further we can do without a logging seam here.
        }
    }

    public void Reload()
    {
        LoadOrCreate();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Settings, SaveOptions);
        SafeFileWriter.WriteAllText(_settingsFilePath, json);
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

    public void SetTheme(AppTheme theme)
    {
        Settings.Theme = theme;
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

        if (string.IsNullOrWhiteSpace(Settings.MachineDeletionPolicy))
        {
            Settings.MachineDeletionPolicy = defaults.MachineDeletionPolicy;
        }
    }
}
