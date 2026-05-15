using LabAssistant.Services.Logging;

namespace LabAssistant.Services.HyperV;

public static class HyperVPowerShellTimingLogger
{
    public static void LogWorkflowSessionCreated(string vmName, long sessionCreationDurationMs)
    {
        DebugLogger.Log(
            $"[HyperVPowerShellTiming] pattern=workflow_session event=session_created vmName='{vmName}' sessionCreationDurationMs={sessionCreationDurationMs}");
    }

    public static void LogWorkflowCommand(string commandName, long commandDurationMs, bool success)
    {
        DebugLogger.Log(
            $"[HyperVPowerShellTiming] pattern=workflow_session event=command_executed command='{commandName}' commandDurationMs={commandDurationMs} success={success}");
    }

    public static void LogQuerySessionCreated(string queryName, long sessionCreationDurationMs)
    {
        DebugLogger.Log(
            $"[HyperVPowerShellTiming] pattern=query_session event=session_created query='{queryName}' sessionCreationDurationMs={sessionCreationDurationMs}");
    }

    public static void LogQueryExecution(string queryName, bool sessionCreated, long commandDurationMs, bool success)
    {
        DebugLogger.Log(
            $"[HyperVPowerShellTiming] pattern=query_session event=query_executed query='{queryName}' sessionCreated={sessionCreated} commandDurationMs={commandDurationMs} success={success}");
    }

    public static void LogAdministrativeCommand(
        string commandName,
        long sessionCreationDurationMs,
        long commandDurationMs,
        bool success)
    {
        DebugLogger.Log(
            $"[HyperVPowerShellTiming] pattern=administrative_command event=command_executed command='{commandName}' sessionCreationDurationMs={sessionCreationDurationMs} commandDurationMs={commandDurationMs} success={success}");
    }
}
