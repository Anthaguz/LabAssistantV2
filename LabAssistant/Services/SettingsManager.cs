using System;
using System.IO;
using System.Text.Json;
using LabAssistant.Models;

namespace LabAssistant.Services
{
    public static class SettingsManager
    {
        private static string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LabAssistant", "Config", "appsettings.json");
        private static SettingsModel _settings;

        static SettingsManager()
        {
            LoadSettings();
        }

        public static SettingsModel Current => _settings;

        private static void LoadSettings()
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                _settings = JsonSerializer.Deserialize<SettingsModel>(json);
            }
            else
            {
                _settings = new SettingsModel();
                SaveSettings();
            }
        }

        public static void SaveSettings()
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, json);
        }

        public static void ReloadSettings()
        {
            LoadSettings();
        }

    }
}