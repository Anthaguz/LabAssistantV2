using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using LabAssistant.UITesting.Infrastructure;

namespace LabAssistant.UITesting.Pages;

/// <summary>
/// Drives the top-level shell: the left NavigationView that routes between the
/// app's capabilities (Machines, Deploy, Templates, Assets, Diagnostics, Settings).
/// </summary>
public sealed class ShellNav
{
    private readonly AppHost _host;

    public ShellNav(AppHost host) => _host = host;

    public static readonly IReadOnlyList<string> Capabilities = new[]
    {
        "Machines", "Deploy", "Templates", "Assets", "Diagnostics", "Settings"
    };

    /// <summary>
    /// Navigates to a capability by its nav-item Name and waits for the content
    /// header to reflect the switch. Throws if the item can't be found.
    /// </summary>
    public void NavigateTo(string capability)
    {
        // The shell's NavigationView runs in LeftCompact mode with the built-in toggle
        // hidden, so top-level items render icon-only and their labels aren't reliably
        // addressable by name until the pane is opened. Open it via the custom hamburger
        // (x:Name -> AutomationId "HamburgerButton") first, then look the item up. Both
        // steps retry because the shell populates its nav asynchronously after launch.
        EnsurePaneOpen();

        var found = Retry.WhileNull(() => _host.MainWindow.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.ListItem).And(cf.ByName(capability))),
            TimeSpan.FromSeconds(30));

        var item = found.Result;
        if (item is null)
        {
            // A second attempt: the pane may have been open (hamburger closed it), so toggle again.
            EnsurePaneOpen(forceToggle: true);
            item = Retry.WhileNull(() => _host.MainWindow.FindFirstDescendant(cf =>
                    cf.ByControlType(ControlType.ListItem).And(cf.ByName(capability))),
                TimeSpan.FromSeconds(15)).Result
                ?? throw new InvalidOperationException(
                    $"Nav item '{capability}' not found after opening the navigation pane.");
        }

        item.Activate();

        // The shell publishes the current route into CurrentRouteTextBlock / the
        // content title; wait for either to reflect the requested capability.
        Retry.WhileFalse(() =>
        {
            var route = _host.MainWindow.ByAutomationId("CurrentRouteTextBlock")?.SafeName();
            var title = _host.MainWindow.ByAutomationId("ContentTitleTextBlock")?.SafeName();
            return string.Equals(route, capability, StringComparison.OrdinalIgnoreCase)
                || string.Equals(title, capability, StringComparison.OrdinalIgnoreCase);
        }, TimeSpan.FromSeconds(10));
    }

    /// <summary>Returns the current content title text, or empty.</summary>
    public string CurrentTitle()
        => _host.MainWindow.ByAutomationId("ContentTitleTextBlock")?.SafeName() ?? string.Empty;

    /// <summary>
    /// Opens the navigation pane by clicking the shell's custom hamburger button so
    /// top-level nav items expand to their labelled state. The button toggles, so this
    /// clicks at most once unless <paramref name="forceToggle"/> asks for another toggle.
    /// Best-effort: if the button can't be found the caller's retry still gets a chance.
    /// </summary>
    private void EnsurePaneOpen(bool forceToggle = false)
    {
        var hamburger = Retry.WhileNull(
            () => _host.MainWindow.ByAutomationId("HamburgerButton"),
            TimeSpan.FromSeconds(30)).Result;

        if (hamburger is null)
        {
            return;
        }

        hamburger.AsButton().Invoke();
        System.Threading.Thread.Sleep(400);

        if (forceToggle)
        {
            hamburger.AsButton().Invoke();
            System.Threading.Thread.Sleep(400);
        }
    }
}
