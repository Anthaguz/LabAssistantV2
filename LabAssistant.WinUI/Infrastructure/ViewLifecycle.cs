using System;
using System.Threading.Tasks;
using LabAssistant.WinUI.Diagnostics;

namespace LabAssistant.WinUI.Infrastructure;

/// <summary>
/// Runs view lifecycle work (InitializeAsync/CleanupAsync and similar) from event handlers without letting a
/// fault escape as an unobserved async-void exception. An exception thrown from one of these handlers on the
/// UI thread would otherwise reach <c>Application.UnhandledException</c> and crash the process (for example on
/// the very first navigation to a page). Faults are logged and swallowed so a single view's initialization
/// failure degrades that view rather than terminating the app.
/// </summary>
public static class ViewLifecycle
{
    /// <summary>
    /// Fire-and-forget a lifecycle task with structured exception isolation. Intended for wiring
    /// <c>Loaded</c>/<c>Unloaded</c> and timer handlers that would otherwise be raw <c>async void</c> lambdas.
    /// </summary>
    /// <param name="action">The lifecycle work to run.</param>
    /// <param name="context">A short source label used when logging a fault.</param>
    public static async void Run(Func<Task> action, string context)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Expected when a view is torn down mid-initialization; not a fault.
        }
        catch (Exception ex)
        {
            StartupCrashLogger.LogException($"ViewLifecycle.{context}", ex);
        }
    }
}
