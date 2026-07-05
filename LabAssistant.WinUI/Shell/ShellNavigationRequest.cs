namespace LabAssistant.WinUI.Shell;

/// <summary>
/// Navigation parameter passed to a capability page through the capability frame. Carries the
/// shell seam the page coordinates through and the initial subview route to display.
/// </summary>
internal sealed record ShellNavigationRequest(IShellHost Host, string RouteKey);
