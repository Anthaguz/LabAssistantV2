using LabAssistant.Services.Logging;

namespace LabAssistant.Services.PowerShell;

internal static class PersistentPowerShellSessionTrace
{
    internal const string EnvironmentVariableName = "LABASSISTANT_POWERSHELL_WRAPPER_TRACE";
    private static bool? _testOverride;

    // Wrapper protocol/lifecycle traces are disabled by default and can be enabled
    // during troubleshooting via the environment variable above.
    public static bool IsEnabled()
    {
        if (_testOverride.HasValue)
        {
            return _testOverride.Value;
        }

        var value = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    public static void Log(string message)
    {
        if (!IsEnabled())
        {
            return;
        }

        DebugLogger.Log($"[PowerShellWrapperTrace] {message}");
    }

    internal static void SetEnabledForTests(bool? enabled)
    {
        _testOverride = enabled;
    }
}
