using System.Text;
using System.Text.Json;

namespace LabAssistant.Services.Logging;

public sealed class JsonLinesLogEventSink : ILogEventSink
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    private readonly object _writeLock = new();
    private readonly string _filePath;

    public JsonLinesLogEventSink(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Log file path is required.", nameof(filePath));
        }

        _filePath = filePath;
    }

    public void Write(StructuredLogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(logEvent, SerializerOptions);

            lock (_writeLock)
            {
                using var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.WriteLine(json);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new StructuredLogWriteException(
                _filePath,
                $"Failed to write structured log event to '{_filePath}'.",
                ex);
        }
    }
}
