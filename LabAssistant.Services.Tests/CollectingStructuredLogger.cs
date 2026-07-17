using System.Collections.Generic;
using LabAssistant.Services.Logging;

namespace LabAssistant.Services.Tests;

/// <summary>
/// Minimal <see cref="IStructuredLogger"/> test double that records every emitted event. Used to assert that the
/// ambient tracers (DebugLogger / HyperVPowerShellTimingLogger) forward into the structured pipeline.
/// </summary>
internal sealed class CollectingStructuredLogger : IStructuredLogger
{
    public List<StructuredLogEvent> Events { get; } = new();

    public void Log(StructuredLogEvent logEvent) => Events.Add(logEvent);

    public void Log(
        StructuredLogLevel level,
        string eventName,
        string operationId,
        string? result = null,
        IReadOnlyDictionary<string, object?>? context = null)
        => Events.Add(StructuredLogEvent.Create(level, eventName, operationId, result, context));
}
