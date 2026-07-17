using System.Reflection;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Services.Tests;

[CollectionDefinition("DebugLogger tests", DisableParallelization = true)]
public sealed class DebugLoggerTestsCollectionDefinition
{
}

[Collection("DebugLogger tests")]
public class DebugLoggerTests
{
    private static readonly object DebugLoggerTestLock = new();

    [Fact]
    public void Log_ForwardsCodedDiagDebugEvent_PreservingMessageThreadAndCallsite()
    {
        lock (DebugLoggerTestLock)
        {
            var logger = new CollectingStructuredLogger();
            try
            {
                DebugLogger.ConfigureStructuredSink(logger);

                DebugLogger.Log("hello-trace");

                var e = Assert.Single(logger.Events);
                Assert.Equal($"0x{LaStatus.DiagDebug_DebugTrace:X8}", e.Code);
                Assert.Equal("diag.debug.trace", e.Event);
                Assert.Equal("debug", e.Level);
                Assert.Equal(StructuredLoggingDefaults.AmbientOperationId, e.OperationId);
                Assert.NotNull(e.Context);
                Assert.Equal("hello-trace", e.Context!["message"]);
                Assert.True(e.Context!.ContainsKey("member"));
                Assert.NotNull(e.Thread);
                Assert.NotNull(e.Callsite);
            }
            finally
            {
                ResetSink();
            }
        }
    }

    [Fact]
    public void Log_IsSilentNoOp_WhenSinkNotConfigured()
    {
        lock (DebugLoggerTestLock)
        {
            var logger = new CollectingStructuredLogger();
            try
            {
                DebugLogger.ConfigureStructuredSink(logger);
                ResetSink();

                DebugLogger.Log("dropped");

                Assert.Empty(logger.Events);
            }
            finally
            {
                ResetSink();
            }
        }
    }

    [Fact]
    public void LogPowerShellOutput_ForwardsOutputAndErrorAsSeparateTraces()
    {
        lock (DebugLoggerTestLock)
        {
            var logger = new CollectingStructuredLogger();
            try
            {
                DebugLogger.ConfigureStructuredSink(logger);

                DebugLogger.LogPowerShellOutput("Get-VM", output: "ok", error: "boom");

                Assert.Equal(2, logger.Events.Count);
                Assert.All(logger.Events, e => Assert.Equal($"0x{LaStatus.DiagDebug_DebugTrace:X8}", e.Code));
                Assert.Contains(logger.Events, e => ((string?)e.Context!["message"])!.Contains("ok"));
                Assert.Contains(logger.Events, e => ((string?)e.Context!["message"])!.Contains("boom"));
            }
            finally
            {
                ResetSink();
            }
        }
    }

    private static void ResetSink()
    {
        var method = typeof(DebugLogger).GetMethod("ResetForTests", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(null, System.Array.Empty<object>());
    }
}
