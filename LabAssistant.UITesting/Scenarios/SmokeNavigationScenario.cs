using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Pages;
using LabAssistant.UITesting.Runner;

namespace LabAssistant.UITesting.Scenarios;

/// <summary>
/// Visits every top-level capability, screenshots each, and records a failure if
/// a screen cannot be reached, the app crashes, or an unexpected modal dialog
/// appears. Needs no Hyper-V resources, so it is the cheapest nightly smoke pass
/// and the first line of defense against regressions in navigation and shell.
/// </summary>
public sealed class SmokeNavigationScenario : IScenario
{
    public string Name => "smoke-navigation";

    public string Capability => "Shell";

    public ScenarioRequirements Requirements => ScenarioRequirements.None;

    public void Run(ScenarioContext context)
    {
        var nav = new ShellNav(context.Host);

        foreach (var capability in ShellNav.Capabilities)
        {
            if (context.Host.Application.HasExited)
            {
                context.Recorder.Record(new Finding
                {
                    Scenario = Name,
                    Step = capability,
                    Severity = FindingSeverity.Crash,
                    Title = "App exited during navigation smoke",
                    Detail = $"The app process was gone before navigating to '{capability}'."
                });
                return;
            }

            try
            {
                nav.NavigateTo(capability);

                // Give the view a moment to render its content before evidence.
                Thread.Sleep(600);

                string title = nav.CurrentTitle();
                string? shot = context.Recorder.Capture(context.Host, $"nav-{capability}");

                context.Recorder.Record(new Finding
                {
                    Scenario = Name,
                    Step = capability,
                    Severity = FindingSeverity.Info,
                    Title = $"Visited {capability} (title: '{title}')",
                    Detail = $"Navigated to the {capability} capability successfully.",
                    ScreenshotFile = shot is null ? null : Path.GetRelativePath(context.Recorder.RunDir, shot)
                });

                CheckForUnexpectedDialog(context, capability);
            }
            catch (TimeoutException ex)
            {
                context.Recorder.RecordFailure(
                    context.Host, Name, capability, FindingSeverity.Error,
                    $"Could not reach {capability}",
                    $"Navigation to '{capability}' timed out. The nav item or its content header was not found.",
                    ex);
            }
        }
    }

    /// <summary>
    /// A visible ContentDialog appearing during plain navigation is almost always
    /// a bug (an unhandled error surfaced to the user). Hidden popup hosts (combo
    /// dropdowns, light-dismiss layers) are ignored via the offscreen check.
    /// </summary>
    private void CheckForUnexpectedDialog(ScenarioContext context, string capability)
    {
        var dialog = context.Host.MainWindow.FindFirstDescendant(cf =>
            cf.ByClassName("ContentDialog"));

        if (dialog is null)
        {
            return;
        }

        bool offscreen;
        try
        {
            offscreen = dialog.IsOffscreen;
        }
        catch
        {
            offscreen = false;
        }

        if (offscreen)
        {
            return;
        }

        context.Recorder.RecordFailure(
            context.Host, Name, capability, FindingSeverity.Warning,
            $"Unexpected dialog on {capability}",
            $"A ContentDialog was visible after navigating to '{capability}' with no user action. " +
            $"Dialog name: '{dialog.SafeName()}'.");
    }
}
