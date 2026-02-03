using System;
using System.IO;
using LabAssistant.Models.Configuration;

namespace LabAssistant.Services
{
    public sealed class Logger
    {
        private readonly IAppSettingsStore _settingsStore;

        public Logger(IAppSettingsStore settingsStore)
        {
            _settingsStore = settingsStore;
        }

        public void Log(string operationId, string message)
        {
            var logsDirectory = _settingsStore.Settings.LogFolder;
            if (string.IsNullOrWhiteSpace(logsDirectory))
            {
                throw new InvalidOperationException("Log folder not configured.");
            }

            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }

            string filePath = Path.Combine(logsDirectory, $"deployment-{DateTime.UtcNow:yyyy-MM-dd}.log");
            string logEntry = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}-{{{operationId}}}-{message}";

            File.AppendAllLines(filePath, new[] { logEntry });
        }
    }
}
