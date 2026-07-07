using LabAssistant.Business.Runtime;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

/// <summary>
/// Guards the guest-NIC-by-MAC matching contract in <see cref="RouterGuestScriptBuilder"/>.
/// The guest's <c>Get-NetAdapter</c> reports MACs as separator-delimited hex (00-15-5D-..) while
/// Hyper-V injects them bare (00155D..); the emitted PowerShell must canonicalize both sides so the
/// comparison cannot silently mismatch. These tests fail loudly if a raw <c>-eq</c> compare returns.
/// </summary>
public sealed class RouterGuestScriptBuilderTests
{
    [Fact]
    public void BuildPrepareRouterNetworkScript_NormalizesInjectedMacAndCanonicalizesGuestMac()
    {
        var nics = new[]
        {
            new RouterNicPlan
            {
                SwitchName = "vSwitch-External",
                MacAddress = "00-15-5d-ab-cd-ef",
                IsExternal = true
            }
        };

        var script = RouterGuestScriptBuilder.BuildPrepareRouterNetworkScript(nics);

        // Injected Hyper-V MAC is reduced to canonical bare/upper form.
        Assert.Contains("MacAddress = '00155DABCDEF'", script, StringComparison.Ordinal);
        // Guest-side value is canonicalized before comparison.
        Assert.Contains("(ConvertTo-CanonicalMac $_.MacAddress) -eq $target.MacAddress", script, StringComparison.Ordinal);
        Assert.Contains("function ConvertTo-CanonicalMac", script, StringComparison.Ordinal);
        // Regression guard: no raw, un-canonicalized comparison remains.
        Assert.DoesNotContain("$_.MacAddress -eq", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildEnableRouterRoutingScript_NormalizesExternalMacAndCanonicalizesGuestMac()
    {
        var script = RouterGuestScriptBuilder.BuildEnableRouterRoutingScript("00:15:5D:AB:CD:EF");

        Assert.Contains("$externalMac = '00155DABCDEF'", script, StringComparison.Ordinal);
        Assert.Contains("(ConvertTo-CanonicalMac $_.MacAddress) -eq $externalMac", script, StringComparison.Ordinal);
        Assert.Contains("function ConvertTo-CanonicalMac", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$_.MacAddress -eq", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildConfigureRouterNatScript_NormalizesAllMacsAndCanonicalizesGuestComparisons()
    {
        var script = RouterGuestScriptBuilder.BuildConfigureRouterNatScript(
            "00-15-5d-00-00-02",
            new[] { "00:15:5d:00:00:01", "00.15.5d.00.00.03" });

        Assert.Contains("$externalMac = '00155D000002'", script, StringComparison.Ordinal);
        Assert.Contains("'00155D000001'", script, StringComparison.Ordinal);
        Assert.Contains("'00155D000003'", script, StringComparison.Ordinal);
        Assert.Contains("(ConvertTo-CanonicalMac $_.MacAddress) -eq $externalMac", script, StringComparison.Ordinal);
        Assert.Contains("(ConvertTo-CanonicalMac $_.MacAddress) -eq $mac", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$_.MacAddress -eq", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildProbeRouterExternalReadinessScript_NormalizesExternalMacAndCanonicalizesGuestMac()
    {
        var script = RouterGuestScriptBuilder.BuildProbeRouterExternalReadinessScript("00-15-5D-AB-CD-EF");

        Assert.Contains("$externalMac = '00155DABCDEF'", script, StringComparison.Ordinal);
        Assert.Contains("(ConvertTo-CanonicalMac $_.MacAddress) -eq $externalMac", script, StringComparison.Ordinal);
        Assert.Contains("function ConvertTo-CanonicalMac", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$_.MacAddress -eq", script, StringComparison.Ordinal);
    }
}
