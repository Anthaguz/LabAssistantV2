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
        var directory = Path.Combine(Path.GetTempPath(), $"labassistant-ps-trace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var activeLog = Path.Combine(directory, DebugLoggingDefaults.DebugLogFileName);

        try
        {
            DebugLogger.SetLogFolder(directory);
            PersistentPowerShellSessionTrace.SetEnabledForTests(false);
            PersistentPowerShellSessionTrace.Log("suppressed");

            Assert.False(File.Exists(activeLog));

            PersistentPowerShellSessionTrace.SetEnabledForTests(true);
            PersistentPowerShellSessionTrace.Log("visible");

            Assert.True(File.Exists(activeLog));
            var content = File.ReadAllText(activeLog);
            Assert.Contains("[PowerShellWrapperTrace] visible", content, StringComparison.Ordinal);
            Assert.DoesNotContain("suppressed", content, StringComparison.Ordinal);
        }
        finally
        {
            PersistentPowerShellSessionTrace.SetEnabledForTests(null);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
