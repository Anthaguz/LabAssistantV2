using LabAssistant.Business.Runtime;
using LabAssistant.Models.Deployment;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

/// <summary>
/// Guards the network-profile contract in <see cref="BaseRemoteAccessGuestScriptBuilder"/>.
/// On a domain member the NIC's connection profile is <c>DomainAuthenticated</c>, which Windows refuses to
/// change ("the NetworkCategory cannot be changed from 'DomainAuthenticated'"). The emitted script must only
/// demote <c>Public</c> profiles to <c>Private</c> and skip <c>DomainAuthenticated</c>/<c>Private</c> ones,
/// otherwise ConfigureBaseRemoteAccess hard-fails on every domain-joined member.
/// </summary>
public sealed class BaseRemoteAccessGuestScriptBuilderTests
{
    [Fact]
    public void BuildConfigureBaseRemoteAccessScript_SkipsDomainAuthenticatedProfilesBeforeSettingPrivate()
    {
        var options = new V2BaseRemoteAccessOptions { SetPrivateNetworkProfile = true };

        var script = BaseRemoteAccessGuestScriptBuilder.BuildConfigureBaseRemoteAccessScript(options);

        // The guard must appear before the Set-NetConnectionProfile call so DomainAuthenticated NICs are skipped.
        var guardIndex = script.IndexOf(
            "if ($profile.NetworkCategory -eq 'DomainAuthenticated' -or $profile.NetworkCategory -eq 'Private') { continue }",
            StringComparison.Ordinal);
        var setIndex = script.IndexOf("Set-NetConnectionProfile", StringComparison.Ordinal);

        Assert.True(guardIndex >= 0, "Expected a DomainAuthenticated/Private skip guard in the emitted script.");
        Assert.True(setIndex >= 0, "Expected a Set-NetConnectionProfile call in the emitted script.");
        Assert.True(guardIndex < setIndex, "The skip guard must precede Set-NetConnectionProfile so it can short-circuit.");
    }

    [Fact]
    public void BuildConfigureBaseRemoteAccessScript_OmitsProfileBlockWhenNotRequested()
    {
        var options = new V2BaseRemoteAccessOptions { SetPrivateNetworkProfile = false };

        var script = BaseRemoteAccessGuestScriptBuilder.BuildConfigureBaseRemoteAccessScript(options);

        Assert.DoesNotContain("Set-NetConnectionProfile", script, StringComparison.Ordinal);
    }
}
