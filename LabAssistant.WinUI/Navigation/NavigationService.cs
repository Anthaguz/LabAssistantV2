using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace LabAssistant.WinUI.Navigation;

/// <summary>
/// Provides route-driven <see cref="Frame"/> navigation with back-stack and page lifecycle handling.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly RouteRegistry _routeRegistry;
    private Frame? _frame;
    private string _currentRoute = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationService"/> class.
    /// </summary>
    /// <param name="routeRegistry">The route registry used for route resolution.</param>
    public NavigationService(RouteRegistry routeRegistry)
    {
        _routeRegistry = routeRegistry ?? throw new ArgumentNullException(nameof(routeRegistry));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationService"/> class and attaches a frame.
    /// </summary>
    /// <param name="routeRegistry">The route registry used for route resolution.</param>
    /// <param name="frame">The frame that hosts page navigation.</param>
    public NavigationService(RouteRegistry routeRegistry, Frame frame)
        : this(routeRegistry)
    {
        AttachFrame(frame);
    }

    /// <summary>
    /// Gets or sets the transition used for forward navigation.
    /// </summary>
    public NavigationTransitionInfo ForwardTransition { get; set; } = NavigationTransitions.Default;

    /// <summary>
    /// Gets or sets the transition used for back navigation.
    /// </summary>
    public NavigationTransitionInfo BackTransition { get; set; } = NavigationTransitions.SlideFromLeft;

    /// <inheritdoc />
    public bool CanGoBack => _frame?.CanGoBack ?? false;

    /// <inheritdoc />
    public string CurrentRoute => _currentRoute;

    /// <inheritdoc />
    public event EventHandler<NavigatedEventArgs>? Navigated;

    /// <summary>
    /// Attaches the frame used for page navigation.
    /// </summary>
    /// <param name="frame">The frame to attach.</param>
    public void AttachFrame(Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (ReferenceEquals(_frame, frame))
        {
            return;
        }

        if (_frame is not null)
        {
            _frame.Navigated -= OnFrameNavigated;
        }

        _frame = frame;
        _frame.Navigated += OnFrameNavigated;
        _currentRoute = ResolveRouteKey(_frame.Content?.GetType()) ?? string.Empty;
    }

    /// <inheritdoc />
    public bool NavigateTo(string routeKey, object? parameter = null)
    {
        if (string.IsNullOrWhiteSpace(routeKey))
        {
            throw new ArgumentException("Route key cannot be null or whitespace.", nameof(routeKey));
        }

        if (!_routeRegistry.TryResolve(routeKey, out var entry))
        {
            return false;
        }

        if (!typeof(Page).IsAssignableFrom(entry.PageType))
        {
            throw new InvalidOperationException($"Route '{entry.RouteKey}' resolves to '{entry.PageType.FullName}', which is not a Page.");
        }

        var frame = EnsureFrame();
        if (!CanLeaveCurrentPage(frame))
        {
            return false;
        }

        NotifyNavigatingFrom(frame.Content);
        return frame.Navigate(entry.PageType, parameter, ForwardTransition);
    }

    /// <inheritdoc />
    public void GoBack()
    {
        var frame = EnsureFrame();
        if (!frame.CanGoBack)
        {
            return;
        }

        if (!CanLeaveCurrentPage(frame))
        {
            return;
        }

        NotifyNavigatingFrom(frame.Content);
        frame.GoBack(BackTransition);
    }

    private static bool CanLeaveCurrentPage(Frame frame)
    {
        if (frame.Content is not INavigationGuard guard)
        {
            return true;
        }

        return guard.CanNavigateAwayAsync().GetAwaiter().GetResult();
    }

    private static void NotifyNavigatingFrom(object? content)
    {
        if (content is INavigationAware aware)
        {
            aware.OnNavigatedFromAsync().GetAwaiter().GetResult();
        }
    }

    private Frame EnsureFrame()
    {
        return _frame ?? throw new InvalidOperationException("A Frame must be attached before navigation can occur.");
    }

    private void OnFrameNavigated(object sender, NavigationEventArgs e)
    {
        var routeKey = ResolveRouteKey(e.SourcePageType) ?? string.Empty;
        _currentRoute = routeKey;

        if (e.Content is INavigationAware aware)
        {
            aware.OnNavigatedToAsync(e.Parameter).GetAwaiter().GetResult();
        }

        Navigated?.Invoke(this, new NavigatedEventArgs
        {
            RouteKey = routeKey,
            PageType = e.SourcePageType ?? e.Content?.GetType() ?? typeof(object),
            Parameter = e.Parameter
        });
    }

    private string? ResolveRouteKey(Type? pageType)
    {
        if (pageType is null)
        {
            return null;
        }

        return _routeRegistry.GetAll()
            .FirstOrDefault(entry => entry.PageType == pageType)
            ?.RouteKey;
    }
}
