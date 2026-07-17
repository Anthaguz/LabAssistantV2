using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;

namespace LabAssistant.Services.HyperV;

/// <summary>
/// Raw PowerShell timing tracer. Each sample is emitted into the structured logging pipeline as a coded
/// <c>diag.ps_timing</c> event (session or command), preserving the original pattern/name/duration/success as
/// structured fields and the real call site. These are Debug severity, so they are off by default.
/// </summary>
public static class HyperVPowerShellTimingLogger
{
    public static void LogWorkflowSessionCreated(
        string vmName,
        long sessionCreationDurationMs,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0)
    {
        EmitSession(
            pattern: "workflow_session",
            result: "session_created",
            new Dictionary<string, object?>
            {
                ["vmName"] = vmName,
                ["sessionCreationDurationMs"] = sessionCreationDurationMs
            },
            file, member, line);
    }

    public static void LogWorkflowCommand(
        string commandName,
        long commandDurationMs,
        bool success,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0)
    {
        EmitCommand(
            pattern: "workflow_session",
            result: "command_executed",
            new Dictionary<string, object?>
            {
                ["command"] = commandName,
                ["commandDurationMs"] = commandDurationMs,
                ["success"] = success
            },
            file, member, line);
    }

    public static void LogQuerySessionCreated(
        string queryName,
        long sessionCreationDurationMs,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0)
    {
        EmitSession(
            pattern: "query_session",
            result: "session_created",
            new Dictionary<string, object?>
            {
                ["query"] = queryName,
                ["sessionCreationDurationMs"] = sessionCreationDurationMs
            },
            file, member, line);
    }

    public static void LogQueryExecution(
        string queryName,
        bool sessionCreated,
        long commandDurationMs,
        bool success,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0)
    {
        EmitCommand(
            pattern: "query_session",
            result: "query_executed",
            new Dictionary<string, object?>
            {
                ["query"] = queryName,
                ["sessionCreated"] = sessionCreated,
                ["commandDurationMs"] = commandDurationMs,
                ["success"] = success
            },
            file, member, line);
    }

    public static void LogAdministrativeCommand(
        string commandName,
        long sessionCreationDurationMs,
        long commandDurationMs,
        bool success,
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0)
    {
        EmitCommand(
            pattern: "administrative_command",
            result: "command_executed",
            new Dictionary<string, object?>
            {
                ["command"] = commandName,
                ["sessionCreationDurationMs"] = sessionCreationDurationMs,
                ["commandDurationMs"] = commandDurationMs,
                ["success"] = success
            },
            file, member, line);
    }

    private static void EmitSession(
        string pattern,
        string result,
        Dictionary<string, object?> context,
        string file,
        string member,
        int line)
    {
        context["pattern"] = pattern;
        DebugLogger.EmitCoded(LaStatus.DiagPsTiming_SessionTiming, result, message: null, context, file, member, line);
    }

    private static void EmitCommand(
        string pattern,
        string result,
        Dictionary<string, object?> context,
        string file,
        string member,
        int line)
    {
        context["pattern"] = pattern;
        DebugLogger.EmitCoded(LaStatus.DiagPsTiming_CommandTiming, result, message: null, context, file, member, line);
    }
}
