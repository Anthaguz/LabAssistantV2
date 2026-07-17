using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

[Collection("DebugLogger tests")]
public class PersistentPowerShellSessionTraceTests
{
    [Fact]
    public void IsEnabled_DefaultsToFalse_WhenNoOverrideOrEnvironmentValue()
    {
        var previous = Environment.GetEnvironmentVariable(PersistentPowerShellSessionTrace.EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(PersistentPowerShellSessionTrace.EnvironmentVariableName, null);
            PersistentPowerShellSessionTrace.SetEnabledForTests(null);

            Assert.False(PersistentPowerShellSessionTrace.IsEnabled());
        }
        finally
        {
            PersistentPowerShellSessionTrace.SetEnabledForTests(null);
            Environment.SetEnvironmentVariable(PersistentPowerShellSessionTrace.EnvironmentVariableName, previous);
        }
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("yes")]
    [InlineData("on")]
    public void IsEnabled_ReadsTruthyEnvironmentValues(string value)
    {
        var previous = Environment.GetEnvironmentVariable(PersistentPowerShellSessionTrace.EnvironmentVariableName);
        try
        {
            PersistentPowerShellSessionTrace.SetEnabledForTests(null);
            Environment.SetEnvironmentVariable(PersistentPowerShellSessionTrace.EnvironmentVariableName, value);

            Assert.True(PersistentPowerShellSessionTrace.IsEnabled());
        }
        finally
        {
            PersistentPowerShellSessionTrace.SetEnabledForTests(null);
            Environment.SetEnvironmentVariable(PersistentPowerShellSessionTrace.EnvironmentVariableName, previous);
        }
    }

    [Fact]
    public void Log_WritesToDebugLoggerOnlyWhenEnabled()
    {
        var logger = new CollectingStructuredLogger();
        try
        {
            DebugLogger.ConfigureStructuredSink(logger);

            PersistentPowerShellSessionTrace.SetEnabledForTests(false);
            PersistentPowerShellSessionTrace.Log("suppressed");

            Assert.Empty(logger.Events);

            PersistentPowerShellSessionTrace.SetEnabledForTests(true);
            PersistentPowerShellSessionTrace.Log("visible");

            var e = Assert.Single(logger.Events);
            Assert.Equal($"0x{LaStatus.DiagDebug_DebugTrace:X8}", e.Code);
            Assert.Equal("[PowerShellWrapperTrace] visible", e.Context!["message"]);
            Assert.DoesNotContain(logger.Events, x => ((string?)x.Context!["message"])!.Contains("suppressed"));
        }
        finally
        {
            PersistentPowerShellSessionTrace.SetEnabledForTests(null);
            ResetDebugLoggerSink();
        }
    }

    private static void ResetDebugLoggerSink()
    {
        var method = typeof(DebugLogger).GetMethod("ResetForTests", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(null, System.Array.Empty<object>());
    }
}
