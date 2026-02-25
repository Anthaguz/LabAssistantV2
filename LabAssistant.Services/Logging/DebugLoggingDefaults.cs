namespace LabAssistant.Services.Logging;

public static class DebugLoggingDefaults
{
    public const string DebugLogFileName = "log.txt";
    public const long MaxActiveFileBytes = 2 * 1024 * 1024;
    public const int RetainedHistoryFiles = 5;
}
