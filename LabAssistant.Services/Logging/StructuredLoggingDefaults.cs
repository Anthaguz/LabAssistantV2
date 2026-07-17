namespace LabAssistant.Services.Logging;

public static class StructuredLoggingDefaults
{
    public const string StructuredEventsFileName = "structured-events.jsonl";
    public const long MaxActiveFileBytes = 5 * 1024 * 1024;
    public const int RetainedHistoryFiles = 5;

    /// <summary>
    /// Operation id stamped on ambient diagnostic traces (the <c>diag.*</c> facilities) that are not tied to a
    /// user-initiated operation. Real operation ids are threaded through the coded emit sites in later work;
    /// the raw tracer has no operation context of its own, so it uses this well-known sentinel.
    /// </summary>
    public const string AmbientOperationId = "ambient";
}
