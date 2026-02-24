namespace LabAssistant.Services.Logging;

public sealed class NullStructuredLogger : IStructuredLogger
{
    public static readonly NullStructuredLogger Instance = new();

    private NullStructuredLogger()
    {
    }

    public void Log(StructuredLogEvent logEvent)
    {
    }

    public void Log(
        StructuredLogLevel level,
        string eventName,
        string operationId,
        string? result = null,
        IReadOnlyDictionary<string, object?>? context = null)
    {
    }
}
