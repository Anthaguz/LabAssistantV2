using LabAssistant.Services.GuestExecution;
using Xunit;

namespace LabAssistant.Services.Tests;

/// <summary>
/// Verifies that <see cref="GuestErrorClassifier"/> distinguishes a deterministic credential rejection (which the
/// runtime readiness loops should fail fast on) from a transient not-ready-yet error (which they should retry).
/// </summary>
public class GuestErrorClassifierTests
{
    [Theory]
    [InlineData("Hyper-V\\Invoke-Command : The credential is invalid.")]
    [InlineData("Logon failure: unknown user name or bad password.")]
    [InlineData("Access is denied.")]
    [InlineData("The user name or password is incorrect.")]
    [InlineData("Authentication failed for the supplied credential.")]
    public void Classify_CredentialRejectionMessages_ReturnsAuthenticationRejected(string error)
    {
        Assert.Equal(GuestCommandErrorCategory.AuthenticationRejected, GuestErrorClassifier.Classify(error));
    }

    [Theory]
    [InlineData("RDP listener not yet bound.")]
    [InlineData("The virtual machine is still starting.")]
    [InlineData("Guest PowerShell Direct command did not complete within 00:05:00.")]
    [InlineData("Cannot connect to the guest operating system.")]
    public void Classify_TransientMessages_ReturnsTransient(string error)
    {
        Assert.Equal(GuestCommandErrorCategory.Transient, GuestErrorClassifier.Classify(error));
    }

    [Theory]
    [InlineData("The running command stopped because the preference variable \"ErrorActionPreference\" is set to Stop: The Hyper-V socket target process has ended.")]
    [InlineData("[dc01] The background process reported an error with the following message: \"The Hyper-V socket target process has ended.\".")]
    [InlineData("FullyQualifiedErrorId : 2100,PSSessionStateBroken")]
    [InlineData("CategoryInfo : OpenError: (dc01:String) [], PSDirectException")]
    [InlineData("CategoryInfo : OpenError: (dc01:String) [], PSRemotingTransportException")]
    public void Classify_GuestRebootMessages_ReturnsGuestRebooting(string error)
    {
        Assert.Equal(GuestCommandErrorCategory.GuestRebooting, GuestErrorClassifier.Classify(error));
    }

    [Fact]
    public void Classify_CredentialRejectionTakesPrecedenceOverReboot()
    {
        // A message carrying both signatures is a real credential rejection, not a reboot: fail fast.
        Assert.Equal(
            GuestCommandErrorCategory.AuthenticationRejected,
            GuestErrorClassifier.Classify("The credential is invalid. PSSessionStateBroken"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_NoError_ReturnsNone(string? error)
    {
        Assert.Equal(GuestCommandErrorCategory.None, GuestErrorClassifier.Classify(error));
    }

    [Fact]
    public void Classify_IsCaseInsensitive()
    {
        Assert.Equal(
            GuestCommandErrorCategory.AuthenticationRejected,
            GuestErrorClassifier.Classify("THE CREDENTIAL IS INVALID"));
    }
}
