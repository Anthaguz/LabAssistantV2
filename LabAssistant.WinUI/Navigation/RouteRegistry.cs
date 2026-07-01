namespace LabAssistant.WinUI.Navigation;

/// <summary>
/// Maps route keys to page types and optional parent routes.
/// </summary>
public sealed class RouteRegistry
{
    private readonly Dictionary<string, RouteEntry> _routes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a route for the specified page type.
    /// </summary>
    /// <typeparam name="TPage">The page type.</typeparam>
    /// <param name="routeKey">The route key.</param>
    /// <param name="parentRoute">The optional parent route key.</param>
    /// <returns>The current registry.</returns>
    public RouteRegistry Register<TPage>(string routeKey, string? parentRoute = null)
        where TPage : class
    {
        return Register(routeKey, typeof(TPage), parentRoute);
    }

    /// <summary>
    /// Registers a route for the specified page type.
    /// </summary>
    /// <param name="routeKey">The route key.</param>
    /// <param name="pageType">The page type.</param>
    /// <param name="parentRoute">The optional parent route key.</param>
    /// <returns>The current registry.</returns>
    public RouteRegistry Register(string routeKey, Type pageType, string? parentRoute = null)
    {
        if (string.IsNullOrWhiteSpace(routeKey))
        {
            throw new ArgumentException("Route key cannot be null or whitespace.", nameof(routeKey));
        }

        ArgumentNullException.ThrowIfNull(pageType);

        if (_routes.ContainsKey(routeKey))
        {
            throw new InvalidOperationException($"Route '{routeKey}' is already registered.");
        }

        var normalizedParentRoute = string.IsNullOrWhiteSpace(parentRoute)
            ? null
            : parentRoute.Trim();

        _routes.Add(routeKey, new RouteEntry(routeKey.Trim(), pageType, normalizedParentRoute));
        return this;
    }

    /// <summary>
    /// Attempts to resolve a route entry by key.
    /// </summary>
    /// <param name="routeKey">The route key.</param>
    /// <param name="entry">The resolved route entry.</param>
    /// <returns><see langword="true"/> when the route exists; otherwise, <see langword="false"/>.</returns>
    public bool TryResolve(string routeKey, out RouteEntry entry)
    {
        if (string.IsNullOrWhiteSpace(routeKey))
        {
            entry = default!;
            return false;
        }

        return _routes.TryGetValue(routeKey.Trim(), out entry!);
    }

    /// <summary>
    /// Gets all routes whose parent matches the supplied route key.
    /// </summary>
    /// <param name="parentRoute">The parent route key.</param>
    /// <returns>The matching child routes.</returns>
    public IReadOnlyList<RouteEntry> GetChildren(string parentRoute)
    {
        if (string.IsNullOrWhiteSpace(parentRoute))
        {
            return Array.Empty<RouteEntry>();
        }

        return _routes.Values
            .Where(entry => string.Equals(entry.ParentRoute, parentRoute, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>
    /// Gets all registered routes.
    /// </summary>
    /// <returns>All registered route entries.</returns>
    public IReadOnlyList<RouteEntry> GetAll()
    {
        return _routes.Values.ToArray();
    }
}

/// <summary>
/// Represents a registered route mapping.
/// </summary>
/// <param name="RouteKey">The route key.</param>
/// <param name="PageType">The destination page type.</param>
/// <param name="ParentRoute">The optional parent route key.</param>
public record RouteEntry(string RouteKey, Type PageType, string? ParentRoute);
