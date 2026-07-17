using System.Reflection;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Services.Tests;

[Collection("DebugLogger tests")]
public class HyperVPowerShellTimingLoggerTests
{
    [Fact]
    public void LogWorkflowSessionCreated_EmitsCodedSessionTimingEvent()
    {
        var logger = new CollectingStructuredLogger();
        try
        {
            DebugLogger.ConfigureStructuredSink(logger);

            HyperVPowerShellTimingLogger.LogWorkflowSessionCreated("vm-1", 1234);

            var e = Assert.Single(logger.Events);
            Assert.Equal($"0x{LaStatus.DiagPsTiming_SessionTiming:X8}", e.Code);
            Assert.Equal("diag.ps_timing.session", e.Event);
            Assert.Equal("debug", e.Level);
            Assert.Equal("session_created", e.Result);
            Assert.Equal(StructuredLoggingDefaults.AmbientOperationId, e.OperationId);
            Assert.Equal("workflow_session", e.Context!["pattern"]);
            Assert.Equal("vm-1", e.Context!["vmName"]);
            Assert.Equal(1234L, e.Context!["sessionCreationDurationMs"]);
            Assert.NotNull(e.Callsite);
        }
        finally
        {
            ResetSink();
        }
    }

    [Fact]
    public void LogQueryExecution_EmitsCodedCommandTimingEvent()
    {
        var logger = new CollectingStructuredLogger();
        try
        {
            DebugLogger.ConfigureStructuredSink(logger);

            HyperVPowerShellTimingLogger.LogQueryExecution("Get-VM", sessionCreated: true, commandDurationMs: 42, success: false);

            var e = Assert.Single(logger.Events);
            Assert.Equal($"0x{LaStatus.DiagPsTiming_CommandTiming:X8}", e.Code);
            Assert.Equal("diag.ps_timing.command", e.Event);
            Assert.Equal("query_executed", e.Result);
            Assert.Equal("query_session", e.Context!["pattern"]);
            Assert.Equal("Get-VM", e.Context!["query"]);
            Assert.Equal(42L, e.Context!["commandDurationMs"]);
            Assert.Equal(false, e.Context!["success"]);
        }
        finally
        {
            ResetSink();
        }
    }

    private static void ResetSink()
    {
        var method = typeof(DebugLogger).GetMethod("ResetForTests", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(null, System.Array.Empty<object>());
    }
}
