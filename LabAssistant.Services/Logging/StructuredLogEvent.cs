using System.Text.Json.Serialization;

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
