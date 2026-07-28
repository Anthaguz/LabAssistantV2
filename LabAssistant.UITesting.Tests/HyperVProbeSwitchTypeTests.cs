using LabAssistant.UITesting.Infrastructure;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Covers <see cref="HyperVProbe.GetSwitchType(string, System.Func{string, PowerShellResult})"/> and its
/// script builder - the not-found-returns-null contract that a live switch-auto-create scenario depends on.
///
/// Regression: the original probe used <c>Get-VMSwitch -Name '&lt;absent&gt;'</c>, which raises a
/// terminating error under <see cref="PowerShellRunner"/>'s <c>$ErrorActionPreference='Stop'</c> even with
/// -ErrorAction SilentlyContinue, so probing a deliberately-absent switch THREW instead of returning null -
/// aborting the switch-auto-create-live scenario before it could click Start Deploy. These tests pin the
/// fixed behavior (absent => null, present => type, genuine failure => throw) and lock the query shape that
/// makes an absent switch non-erroring (enumerate + filter, never -Name).
/// </summary>
public sealed class HyperVProbeSwitchTypeTests
{
    [Fact]
    public void GetSwitchType_ReturnsNull_WhenSwitchAbsent_WithoutThrowing()
    {
        // The fixed enumerate-and-filter query yields no output for an absent switch and exits 0.
        var runner = (string _) => new PowerShellResult(0, string.Empty, string.Empty);

        var type = HyperVProbe.GetSwitchType("LAT-run-ghostswlive", runner);

        Assert.Null(type);
    }

    [Fact]
    public void GetSwitchType_ReturnsType_WhenSwitchPresent()
    {
        var runner = (string _) => new PowerShellResult(0, "Internal\r\n", string.Empty);

        var type = HyperVProbe.GetSwitchType("LAT-run-ghostswlive", runner);

        Assert.Equal("Internal", type);
    }

    [Fact]
    public void GetSwitchType_Throws_OnGenuineFailure()
    {
        // A real host/module failure (non-zero exit) must still surface, not be masked as "absent".
        var runner = (string _) => new PowerShellResult(1, string.Empty, "Hyper-V module not available");

        var ex = Assert.Throws<InvalidOperationException>(
            () => HyperVProbe.GetSwitchType("LAT-run-ghostswlive", runner));
        Assert.Contains("Get-VMSwitch type probe failed", ex.Message);
    }

    [Fact]
    public void BuildSwitchTypeScript_EnumeratesAndFilters_WithoutNameParameter()
    {
        var script = HyperVProbe.BuildSwitchTypeScript("LAT-run-ghostswlive");

        // The whole point of the fix: never reference a possibly-absent switch by -Name (which throws
        // under ErrorActionPreference=Stop). Enumerate all switches and filter by exact name instead.
        Assert.DoesNotContain("-Name", script);
        Assert.Contains("Get-VMSwitch", script);
        Assert.Contains("$_.Name -eq 'LAT-run-ghostswlive'", script);
        Assert.Contains("-ExpandProperty SwitchType", script);
    }

    [Fact]
    public void BuildSwitchTypeScript_EscapesSingleQuotesInName()
    {
        var script = HyperVProbe.BuildSwitchTypeScript("weird'name");

        // Single quotes are doubled so a crafted name cannot break out of the PowerShell string literal.
        Assert.Contains("$_.Name -eq 'weird''name'", script);
    }
}
