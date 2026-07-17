using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using LabAssistant.Services.Diagnostics;

namespace LabAssistant.Services.Logging
{
    /// <summary>
    /// Ambient diagnostic tracer. Historically this wrote free-form lines to a separate <c>log.txt</c>; it now
    /// forwards every trace into the single structured logging pipeline as a coded <c>diag.debug.trace</c> event
    /// so all diagnostics live in one place and can be filtered by status code. The thread id, call site, and the
    /// original free-form message are preserved as structured fields.
    /// </summary>
    /// <remarks>
    /// This is a static, dependency-injection-free seam because it is called from a large number of legacy static
    /// and instance sites. <see cref="ConfigureStructuredSink"/> wires it to the application's structured logger at
    /// startup. Until it is wired (early startup, or unit tests that do not opt in) traces are silently dropped,
    /// which matches the previous behavior of a not-yet-configured log folder.
    /// </remarks>
    public static class DebugLogger
    {
        private static IStructuredLogger? _structuredLogger;

        /// <summary>
        /// Wires the ambient tracer to the application's structured logging pipeline. Call once at startup.
        /// </summary>
        public static void ConfigureStructuredSink(IStructuredLogger structuredLogger)
        {
            _structuredLogger = structuredLogger ?? throw new ArgumentNullException(nameof(structuredLogger));
        }

        public static void Log(
            string message = "",
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
        {
            EmitCoded(LaStatus.DiagDebug_DebugTrace, result: null, message: message, extraContext: null, file: file, member: member, line: line);
        }

        // Method to log both error and output obtained from powershell
        public static void LogPowerShellOutput(string command = "", string output = "", string error = "")
        {
            if (!string.IsNullOrEmpty(output))
            {
                Log($"PowerShell Output for '{command}': \n{output}");
            }
            if (!string.IsNullOrEmpty(error))
            {
                Log($"PowerShell Error for '{command}': \n{error}");
            }
        }

        /// <summary>
        /// Emits an ambient diagnostic event under the given status code, preserving the caller's thread and call
        /// site. Shared by <see cref="Log(string, string, string, int)"/> and the raw timing tracer so both land in
        /// the structured pipeline with the same shape. The caller attributes are forwarded explicitly so the
        /// recorded call site is the real emit site, not this helper.
        /// </summary>
        internal static void EmitCoded(
            uint code,
            string? result,
            string? message,
            IReadOnlyDictionary<string, object?>? extraContext,
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
        {
            var logger = _structuredLogger;
            if (logger is null)
            {
                return;
            }

            try
            {
                var context = new Dictionary<string, object?>();
                if (!string.IsNullOrEmpty(message))
                {
                    context["message"] = message;
                }
                if (!string.IsNullOrEmpty(member))
                {
                    context["member"] = member;
                }
                if (extraContext is not null)
                {
                    foreach (var pair in extraContext)
                    {
                        context[pair.Key] = pair.Value;
                    }
                }

                var callsite = $"{Path.GetFileName(file)}:{line}";
                logger.Log(StructuredLogEvent.Create(
                    code,
                    StructuredLoggingDefaults.AmbientOperationId,
                    result: result,
                    context: context.Count == 0 ? null : context,
                    callsite: callsite));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DebugLogger failed: {ex.Message}");
            }
        }

        internal static void ResetForTests()
        {
            _structuredLogger = null;
        }
    }
}
