namespace LabAssistant.Services.Logging;

public sealed class StructuredLogger : IStructuredLogger
{
    private readonly IReadOnlyList<ILogEventSink> _sinks;

    public StructuredLogger(IEnumerable<ILogEventSink> sinks)
    {
        _sinks = sinks?.ToList() ?? throw new ArgumentNullException(nameof(sinks));
    }

    public void Log(StructuredLogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        foreach (var sink in _sinks)
        {
            sink.Write(logEvent);
        }
    }

    public void Log(
        StructuredLogLevel level,
        string eventName,
        string operationId,
        string? result = null,
        IReadOnlyDictionary<string, object?>? context = null)
    {
        Log(StructuredLogEvent.Create(level, eventName, operationId, result, context));
    }

    // The code-based Log(uint, ...) overload is the default interface implementation on IStructuredLogger;
    // it composes the event and routes back through Log(StructuredLogEvent) above.
}
