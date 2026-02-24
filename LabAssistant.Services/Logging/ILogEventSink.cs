namespace LabAssistant.Services.Logging;

public interface ILogEventSink
{
    void Write(StructuredLogEvent logEvent);
}
