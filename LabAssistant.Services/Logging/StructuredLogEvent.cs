using System.Text.Json.Serialization;
using LabAssistant.Services.Diagnostics;

namespace LabAssistant.Services.Logging;

public sealed class StructuredLogEvent
{
    [JsonPropertyName("ts")]
    public string Ts { get; init; } = string.Empty;

    [JsonPropertyName("level")]
    public string Level { get; init; } = string.Empty;

    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    [JsonPropertyName("operationId")]
    public string OperationId { get; init; } = string.Empty;

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Result { get; init; }

    /// <summary>The canonical 32-bit status code as <c>0xSSFFOOCC</c>, when the event was emitted with one.</summary>
    [JsonPropertyName("code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; init; }

    /// <summary>The dotted facility name decomposed from the code (for example <c>infra.powershell</c>).</summary>
    [JsonPropertyName("facility")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Facility { get; init; }

    /// <summary>The operation name within the facility.</summary>
    [JsonPropertyName("operation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Operation { get; init; }

    /// <summary>Lifecycle phase (start/progress/end/atomic), carried separately from the code number.</summary>
    [JsonPropertyName("phase")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Phase { get; init; }

    /// <summary>The severity name decomposed from the code's high nibble.</summary>
    [JsonPropertyName("severity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Severity { get; init; }

    /// <summary>The set flag names decomposed from the code's flags nibble. Omitted when no flags are set.</summary>
    [JsonPropertyName("flags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Flags { get; init; }

    /// <summary>The managed thread id that emitted the event. Debug aid; only set on the code-based path.</summary>
    [JsonPropertyName("thread")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Thread { get; init; }

    /// <summary>The emitting call site as <c>file:line</c>. Debug aid; only set on the code-based path.</summary>
    [JsonPropertyName("callsite")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Callsite { get; init; }

    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? Context { get; init; }

    public static StructuredLogEvent Create(
        StructuredLogLevel level,
        string eventName,
        string operationId,
        string? result = null,
        IReadOnlyDictionary<string, object?>? context = null,
        DateTimeOffset? timestampUtc = null)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException("Event name is required.", nameof(eventName));
        }

        if (string.IsNullOrWhiteSpace(operationId))
        {
            throw new ArgumentException("OperationId is required.", nameof(operationId));
        }

        var utcTimestamp = (timestampUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();

        return new StructuredLogEvent
        {
            Ts = utcTimestamp.ToString("O"),
            Level = ToContractLevel(level),
            Event = eventName,
            OperationId = operationId,
            Result = string.IsNullOrWhiteSpace(result) ? null : result,
            Context = context == null || context.Count == 0 ? null : new Dictionary<string, object?>(context)
        };
    }

    /// <summary>
    /// Creates an event from a canonical 32-bit status code. The event name, level, severity, flags,
    /// facility, operation, and phase are all resolved from the code so they cannot drift from it.
    /// The level is projected from the code's severity nibble. Unregistered codes degrade gracefully to
    /// their decomposed byte values rather than throwing, so a caller can never crash the emit path.
    /// </summary>
    public static StructuredLogEvent Create(
        uint code,
        string operationId,
        string? result = null,
        IReadOnlyDictionary<string, object?>? context = null,
        DateTimeOffset? timestampUtc = null,
        int? threadId = null,
        string? callsite = null)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            throw new ArgumentException("OperationId is required.", nameof(operationId));
        }

        var utcTimestamp = (timestampUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var descriptor = StatusCodes.Describe(code);

        var facility = descriptor?.FacilityName ?? $"0x{StatusCodes.FacilityOf(code):X2}";
        var operation = descriptor?.OperationName ?? $"0x{StatusCodes.OperationOf(code):X2}";
        var dotted = descriptor?.DottedName ?? $"{facility}.{operation}";
        var phase = (descriptor?.Phase ?? StatusCodePhase.Atomic).ToString().ToLowerInvariant();
        var flags = StatusCodes.FlagNames(code);

        return new StructuredLogEvent
        {
            Ts = utcTimestamp.ToString("O"),
            Level = StatusCodes.LevelOf(code),
            Event = dotted,
            OperationId = operationId,
            Result = string.IsNullOrWhiteSpace(result) ? null : result,
            Code = $"0x{code:X8}",
            Facility = facility,
            Operation = operation,
            Phase = phase,
            Severity = StatusCodes.SeverityOf(code).ToString(),
            Flags = flags.Count == 0 ? null : flags,
            Thread = threadId ?? Environment.CurrentManagedThreadId,
            Callsite = string.IsNullOrWhiteSpace(callsite) ? null : callsite,
            Context = context == null || context.Count == 0 ? null : new Dictionary<string, object?>(context)
        };
    }

    private static string ToContractLevel(StructuredLogLevel level)
    {
        return level switch
        {
            StructuredLogLevel.Debug => "debug",
            StructuredLogLevel.Info => "info",
            StructuredLogLevel.Warn => "warn",
            StructuredLogLevel.Error => "error",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unsupported log level.")
        };
    }
}
