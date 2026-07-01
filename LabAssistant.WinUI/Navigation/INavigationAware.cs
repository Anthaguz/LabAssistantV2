namespace LabAssistant.WinUI.Navigation;

/// <summary>
/// Implemented by pages that need navigation lifecycle notifications.
/// </summary>
public interface INavigationAware
{
    /// <summary>
    /// Called when navigation arrives at this page.
    /// </summary>
    /// <param name="parameter">The navigation parameter.</param>
    Task OnNavigatedToAsync(object? parameter);

    /// <summary>
    /// Called before navigation leaves this page.
    /// </summary>
    Task OnNavigatedFromAsync();
}
