namespace LabAssistant.Services.Logging;

public static class StructuredLoggingDefaults
{
    public const string StructuredEventsFileName = "structured-events.jsonl";
    public const long MaxActiveFileBytes = 5 * 1024 * 1024;
    public const int RetainedHistoryFiles = 5;
}
