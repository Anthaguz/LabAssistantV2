using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace LabAssistant.Services.Logging
{
    public static class DebugLogger
    {
        private static readonly object _lock = new();
        private static string? _logFolder;
        private static long _maxActiveFileBytes = DebugLoggingDefaults.MaxActiveFileBytes;
        private static int _retainedHistoryFiles = DebugLoggingDefaults.RetainedHistoryFiles;

        private static string LogFilePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_logFolder))
                {
                    throw new InvalidOperationException("DebugLogger log folder not configured.");
                }

                return Path.Combine(_logFolder, DebugLoggingDefaults.DebugLogFileName);
            }
        }

        public static void SetLogFolder(string logFolder)
        {
            _logFolder = logFolder;
        }

        public static void Log(
            string message = "",
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
        {
            try
            {
                string directory = Path.GetDirectoryName(LogFilePath)!;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                int threadId = Environment.CurrentManagedThreadId;
                string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Thread {threadId}] [{Path.GetFileName(file)}:{line}] {member}() {message}";

                lock (_lock)
                {
                    var bytesToAppend = System.Text.Encoding.UTF8.GetByteCount(logLine + Environment.NewLine);
                    FileLogRotation.RotateIfNeeded(LogFilePath, bytesToAppend, _maxActiveFileBytes, _retainedHistoryFiles);
                    File.AppendAllText(LogFilePath, logLine + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DebugLogger failed: {ex.Message}");
            }
        }

        // Method to log both error and output obtained from powershell
        public static void LogPowerShellOutput(string command = "", string output = "", string error = "")
        {
            if (!string.IsNullOrEmpty(output))
            {
                Log($"PowerShell Output for '{command}': \n{output}");
            }
            if (!string.IsNullOrEmpty(error))
            {
                Log($"PowerShell Error for '{command}': \n{error}");
            }
        }

        internal static void ConfigureRotationForTests(long maxActiveFileBytes, int retainedHistoryFiles)
        {
            lock (_lock)
            {
                _maxActiveFileBytes = maxActiveFileBytes;
                _retainedHistoryFiles = retainedHistoryFiles;
            }
        }

        internal static void ResetRotationForTests()
        {
            lock (_lock)
            {
                _maxActiveFileBytes = DebugLoggingDefaults.MaxActiveFileBytes;
                _retainedHistoryFiles = DebugLoggingDefaults.RetainedHistoryFiles;
            }
        }
    }
}
