using System;
using System.IO;

namespace LabAssistant.Services
{
    public static class Logger
    {
        private static string LogsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LabAssistant", "Logs");

        public static void Log(string operationId, string message)
        {
            if (!Directory.Exists(LogsDirectory))
                Directory.CreateDirectory(LogsDirectory);

            string filePath = Path.Combine(LogsDirectory, $"deployment-{DateTime.UtcNow:yyyy-MM-dd}.log");
            string logEntry = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}-{{{operationId}}}-{message}";

            File.AppendAllLines(filePath, new[] { logEntry });
        }
    }
}