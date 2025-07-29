using System;
using System.IO;
using System.Text.Json;
using LabAssistant.Models.Configuration;

namespace LabAssistant.Services.Configuration
{
    public static class SettingsManager
    {
        private static readonly string BaseFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private static readonly string AppRoot = Path.Combine(BaseFolder, "LabAssistant");
        private static readonly string AppConfigFolder = Path.Combine(AppRoot, "Config");
        private static readonly string SettingsFilePath = Path.Combine(AppConfigFolder, "settings.json");

        public static AppSettings Settings { get; private set; } = new();

        public static void LoadOrCreate()
        {
            try
            {
                if (!Directory.Exists(AppConfigFolder))
                    Directory.CreateDirectory(AppConfigFolder);

                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    Settings = GetDefaultSettings();
                    Save(); // Create default file
                }

                ValidateSettings();
                EnsureAllConfiguredDirectoriesExist();
            }
            catch (Exception)
            {
                Settings = GetDefaultSettings(); // fallback
                EnsureAllConfiguredDirectoriesExist();
                Save();
            }
        }

        public static void Reload()
        {
            LoadOrCreate();
        }

        public static void Save()
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }

        public static void ResetToDefault()
        {
            Settings = GetDefaultSettings();
            EnsureAllConfiguredDirectoriesExist();
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
                DefaultVmMemoryMb = 2048,
                DefaultCpuCount = 2
            };
        }

        private static void EnsureAllConfiguredDirectoriesExist()
        {
            EnsureDirectoryExists(Settings.TemplateFolder);
            EnsureDirectoryExists(Settings.LogFolder);
            EnsureDirectoryExists(Settings.VmBasePath);
            EnsureDirectoryExists(Settings.DifferencingDiskBasePath);
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static void ValidateSettings()
        {
            var defaults = GetDefaultSettings();

            if (string.IsNullOrWhiteSpace(Settings.TemplateFolder))
                Settings.TemplateFolder = defaults.TemplateFolder;

            if (string.IsNullOrWhiteSpace(Settings.LogFolder))
                Settings.LogFolder = defaults.LogFolder;

            if (string.IsNullOrWhiteSpace(Settings.VmBasePath))
                Settings.VmBasePath = defaults.VmBasePath;

            if (string.IsNullOrWhiteSpace(Settings.DifferencingDiskBasePath))
                Settings.DifferencingDiskBasePath = defaults.DifferencingDiskBasePath;

            if (Settings.DefaultVmMemoryMb <= 0)
                Settings.DefaultVmMemoryMb = defaults.DefaultVmMemoryMb;

            if (Settings.DefaultCpuCount <= 0)
                Settings.DefaultCpuCount = defaults.DefaultCpuCount;
        }

        public static string SettingsPath => SettingsFilePath;

        public static void SetTemplateFolder(string path)
        {
            Settings.TemplateFolder = path;
            EnsureDirectoryExists(path);
            Save();
        }

        public static void SetLogFolder(string path)
        {
            Settings.LogFolder = path;
            EnsureDirectoryExists(path);
            Save();
        }

        public static void SetVmBasePath(string path)
        {
            Settings.VmBasePath = path;
            EnsureDirectoryExists(path);
            Save();
        }

        public static void SetDifferencingDiskBasePath(string path)
        {
            Settings.DifferencingDiskBasePath = path;
            EnsureDirectoryExists(path);
            Save();
        }
    }
}
