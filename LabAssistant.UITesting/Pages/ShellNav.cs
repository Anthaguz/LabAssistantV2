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
        var item = _host.MainWindow.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.ListItem).And(cf.ByName(capability)))
            ?? throw new InvalidOperationException($"Nav item '{capability}' not found.");

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
}
