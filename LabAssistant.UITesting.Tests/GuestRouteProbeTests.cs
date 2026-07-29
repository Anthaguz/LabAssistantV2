using LabAssistant.UITesting.Infrastructure;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Parse tests for the in-guest route-hop readout that backs the routed cross-forest "the trust crossed
/// the router" proof. The probe shells into a DC over PowerShell Direct, but the ROUTE_* parsing is pure
/// and is pinned here against synthetic stdout, so the next-hop / reachability assertions can't drift. A
/// null result is the "no off-link route to the peer" signal the scenario reports as a failure.
/// </summary>
public sealed class GuestRouteProbeTests
{
    private static string Readout(string found, string? nextHop, string reachable)
    {
        var lines = new List<string> { $"ROUTE_FOUND={found}" };
        if (nextHop is not null)
        {
            lines.Add($"ROUTE_NEXTHOP={nextHop}");
        }

        lines.Add($"ROUTE_REACHABLE={reachable}");
        return string.Join("\r\n", lines);
    }

    [Fact]
    public void Parses_an_off_link_route_via_the_router()
    {
        var info = GuestRouteProbe.TryParse(Readout("True", "10.70.0.1", "True"));

        Assert.NotNull(info);
        Assert.Equal("10.70.0.1", info!.NextHop);
        Assert.True(info.Reachable);
    }

    [Fact]
    public void Route_found_but_peer_not_reachable_yet()
    {
        var info = GuestRouteProbe.TryParse(Readout("True", "10.71.0.1", "False"));

        Assert.NotNull(info);
        Assert.Equal("10.71.0.1", info!.NextHop);
        Assert.False(info.Reachable);
    }

    [Fact]
    public void No_off_link_route_returns_null()
        => Assert.Null(GuestRouteProbe.TryParse(Readout("False", nextHop: null, "False")));

    [Fact]
    public void Found_but_missing_next_hop_returns_null()
        => Assert.Null(GuestRouteProbe.TryParse(Readout("True", nextHop: null, "True")));

    [Fact]
    public void Found_and_reachable_flags_are_case_insensitive()
    {
        var info = GuestRouteProbe.TryParse(Readout("true", "10.70.0.1", "true"));

        Assert.NotNull(info);
        Assert.True(info!.Reachable);
    }

    [Fact]
    public void Unparseable_readout_returns_null()
        => Assert.Null(GuestRouteProbe.TryParse("something went wrong"));
}
