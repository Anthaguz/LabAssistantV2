namespace LabAssistant.WinUI.Navigation;

/// <summary>
/// Provides Frame-based navigation with route resolution, back-stack, and lifecycle hooks.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigates to a registered route.
    /// </summary>
    /// <param name="routeKey">The route key to navigate to.</param>
    /// <param name="parameter">An optional parameter passed to the destination page.</param>
    /// <returns><see langword="true"/> when navigation succeeds; otherwise, <see langword="false"/>.</returns>
    bool NavigateTo(string routeKey, object? parameter = null);

    /// <summary>
    /// Gets a value indicating whether back navigation is available.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Navigates back in the stack.
    /// </summary>
    void GoBack();

    /// <summary>
    /// Gets the current active route key.
    /// </summary>
    string CurrentRoute { get; }

    /// <summary>
    /// Raised after navigation completes.
    /// </summary>
    event EventHandler<NavigatedEventArgs>? Navigated;
}

/// <summary>
/// Describes a completed navigation operation.
/// </summary>
public class NavigatedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the resolved route key.
    /// </summary>
    public string RouteKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the destination page type.
    /// </summary>
    public Type PageType { get; init; } = typeof(object);

    /// <summary>
    /// Gets the navigation parameter.
    /// </summary>
    public object? Parameter { get; init; }
}
