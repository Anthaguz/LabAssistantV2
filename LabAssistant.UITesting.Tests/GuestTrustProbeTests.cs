using LabAssistant.UITesting.Infrastructure;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Covers the pure parse seam of <see cref="GuestTrustProbe"/> - turning the TRUST_* lines a successful
/// PowerShell Direct probe prints (from Get-ADTrust) into a <see cref="GuestTrustInfo"/> - plus the
/// forest-trust / bidirectional classification the scenario asserts on. No VM or PowerShell is launched.
/// </summary>
public sealed class GuestTrustProbeTests
{
    private static string Output(string target, string type, string direction, string forestTransitive) =>
        string.Join(
            "\n",
            "TRUST_FOUND=True",
            "TRUST_TARGET=" + target,
            "TRUST_TYPE=" + type,
            "TRUST_DIRECTION=" + direction,
            "TRUST_FOREST_TRANSITIVE=" + forestTransitive);

    [Fact]
    public void TryParse_ForestTrust_ParsesAllFields()
    {
        var trust = GuestTrustProbe.TryParse(Output("beta.lab", "Forest", "Bidirectional", "True"));

        Assert.NotNull(trust);
        Assert.Equal("beta.lab", trust!.Target);
        Assert.Equal("Forest", trust.TrustType);
        Assert.Equal("Bidirectional", trust.Direction);
        Assert.True(trust.ForestTransitive);
        Assert.True(trust.IsForestTrust);
        Assert.True(trust.IsBidirectional);
    }

    [Fact]
    public void TryParse_UplevelTransitiveTrust_IsTreatedAsForestTrust()
    {
        // On Windows Server 2022 Get-ADTrust reports a forest trust as TrustType "Uplevel" with
        // ForestTransitive set, not the literal "Forest" - both must classify as a forest trust.
        var trust = GuestTrustProbe.TryParse(Output("beta.lab", "Uplevel", "BiDirectional", "True"));

        Assert.NotNull(trust);
        Assert.True(trust!.IsForestTrust);
        // Get-ADTrust's ADTrustDirection stringifies as "BiDirectional"; the check is case-insensitive.
        Assert.True(trust.IsBidirectional);
    }

    [Fact]
    public void TryParse_UplevelExternalTrust_IsNotForestTrust_EvenWhenBidirectional()
    {
        // The trap this scenario must not fall into: on Windows Server 2022 a plain EXTERNAL trust also
        // reports TrustType "Uplevel" - only ForestTransitive distinguishes it from a forest trust. A
        // bidirectional external trust must NOT be accepted as the forest trust the deploy was meant to
        // create.
        var trust = GuestTrustProbe.TryParse(Output("beta.lab", "Uplevel", "Bidirectional", "False"));

        Assert.NotNull(trust);
        Assert.False(trust!.IsForestTrust);
        Assert.True(trust.IsBidirectional);
    }

    [Fact]
    public void TryParse_UplevelOneWayExternalTrust_IsNotForestOrBidirectional()
    {
        var trust = GuestTrustProbe.TryParse(Output("beta.lab", "Uplevel", "Inbound", "False"));

        Assert.NotNull(trust);
        Assert.False(trust!.IsForestTrust);
        Assert.False(trust.IsBidirectional);
    }

    [Fact]
    public void TryParse_IsCaseInsensitive_ForTypeAndDirection()
    {
        var trust = GuestTrustProbe.TryParse(Output("beta.lab", "forest", "bidirectional", "true"));

        Assert.NotNull(trust);
        Assert.True(trust!.IsForestTrust);
        Assert.True(trust.IsBidirectional);
        Assert.True(trust.ForestTransitive);
    }

    [Fact]
    public void TryParse_MissingTarget_ReturnsNull()
    {
        var output = string.Join("\n", "TRUST_FOUND=True", "TRUST_TYPE=Forest", "TRUST_DIRECTION=Bidirectional");
        Assert.Null(GuestTrustProbe.TryParse(output));
    }

    [Fact]
    public void TryParse_MissingDirection_ReturnsNull()
    {
        var output = string.Join("\n", "TRUST_FOUND=True", "TRUST_TARGET=beta.lab", "TRUST_TYPE=Forest");
        Assert.Null(GuestTrustProbe.TryParse(output));
    }

    [Fact]
    public void TryParse_NotFoundAnswer_ReturnsNull()
    {
        Assert.Null(GuestTrustProbe.TryParse("TRUST_FOUND=False"));
    }

    [Fact]
    public void TryParse_EmptyOutput_ReturnsNull()
    {
        Assert.Null(GuestTrustProbe.TryParse(string.Empty));
    }

    [Fact]
    public void TryParse_AbsentForestTransitive_DefaultsToFalse()
    {
        var output = string.Join("\n", "TRUST_FOUND=True", "TRUST_TARGET=beta.lab", "TRUST_TYPE=Forest", "TRUST_DIRECTION=Bidirectional");
        var trust = GuestTrustProbe.TryParse(output);

        Assert.NotNull(trust);
        Assert.False(trust!.ForestTransitive);
        // A literal "Forest" type is still a forest trust even without the transitive flag line.
        Assert.True(trust.IsForestTrust);
    }
}
