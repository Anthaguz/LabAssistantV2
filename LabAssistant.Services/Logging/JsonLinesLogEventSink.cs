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
    private readonly long _maxActiveFileBytes;
    private readonly int _retainedHistoryFiles;

    public JsonLinesLogEventSink(
        string filePath,
        long maxActiveFileBytes = StructuredLoggingDefaults.MaxActiveFileBytes,
        int retainedHistoryFiles = StructuredLoggingDefaults.RetainedHistoryFiles)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Log file path is required.", nameof(filePath));
        }

        _filePath = filePath;
        _maxActiveFileBytes = maxActiveFileBytes;
        _retainedHistoryFiles = retainedHistoryFiles;
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
            var bytesToAppend = Encoding.UTF8.GetByteCount(json + Environment.NewLine);

            lock (_writeLock)
            {
                FileLogRotation.RotateIfNeeded(_filePath, bytesToAppend, _maxActiveFileBytes, _retainedHistoryFiles);
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
