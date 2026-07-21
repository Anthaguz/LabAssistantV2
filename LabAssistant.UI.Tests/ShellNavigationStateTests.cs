using LabAssistant.WinUI.Shell;
using LabAssistant.WinUI.ViewModels;
using Xunit;

namespace LabAssistant.UI.Tests;

/// <summary>
/// Runtime-independent seam tests for <see cref="ShellNavigationState"/>: the pure routing and
/// transition logic that <see cref="ShellNavigationCoordinator"/> applies to the real WinUI
/// shell. No WinUI types are involved, so these run without a WinUI runtime.
/// </summary>
public sealed class ShellNavigationStateTests
{
    // Distinct marker types per capability so tests can assert "the page type actually changed"
    // rather than just "a page type was returned".
    private sealed class MachinesPageMarker;
    private sealed class DeployPageMarker;
    private sealed class TemplatesPageMarker;
    private sealed class AssetsPageMarker;
    private sealed class DiagnosticsPageMarker;
    private sealed class SettingsPageMarker;

    private static IReadOnlyDictionary<string, Type> AllCapabilityPageTypes() => new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        ["machines"] = typeof(MachinesPageMarker),
        ["deploy"] = typeof(DeployPageMarker),
        ["templates"] = typeof(TemplatesPageMarker),
        ["assets"] = typeof(AssetsPageMarker),
        ["diagnostics"] = typeof(DiagnosticsPageMarker),
        ["settings"] = typeof(SettingsPageMarker)
    };

    private static ShellNavigationState CreateState(IReadOnlyDictionary<string, Type>? capabilityPageTypes = null) =>
        new(new ShellViewModel(), capabilityPageTypes ?? AllCapabilityPageTypes());

    [Fact]
    public void Constructor_StartsOnMachinesOverview()
    {
        var state = CreateState();

        Assert.Equal(ShellRouteKeys.MachinesOverview, state.ActiveRouteKey);
        Assert.Equal("machines", state.ActiveCapability.Key);
        Assert.Equal(ShellRouteKeys.MachinesOverview, state.ActiveSubview.RouteKey);
    }

    [Theory]
    [InlineData("machines", typeof(MachinesPageMarker))]
    [InlineData("deploy", typeof(DeployPageMarker))]
    [InlineData("templates", typeof(TemplatesPageMarker))]
    [InlineData("assets", typeof(AssetsPageMarker))]
    [InlineData("diagnostics", typeof(DiagnosticsPageMarker))]
    [InlineData("settings", typeof(SettingsPageMarker))]
    public void ResolvePageType_ReturnsRegisteredTypeForEachCapability(string capabilityKey, Type expectedPageType)
    {
        var state = CreateState();

        Assert.Equal(expectedPageType, state.ResolvePageType(capabilityKey));
    }

    [Fact]
    public void ResolvePageType_UnregisteredCapability_ThrowsInvalidOperationException()
    {
        var state = CreateState(new Dictionary<string, Type>(StringComparer.Ordinal));

        var ex = Assert.Throws<InvalidOperationException>(() => state.ResolvePageType("machines"));
        Assert.Contains("machines", ex.Message, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> AllRoutes()
    {
        var shellViewModel = new ShellViewModel();
        foreach (var capability in shellViewModel.Capabilities)
        {
            foreach (var subview in capability.Subviews)
            {
                yield return [subview.RouteKey, capability.Key];
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public void TryChangeRoute_ResolvesEveryRouteToItsCapabilityPageType(string routeKey, string expectedCapabilityKey)
    {
        var state = CreateState();

        // Force a transition away from the route under test first, so this always exercises a
        // real transition rather than a same-route no-op.
        var awayRoute = string.Equals(routeKey, ShellRouteKeys.SettingsMachines, StringComparison.Ordinal)
            ? ShellRouteKeys.MachinesOverview
            : ShellRouteKeys.SettingsMachines;
        state.TryChangeRoute(awayRoute);

        var transition = state.TryChangeRoute(routeKey);

        Assert.NotNull(transition);
        Assert.Equal(expectedCapabilityKey, transition!.Capability.Key);
        Assert.Equal(routeKey, transition.RouteKey);
        Assert.Equal(state.ResolvePageType(expectedCapabilityKey), transition.PageType);
    }

    [Fact]
    public void TryChangeRoute_UnresolvableRoute_ReturnsNull()
    {
        var state = CreateState();

        Assert.Null(state.TryChangeRoute("not.a.real.route"));
    }

    [Fact]
    public void TryChangeRoute_SameRouteAlreadyActive_ReturnsNullAndLeavesStateUnchanged()
    {
        var state = CreateState();

        var transition = state.TryChangeRoute(ShellRouteKeys.MachinesOverview);

        Assert.Null(transition);
        Assert.Equal(ShellRouteKeys.MachinesOverview, state.ActiveRouteKey);
    }

    [Fact]
    public void TryChangeRoute_SubviewChangeWithinSameCapability_DoesNotReportCapabilityChanged()
    {
        var state = CreateState();
        state.TryChangeRoute(ShellRouteKeys.DeployOverview);

        var transition = state.TryChangeRoute(ShellRouteKeys.DeployQuickDeploy);

        Assert.NotNull(transition);
        Assert.False(transition!.CapabilityChanged);
        Assert.Equal("deploy", transition.Capability.Key);
        Assert.Equal(typeof(DeployPageMarker), transition.PageType);
    }

    [Fact]
    public void TryChangeRoute_CapabilityChange_TeardownOnLeave_AlwaysYieldsDifferentPageType()
    {
        var state = CreateState();
        var previousPageType = state.ResolvePageType(state.ActiveCapability.Key);

        var transition = state.TryChangeRoute(ShellRouteKeys.DeployOverview);

        Assert.NotNull(transition);
        Assert.True(transition!.CapabilityChanged);
        Assert.NotEqual(previousPageType, transition.PageType);
    }

    [Fact]
    public void BreadcrumbText_SingleSubviewCapability_ShowsCapabilityNameOnly()
    {
        // Machines has a single subview and no distinct sub-page, so the breadcrumb must not
        // append the placeholder "/ Overview" segment.
        var state = CreateState();

        Assert.Equal("Machines", state.BreadcrumbText);
    }

    [Fact]
    public void BreadcrumbText_MultiSubviewCapability_ShowsCapabilityAndSubview()
    {
        var state = CreateState();
        state.TryChangeRoute(ShellRouteKeys.DeployQuickDeploy);

        Assert.Equal("Deploy / Quick Deploy", state.BreadcrumbText);
    }

    [Fact]
    public void BreadcrumbText_SettingsSingleSubview_ShowsCapabilityNameOnly()
    {
        var state = CreateState();
        state.TryChangeRoute(ShellRouteKeys.SettingsMachines);

        Assert.Equal("Settings", state.BreadcrumbText);
    }

    [Fact]
    public void ReportActiveSubview_SameRouteAlreadyActive_IsNoOp()
    {
        var state = CreateState();

        var changed = state.ReportActiveSubview(ShellRouteKeys.MachinesOverview);

        Assert.False(changed);
        Assert.Equal(ShellRouteKeys.MachinesOverview, state.ActiveRouteKey);
    }

    [Fact]
    public void ReportActiveSubview_UnresolvableRoute_IsNoOp()
    {
        var state = CreateState();

        var changed = state.ReportActiveSubview("not.a.real.route");

        Assert.False(changed);
        Assert.Equal(ShellRouteKeys.MachinesOverview, state.ActiveRouteKey);
    }

    [Fact]
    public void ReportActiveSubview_DifferentCapability_IsNoOp()
    {
        var state = CreateState();

        // Active capability is still "machines"; reporting a Deploy route should be rejected
        // since a page can only report subviews within its own capability.
        var changed = state.ReportActiveSubview(ShellRouteKeys.DeployOverview);

        Assert.False(changed);
        Assert.Equal("machines", state.ActiveCapability.Key);
        Assert.Equal(ShellRouteKeys.MachinesOverview, state.ActiveRouteKey);
    }

    [Fact]
    public void ReportActiveSubview_SameCapabilityDifferentSubview_UpdatesActiveState()
    {
        var state = CreateState();
        state.TryChangeRoute(ShellRouteKeys.DeployOverview);

        var changed = state.ReportActiveSubview(ShellRouteKeys.DeployQuickDeploy);

        Assert.True(changed);
        Assert.Equal("deploy", state.ActiveCapability.Key);
        Assert.Equal(ShellRouteKeys.DeployQuickDeploy, state.ActiveRouteKey);
        Assert.Equal(ShellRouteKeys.DeployQuickDeploy, state.ActiveSubview.RouteKey);
    }
}
