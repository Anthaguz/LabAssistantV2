using System.Text.Json;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Services.Tests;

public class StructuredLoggingTests
{
    [Fact]
    public void StructuredLogEvent_Create_PopulatesRequiredFields_AndUsesUtcTimestamp()
    {
        var timestamp = new DateTimeOffset(2026, 2, 24, 12, 30, 15, TimeSpan.FromHours(-5));

        var logEvent = StructuredLogEvent.Create(
            StructuredLogLevel.Warn,
            "TemplateImported",
            "op-123",
            result: "success",
            context: new Dictionary<string, object?> { ["templateId"] = "t-1" },
            timestampUtc: timestamp);

        Assert.Equal("warn", logEvent.Level);
        Assert.Equal("TemplateImported", logEvent.Event);
        Assert.Equal("op-123", logEvent.OperationId);
        Assert.Equal("success", logEvent.Result);

        var parsed = DateTimeOffset.Parse(logEvent.Ts);
        Assert.Equal(TimeSpan.Zero, parsed.Offset);
        Assert.Equal("2026-02-24T17:30:15.0000000+00:00", parsed.ToString("O"));
    }

    [Fact]
    public void JsonLinesLogEventSink_Write_AppendsOneValidJsonObjectPerLine()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"labassistant-log-{Guid.NewGuid():N}.jsonl");
        try
        {
            var sink = new JsonLinesLogEventSink(filePath);

            sink.Write(StructuredLogEvent.Create(
                StructuredLogLevel.Info,
                "DeployLabStarted",
                "op-a",
                context: new Dictionary<string, object?> { ["templateId"] = "t1" }));

            sink.Write(StructuredLogEvent.Create(
                StructuredLogLevel.Error,
                "DeployLabFailed",
                "op-a",
                result: "failed_with_residuals",
                context: new Dictionary<string, object?> { ["errorMessage"] = "line1\nline2" }));

            var lines = File.ReadAllLines(filePath);
            Assert.Equal(2, lines.Length);
            Assert.All(lines, line => Assert.False(string.IsNullOrWhiteSpace(line)));

            using var firstDoc = JsonDocument.Parse(lines[0]);
            using var secondDoc = JsonDocument.Parse(lines[1]);

            Assert.Equal("DeployLabStarted", firstDoc.RootElement.GetProperty("event").GetString());
            Assert.Equal("op-a", firstDoc.RootElement.GetProperty("operationId").GetString());

            Assert.Equal("DeployLabFailed", secondDoc.RootElement.GetProperty("event").GetString());
            Assert.Equal("failed_with_residuals", secondDoc.RootElement.GetProperty("result").GetString());
            Assert.Equal("line1\nline2", secondDoc.RootElement.GetProperty("context").GetProperty("errorMessage").GetString());
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void JsonLinesLogEventSink_Write_OmitsOptionalNullFields()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"labassistant-log-{Guid.NewGuid():N}.jsonl");
        try
        {
            var sink = new JsonLinesLogEventSink(filePath);
            sink.Write(StructuredLogEvent.Create(StructuredLogLevel.Info, "CatalogLoaded", "op-c"));

            var line = File.ReadAllText(filePath).Trim();
            using var doc = JsonDocument.Parse(line);

            Assert.True(doc.RootElement.TryGetProperty("ts", out _));
            Assert.True(doc.RootElement.TryGetProperty("level", out _));
            Assert.True(doc.RootElement.TryGetProperty("event", out _));
            Assert.True(doc.RootElement.TryGetProperty("operationId", out _));
            Assert.False(doc.RootElement.TryGetProperty("result", out _));
            Assert.False(doc.RootElement.TryGetProperty("context", out _));
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void JsonLinesLogEventSink_Write_ThrowsActionableException_OnWriteFailure()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"labassistant-log-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        try
        {
            var sink = new JsonLinesLogEventSink(directoryPath);

            var ex = Assert.Throws<StructuredLogWriteException>(() =>
                sink.Write(StructuredLogEvent.Create(StructuredLogLevel.Info, "CatalogSaved", "op-x")));

            Assert.Equal(directoryPath, ex.FilePath);
            Assert.Contains("Failed to write structured log event", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void JsonLinesLogEventSink_Write_RotatesAtSizeThreshold_AndKeepsActiveFileStable()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"labassistant-jsonl-rotate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, StructuredLoggingDefaults.StructuredEventsFileName);

        try
        {
            var sink = new JsonLinesLogEventSink(filePath, maxActiveFileBytes: 220, retainedHistoryFiles: 2);

            sink.Write(StructuredLogEvent.Create(StructuredLogLevel.Info, "EventA", "op-1", context: new Dictionary<string, object?> { ["message"] = new string('a', 80) }));
            sink.Write(StructuredLogEvent.Create(StructuredLogLevel.Info, "EventB", "op-1", context: new Dictionary<string, object?> { ["message"] = new string('b', 80) }));
            sink.Write(StructuredLogEvent.Create(StructuredLogLevel.Info, "EventC", "op-1", context: new Dictionary<string, object?> { ["message"] = new string('c', 80) }));

            Assert.True(File.Exists(filePath));
            var rotated1 = Rotated(filePath, 1);
            Assert.True(File.Exists(rotated1));

            var activeLines = File.ReadAllLines(filePath);
            var rotatedLines = File.ReadAllLines(rotated1);
            Assert.NotEmpty(activeLines);
            Assert.NotEmpty(rotatedLines);
            Assert.All(activeLines, line => JsonDocument.Parse(line).Dispose());
            Assert.All(rotatedLines, line => JsonDocument.Parse(line).Dispose());

            using var activeLast = JsonDocument.Parse(activeLines.Last());
            Assert.Equal("EventC", activeLast.RootElement.GetProperty("event").GetString());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void JsonLinesLogEventSink_Write_AppliesRetentionCleanup_ToOldestRotatedFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"labassistant-jsonl-retain-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, StructuredLoggingDefaults.StructuredEventsFileName);

        try
        {
            var sink = new JsonLinesLogEventSink(filePath, maxActiveFileBytes: 200, retainedHistoryFiles: 2);
            for (var i = 0; i < 6; i++)
            {
                sink.Write(StructuredLogEvent.Create(
                    StructuredLogLevel.Info,
                    $"Event{i}",
                    "op-1",
                    context: new Dictionary<string, object?> { ["message"] = new string((char)('a' + i), 70) }));
            }

            Assert.True(File.Exists(filePath));
            Assert.True(File.Exists(Rotated(filePath, 1)));
            Assert.True(File.Exists(Rotated(filePath, 2)));
            Assert.False(File.Exists(Rotated(filePath, 3)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void StructuredLogger_Log_WritesToAllConfiguredSinks()
    {
        var sinkA = new FakeSink();
        var sinkB = new FakeSink();
        var logger = new StructuredLogger(new ILogEventSink[] { sinkA, sinkB });

        logger.Log(StructuredLogLevel.Debug, "StepStarted", "op-1", context: new Dictionary<string, object?> { ["stepKey"] = "CreateVm" });

        Assert.Single(sinkA.Events);
        Assert.Single(sinkB.Events);
        Assert.Equal("StepStarted", sinkA.Events[0].Event);
        Assert.Equal("debug", sinkA.Events[0].Level);
    }

    private sealed class FakeSink : ILogEventSink
    {
        public List<StructuredLogEvent> Events { get; } = new();

        public void Write(StructuredLogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }

    private static string Rotated(string activeFilePath, int index)
    {
        var directory = Path.GetDirectoryName(activeFilePath) ?? string.Empty;
        var extension = Path.GetExtension(activeFilePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(activeFilePath);
        return Path.Combine(directory, $"{fileNameWithoutExtension}.{index}{extension}");
    }
}
