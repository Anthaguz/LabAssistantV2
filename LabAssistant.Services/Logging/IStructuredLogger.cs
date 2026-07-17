using System.IO;
using System.Runtime.CompilerServices;

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

    /// <summary>
    /// Emits an event from a canonical 32-bit status code. The level, severity, facility, operation,
    /// phase, and flags are resolved from the code. The emitting call site is captured automatically
    /// for debugging; pass the caller attributes through explicitly when forwarding from a wrapper so
    /// the real emit site is recorded rather than the wrapper.
    /// </summary>
    /// <remarks>
    /// Default-implemented so it composes the event once and routes through <see cref="Log(StructuredLogEvent)"/>;
    /// every implementer (including test fakes) gets consistent code decomposition for free.
    /// </remarks>
    void Log(
        uint code,
        string operationId,
        string? result = null,
        IReadOnlyDictionary<string, object?>? context = null,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var callsite = string.IsNullOrWhiteSpace(callerFilePath)
            ? null
            : $"{Path.GetFileName(callerFilePath)}:{callerLineNumber}";
        Log(StructuredLogEvent.Create(code, operationId, result, context, callsite: callsite));
    }
}
