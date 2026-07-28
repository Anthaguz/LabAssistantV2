using LabAssistant.UITesting.Infrastructure;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Parse tests for the in-guest router-state readout. The probe itself shells into a VM over
/// PowerShell Direct, but the KEY=value parsing (and the fully-configured gate) is pure and is pinned
/// here against synthetic stdout, so the router scenario's RRAS/forwarding/NAT assertions can't drift.
/// </summary>
public sealed class GuestRouterProbeTests
{
    private static string Readout(string remoteAccess, string routing, string forwarding, string nat)
        => string.Join(
            "\r\n",
            $"REMOTEACCESS={remoteAccess}",
            $"ROUTING={routing}",
            $"FORWARDING={forwarding}",
            $"NATINSTALLED={nat}");

    [Fact]
    public void Parses_a_fully_configured_router()
    {
        bool ok = GuestRouterProbe.TryParse(Readout("Installed", "Installed", "2", "True"), out GuestRouterState state);

        Assert.True(ok);
        Assert.True(state.RemoteAccessInstalled);
        Assert.True(state.RoutingInstalled);
        Assert.Equal(2, state.Ipv4ForwardingEnabledCount);
        Assert.True(state.NatInstalled);
        Assert.True(state.IsFullyConfigured);
    }

    [Fact]
    public void IsFullyConfigured_false_when_forwarding_absent()
    {
        GuestRouterProbe.TryParse(Readout("Installed", "Installed", "0", "True"), out GuestRouterState state);
        Assert.False(state.IsFullyConfigured);
    }

    [Fact]
    public void IsFullyConfigured_false_when_nat_not_installed()
    {
        GuestRouterProbe.TryParse(Readout("Installed", "Installed", "1", "False"), out GuestRouterState state);
        Assert.False(state.IsFullyConfigured);
    }

    [Fact]
    public void IsFullyConfigured_false_when_a_feature_not_installed()
    {
        GuestRouterProbe.TryParse(Readout("Available", "Installed", "1", "True"), out GuestRouterState state);
        Assert.False(state.RemoteAccessInstalled);
        Assert.False(state.IsFullyConfigured);
    }

    [Fact]
    public void Feature_state_and_nat_flag_are_case_insensitive()
    {
        GuestRouterProbe.TryParse(Readout("installed", "INSTALLED", "3", "true"), out GuestRouterState state);
        Assert.True(state.IsFullyConfigured);
    }

    [Fact]
    public void Unparseable_readout_returns_false()
    {
        bool ok = GuestRouterProbe.TryParse("something went wrong", out GuestRouterState state);
        Assert.False(ok);
        Assert.False(state.IsFullyConfigured);
    }

    [Fact]
    public void Non_numeric_forwarding_counts_as_zero()
    {
        bool ok = GuestRouterProbe.TryParse(Readout("Installed", "Installed", "notanumber", "True"), out GuestRouterState state);
        Assert.True(ok);
        Assert.Equal(0, state.Ipv4ForwardingEnabledCount);
        Assert.False(state.IsFullyConfigured);
    }
}
