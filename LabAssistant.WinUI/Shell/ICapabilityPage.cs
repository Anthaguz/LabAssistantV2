namespace LabAssistant.WinUI.Shell;

/// <summary>
/// Implemented by capability pages that host more than one subview. Lets the shell forward a
/// subview change to the already-active page without re-navigating the capability frame.
/// </summary>
internal interface ICapabilityPage
{
    /// <summary>Switches the page to the given capability-local subview route.</summary>
    void ShowSubview(string routeKey);
}
