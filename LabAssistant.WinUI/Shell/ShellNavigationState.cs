using LabAssistant.WinUI.ViewModels;

namespace LabAssistant.WinUI.Shell;

/// <summary>
/// Pure, runtime-independent shell routing state and transition logic. Owns the active
/// capability/subview/route and the capability-to-page-type map, and computes what
/// <see cref="ShellNavigationCoordinator"/> should do about a route change without touching any
/// WinUI control. Kept free of <c>Microsoft.UI.Xaml</c> types so it is unit-testable without a
/// WinUI runtime.
/// </summary>
internal sealed class ShellNavigationState
{
    private readonly ShellViewModel _shellViewModel;
    private readonly IReadOnlyDictionary<string, Type> _capabilityPageTypes;

    public ShellNavigationState(ShellViewModel shellViewModel, IReadOnlyDictionary<string, Type> capabilityPageTypes)
    {
        _shellViewModel = shellViewModel;
        _capabilityPageTypes = capabilityPageTypes;

        ActiveRouteKey = shellViewModel.StartupRoute;
        shellViewModel.TryResolveRoute(ActiveRouteKey, out var capability, out var subview);
        ActiveCapability = capability;
        ActiveSubview = subview;
    }

    public ShellCapability ActiveCapability { get; private set; }

    public ShellSubview ActiveSubview { get; private set; }

    public string ActiveRouteKey { get; private set; }

    /// <summary>
    /// Human-readable breadcrumb for the active route. A capability with a single subview (for
    /// example Machines) has no distinct sub-page, so its breadcrumb is just the capability name;
    /// appending the placeholder "/ Overview" segment would imply a sub-page that does not exist.
    /// Capabilities with real sibling subviews render "Capability / Subview".
    /// </summary>
    public string BreadcrumbText => ActiveCapability.Subviews.Count > 1
        ? $"{ActiveCapability.DisplayName} / {ActiveSubview.DisplayName}"
        : ActiveCapability.DisplayName;

    /// <summary>
    /// Resolves the capability page type registered for a capability key. Every capability the
    /// shell knows about (<see cref="ShellViewModel.Capabilities"/>) must have an entry here;
    /// a miss means a capability was added without registering its page, so this throws instead
    /// of surfacing an opaque dictionary-lookup failure.
    /// </summary>
    public Type ResolvePageType(string capabilityKey)
    {
        if (!_capabilityPageTypes.TryGetValue(capabilityKey, out var pageType))
        {
            throw new InvalidOperationException($"No capability page registered for '{capabilityKey}'.");
        }

        return pageType;
    }

    /// <summary>
    /// Attempts to move to a new route. Returns <see langword="null"/> when the route can't be
    /// resolved, or when it resolves to the already-active capability and subview (no-op).
    /// Otherwise updates the active state and returns the transition the coordinator should
    /// apply to the capability frame and shell chrome.
    /// </summary>
    public ShellRouteTransition? TryChangeRoute(string routeKey)
    {
        if (!_shellViewModel.TryResolveRoute(routeKey, out var capability, out var subview))
        {
            return null;
        }

        var changedCapability = !string.Equals(ActiveCapability.Key, capability.Key, StringComparison.Ordinal);
        var changedSubview = !string.Equals(ActiveSubview.RouteKey, subview.RouteKey, StringComparison.Ordinal);
        if (!changedCapability && !changedSubview)
        {
            return null;
        }

        ActiveCapability = capability;
        ActiveSubview = subview;
        ActiveRouteKey = subview.RouteKey;

        return new ShellRouteTransition(capability, subview, subview.RouteKey, changedCapability, ResolvePageType(capability.Key));
    }

    /// <summary>
    /// Applies a subview change a live page performed on its own (for example via an internal
    /// tab) to the active state, without re-navigating the capability frame. Returns
    /// <see langword="false"/> when the report is a no-op: the route is already active, it can't
    /// be resolved, or it belongs to a different capability than the one currently active (a page
    /// may only report subviews within its own capability).
    /// </summary>
    public bool ReportActiveSubview(string routeKey)
    {
        if (string.Equals(routeKey, ActiveRouteKey, StringComparison.Ordinal) ||
            !_shellViewModel.TryResolveRoute(routeKey, out var capability, out var subview) ||
            !string.Equals(capability.Key, ActiveCapability.Key, StringComparison.Ordinal))
        {
            return false;
        }

        ActiveSubview = subview;
        ActiveRouteKey = routeKey;
        return true;
    }
}

/// <summary>
/// Describes what the coordinator should do after a successful
/// <see cref="ShellNavigationState.TryChangeRoute"/>: the newly active capability/subview/route,
/// whether the capability changed (the right panel needs a reset), and which page type the
/// capability frame should host.
/// </summary>
internal sealed record ShellRouteTransition(
    ShellCapability Capability,
    ShellSubview Subview,
    string RouteKey,
    bool CapabilityChanged,
    Type PageType);
