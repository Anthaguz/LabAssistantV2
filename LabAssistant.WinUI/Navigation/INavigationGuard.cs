namespace LabAssistant.WinUI.Navigation;

/// <summary>
/// Implemented by pages that need to prevent navigation, such as unsaved-change prompts.
/// </summary>
public interface INavigationGuard
{
    /// <summary>
    /// Returns a value indicating whether navigation away is allowed.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when navigation may continue; otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> CanNavigateAwayAsync();
}
