using System.Text;
using System.Diagnostics;

namespace LabAssistant.WinUI.Diagnostics;

internal static class StartupCrashLogger
{
    private static readonly object SyncRoot = new();

    public static void MarkPhase(string phase, string? details = null)
    {
        WriteLine($"[PHASE] {phase}{(string.IsNullOrWhiteSpace(details) ? string.Empty : $" | {details}")}");
    }

    public static void LogException(string source, Exception exception)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"[EXCEPTION] Source={source}");

        var current = exception;
        var depth = 0;
        while (current is not null)
        {
            builder.AppendLine($"  [{depth}] Type={current.GetType().FullName}");
            builder.AppendLine($"  [{depth}] HResult=0x{current.HResult:X8}");
            builder.AppendLine($"  [{depth}] Message={current.Message}");
            if (!string.IsNullOrWhiteSpace(current.StackTrace))
            {
                builder.AppendLine($"  [{depth}] StackTrace:");
                builder.AppendLine(current.StackTrace);
            }

            current = current.InnerException;
            depth++;
        }

        WriteLine(builder.ToString().TrimEnd());
    }

    public static void LogMessage(string source, string message)
    {
        WriteLine($"[MESSAGE] Source={source} | {message}");
    }

    private static void WriteLine(string message)
    {
        try
        {
            lock (SyncRoot)
            {
                var timestamp = DateTimeOffset.UtcNow.ToString("O");
                var line = $"{timestamp} {message}{Environment.NewLine}";

                // Startup diagnostics must never crash the app.
                foreach (var path in ResolveLogPaths())
                {
                    try
                    {
                        var directory = Path.GetDirectoryName(path);
                        if (!string.IsNullOrWhiteSpace(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        File.AppendAllText(path, line);
                        return;
                    }
                    catch
                    {
                        // Try next fallback path.
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupCrashLogger] Failed to write startup log: {ex.Message}");
        }
    }

    private static IEnumerable<string> ResolveLogPaths()
    {
        var paths = new List<string>();
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                var localPath = Path.Combine(localAppData, "LabAssistant", "logs", "startup-crash.log");
                if (yielded.Add(localPath))
                {
                    paths.Add(localPath);
                }
            }
        }
        catch
        {
            // Fallbacks below.
        }

        var tempPath = Path.Combine(Path.GetTempPath(), "LabAssistant-startup-crash.log");
        if (yielded.Add(tempPath))
        {
            paths.Add(tempPath);
        }

        return paths;
    }
}
