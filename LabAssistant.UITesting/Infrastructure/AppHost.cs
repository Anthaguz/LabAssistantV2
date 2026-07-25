using System.Diagnostics;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>
/// Owns the lifetime of the app under test and the UI Automation session that
/// drives it. Launches (or attaches to) LabAssistant.exe, resolves the main
/// window, and guarantees the process is torn down on dispose so a nightly run
/// never leaks orphaned app instances.
/// </summary>
public sealed class AppHost : IDisposable
{
    private readonly bool _weLaunchedIt;

    private AppHost(Application application, UIA3Automation automation, Window mainWindow, bool weLaunchedIt)
    {
        Application = application;
        Automation = automation;
        MainWindow = mainWindow;
        _weLaunchedIt = weLaunchedIt;
    }

    public Application Application { get; }
    public UIA3Automation Automation { get; }
    public Window MainWindow { get; }

    /// <summary>
    /// Launches a fresh instance of the app and waits for its main window to be
    /// ready for automation.
    /// </summary>
    public static AppHost Launch(string exePath, TimeSpan? windowTimeout = null)
    {
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"App exe not found: {exePath}", exePath);
        }

        var processStart = new ProcessStartInfo(exePath)
        {
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            UseShellExecute = false
        };

        var application = Application.Launch(processStart);
        return Attach(application, weLaunchedIt: true, windowTimeout);
    }

    /// <summary>
    /// Attaches to an already-running app instance by process id. Used when a
    /// human wants to point the harness at a session they already have open.
    /// </summary>
    public static AppHost AttachToProcess(int processId, TimeSpan? windowTimeout = null)
    {
        var application = Application.Attach(processId);
        return Attach(application, weLaunchedIt: false, windowTimeout);
    }

    private static AppHost Attach(Application application, bool weLaunchedIt, TimeSpan? windowTimeout)
    {
        var automation = new UIA3Automation();
        var timeout = windowTimeout ?? TimeSpan.FromSeconds(30);

        Window? window = application.GetMainWindow(automation, timeout);
        if (window is null)
        {
            automation.Dispose();
            if (weLaunchedIt)
            {
                TryKill(application);
            }

            throw new TimeoutException(
                $"App main window was not available for automation within {timeout.TotalSeconds:0}s.");
        }

        // Maximize so the shell renders in its wide layout with the navigation pane
        // expanded (labels visible). In the narrow default size the NavigationView
        // collapses to icon-only and nav items can't be addressed by name, which made
        // navigation flaky. Best-effort: never fail the launch just because maximize did.
        try
        {
            if (window.Patterns.Window.IsSupported)
            {
                window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
                System.Threading.Thread.Sleep(500);
            }
        }
        catch
        {
            // A non-maximizable window still works; nav lookups retry regardless.
        }

        return new AppHost(application, automation, window, weLaunchedIt);
    }

    /// <summary>
    /// Produces a human-readable dump of the automation tree from the given root
    /// (defaults to the main window). This is the primary way to discover the
    /// selectors - names, automation ids, control types - the harness can target.
    /// </summary>
    public string DumpTree(AutomationElement? root = null, int maxDepth = 40)
    {
        var start = root ?? MainWindow;
        var builder = new StringBuilder();
        DumpElement(start, 0, maxDepth, builder);
        return builder.ToString();
    }

    private void DumpElement(AutomationElement element, int depth, int maxDepth, StringBuilder builder)
    {
        if (depth > maxDepth)
        {
            return;
        }

        string indent = new(' ', depth * 2);
        string controlType = SafeControlType(element);
        string name = Safe(() => element.Name);
        string automationId = Safe(() => element.AutomationId);
        string className = Safe(() => element.ClassName);

        builder.Append(indent)
            .Append('[').Append(controlType).Append(']')
            .Append(" Name=\"").Append(name).Append('"');

        if (!string.IsNullOrEmpty(automationId))
        {
            builder.Append(" AutomationId=\"").Append(automationId).Append('"');
        }

        if (!string.IsNullOrEmpty(className))
        {
            builder.Append(" Class=\"").Append(className).Append('"');
        }

        builder.AppendLine();

        AutomationElement[] children;
        try
        {
            children = element.FindAllChildren();
        }
        catch
        {
            return;
        }

        foreach (var child in children)
        {
            DumpElement(child, depth + 1, maxDepth, builder);
        }
    }

    private static string SafeControlType(AutomationElement element)
    {
        try
        {
            ControlType ct = element.ControlType;
            return ct.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string Safe(Func<string> getter)
    {
        try
        {
            return getter() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void TryKill(Application application)
    {
        try
        {
            application.Close();
        }
        catch
        {
            // best effort
        }

        try
        {
            application.Kill();
        }
        catch
        {
            // best effort
        }

        // Wait for the process to actually exit so its file handles (open VHDX,
        // config) are released before the caller sweeps disk files. Without this the
        // sweep races the dying process and hits "file in use" locks.
        try
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
            while (DateTime.UtcNow < deadline && !application.HasExited)
            {
                Thread.Sleep(250);
            }
        }
        catch
        {
            // best effort
        }
    }

    public void Dispose()
    {
        // Only tear down the process if we started it; if we attached to a
        // human's session, leave it running.
        if (_weLaunchedIt)
        {
            TryKill(Application);
        }

        Automation.Dispose();
    }
}
