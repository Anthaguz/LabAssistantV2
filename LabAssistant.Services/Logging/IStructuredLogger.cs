namespace LabAssistant.Services.Logging;

public interface IStructuredLogger
{
    void Log(StructuredLogEvent logEvent);

    void Log(
        StructuredLogLevel level,
        string eventName,
        string operationId,
        string? result = null,
        IReadOnlyDictionary<string, object?>? context = null);
}
