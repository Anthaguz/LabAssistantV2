using System.Linq;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Covers the pure subnet/IP core behind the Builder's per-domain switch model: CIDR parse/format, block
/// allocation (10.0.0.0/24 -> 10.0.1.0/24 -> ...), the fixed router (.1) / host (.254) reservations, the VM
/// assignable range, next-free scanning, and last-octet composition. All integer math, no XAML host.
/// </summary>
public sealed class BuilderLabSubnetTests
{
    [Fact]
    public void TryParseCidr_NormalizesToNetworkAddress()
    {
        Assert.True(BuilderLabSubnet.TryParseCidr("10.0.5.37/24", out var subnet));
        Assert.Equal("10.0.5.0/24", subnet.ToCidrString());
        Assert.Equal(24, subnet.PrefixLength);
    }

    [Theory]
    [InlineData("")]
    [InlineData("10.0.0.0")]
    [InlineData("10.0.0.0/33")]
    [InlineData("garbage/24")]
    [InlineData("10.0.0.0/x")]
    public void TryParseCidr_RejectsInvalid(string value)
        => Assert.False(BuilderLabSubnet.TryParseCidr(value, out _));

    [Fact]
    public void AllocateAt_IncrementsTheThirdOctetForA24()
    {
        Assert.True(BuilderLabSubnet.TryParseCidr("10.0.0.0/24", out var basePlan));

        Assert.Equal("10.0.0.0/24", basePlan.AllocateAt(0).ToCidrString());
        Assert.Equal("10.0.1.0/24", basePlan.AllocateAt(1).ToCidrString());
        Assert.Equal("10.0.5.0/24", basePlan.AllocateAt(5).ToCidrString());
    }

    [Fact]
    public void AllocateAt_IncrementsTheSecondOctetForA16()
    {
        Assert.True(BuilderLabSubnet.TryParseCidr("10.0.0.0/16", out var basePlan));

        Assert.Equal("10.0.0.0/16", basePlan.AllocateAt(0).ToCidrString());
        Assert.Equal("10.1.0.0/16", basePlan.AllocateAt(1).ToCidrString());
        Assert.Equal("10.3.0.0/16", basePlan.AllocateAt(3).ToCidrString());
    }

    [Fact]
    public void ReservedAddresses_FollowRouterFirstHostLastPolicy()
    {
        BuilderLabSubnet.TryParseCidr("10.0.5.0/24", out var subnet);

        Assert.Equal("10.0.5.1", BuilderLabSubnet.FormatAddress(subnet.RouterAddress!.Value));
        Assert.Equal("10.0.5.254", BuilderLabSubnet.FormatAddress(subnet.HostReservedAddress!.Value));
        Assert.Equal("10.0.5.2", BuilderLabSubnet.FormatAddress(subnet.FirstAssignableHost!.Value));
        Assert.Equal("10.0.5.253", BuilderLabSubnet.FormatAddress(subnet.LastAssignableHost!.Value));
        Assert.Equal("10.0.5.255", BuilderLabSubnet.FormatAddress(subnet.BroadcastAddress));
    }

    [Fact]
    public void AssignableHosts_ExcludeNetworkBroadcastRouterAndHost()
    {
        BuilderLabSubnet.TryParseCidr("10.0.5.0/24", out var subnet);
        var assignable = subnet.AssignableHosts.ToList();

        Assert.Equal(252, assignable.Count); // 256 - network - broadcast - router - host
        Assert.DoesNotContain(subnet.NetworkAddress, assignable);
        Assert.DoesNotContain(subnet.BroadcastAddress, assignable);
        Assert.DoesNotContain(subnet.RouterAddress!.Value, assignable);
        Assert.DoesNotContain(subnet.HostReservedAddress!.Value, assignable);
    }

    [Fact]
    public void NextFreeHost_ReturnsLowestUnusedAssignable()
    {
        BuilderLabSubnet.TryParseCidr("10.0.5.0/24", out var subnet);
        BuilderLabSubnet.TryParseAddress("10.0.5.2", out var dotTwo);
        BuilderLabSubnet.TryParseAddress("10.0.5.3", out var dotThree);

        var next = subnet.NextFreeHost([dotTwo, dotThree]);

        Assert.Equal("10.0.5.4", BuilderLabSubnet.FormatAddress(next!.Value));
    }

    [Fact]
    public void NextFreeHost_ReturnsNullWhenFull()
    {
        BuilderLabSubnet.TryParseCidr("10.0.5.0/30", out var subnet); // block of 4: .0 net, .3 bcast, .1 router, .2 host -> none assignable
        Assert.False(subnet.HasAssignableHosts);
        Assert.Null(subnet.NextFreeHost([]));
    }

    [Fact]
    public void Contains_AndReservationChecks()
    {
        BuilderLabSubnet.TryParseCidr("10.0.5.0/24", out var subnet);
        BuilderLabSubnet.TryParseAddress("10.0.5.10", out var inside);
        BuilderLabSubnet.TryParseAddress("10.0.6.10", out var outside);

        Assert.True(subnet.Contains(inside));
        Assert.False(subnet.Contains(outside));
        Assert.True(subnet.IsRouterReserved(subnet.RouterAddress!.Value));
        Assert.True(subnet.IsHostReserved(subnet.HostReservedAddress!.Value));
        Assert.True(subnet.IsNetworkOrBroadcast(subnet.NetworkAddress));
        Assert.True(subnet.IsNetworkOrBroadcast(subnet.BroadcastAddress));
    }

    [Fact]
    public void EditableOctets_OneForA24TwoForA16()
    {
        BuilderLabSubnet.TryParseCidr("10.0.5.0/24", out var slash24);
        BuilderLabSubnet.TryParseCidr("10.0.0.0/16", out var slash16);

        Assert.Equal(1, slash24.EditableOctetCount);
        Assert.Equal("10.0.5.", slash24.FixedOctetPrefix);
        Assert.Equal(2, slash16.EditableOctetCount);
        Assert.Equal("10.0.", slash16.FixedOctetPrefix);
    }

    [Fact]
    public void ComposeAndDecompose_LastOctetRoundTripsForA24()
    {
        BuilderLabSubnet.TryParseCidr("10.0.5.0/24", out var subnet);

        var composed = subnet.ComposeHostAddress([37]);
        Assert.Equal("10.0.5.37", BuilderLabSubnet.FormatAddress(composed));

        var octets = subnet.EditableOctetsOf(composed);
        Assert.Equal(new[] { 37 }, octets);
    }

    [Fact]
    public void ComposeAndDecompose_TwoOctetsRoundTripForA16()
    {
        BuilderLabSubnet.TryParseCidr("10.0.0.0/16", out var subnet);

        var composed = subnet.ComposeHostAddress([9, 42]);
        Assert.Equal("10.0.9.42", BuilderLabSubnet.FormatAddress(composed));
        Assert.Equal(new[] { 9, 42 }, subnet.EditableOctetsOf(composed));
    }

    [Theory]
    [InlineData("010.0.0.0")]   // octal-looking leading zero, silently reinterpreted by IPAddress.TryParse
    [InlineData("10.5")]        // short form -> 10.0.0.5
    [InlineData("1.2.3")]       // short form -> 1.2.0.3
    [InlineData("10")]          // single value -> 0.0.0.10
    [InlineData("0x0a.0.0.1")]  // hex octet
    [InlineData("1.2.3.4.5")]   // too many octets
    [InlineData("256.0.0.0")]   // octet out of range
    [InlineData("1.2.3.256")]   // octet out of range
    [InlineData("00.0.0.0")]    // leading zero (not a lone "0")
    [InlineData("1.2.3.")]      // empty trailing octet
    [InlineData(" 1.2.3.4 ")]   // trims outer whitespace but inner spaces would be rejected elsewhere
    public void TryParseAddress_RejectsNonCanonicalOrOutOfRange(string value)
    {
        // " 1.2.3.4 " is actually valid after the outer trim; assert only the genuinely invalid cases here.
        if (value.Trim() == "1.2.3.4")
        {
            Assert.True(BuilderLabSubnet.TryParseAddress(value, out _));
            return;
        }

        Assert.False(BuilderLabSubnet.TryParseAddress(value, out _));
    }

    [Theory]
    [InlineData("10.0.5.0", "10.0.5.0")]
    [InlineData("0.0.0.0", "0.0.0.0")]
    [InlineData("255.255.255.255", "255.255.255.255")]
    [InlineData("192.168.1.10", "192.168.1.10")]
    public void TryParseAddress_AcceptsCanonicalDottedQuads(string value, string expected)
    {
        Assert.True(BuilderLabSubnet.TryParseAddress(value, out var address));
        Assert.Equal(expected, BuilderLabSubnet.FormatAddress(address));
    }
}
