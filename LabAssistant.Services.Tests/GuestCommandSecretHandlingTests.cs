using LabAssistant.Models.Deployment;
using LabAssistant.Services.GuestExecution;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

/// <summary>
/// Verifies that the guest command executor never embeds the plaintext guest password in the emitted
/// (and therefore loggable) command text, and instead supplies it out-of-band as a secure variable.
/// </summary>
public class GuestCommandSecretHandlingTests
{
    private const string Password = "S3cr3t-P@ssw0rd!";

    [Fact]
    public void BuildPowerShellDirectCommand_DoesNotContainPlaintextPassword()
    {
        var command = HyperVPowerShellDirectGuestCommandExecutor.BuildPowerShellDirectCommand(
            "Router01",
            "Administrator",
            "Get-Date");

        Assert.DoesNotContain(Password, command, StringComparison.Ordinal);
        // The password is referenced only by variable name, never inlined as a plaintext literal.
        Assert.Contains("$__laGuestPassword", command, StringComparison.Ordinal);
        Assert.DoesNotContain("ConvertTo-SecureString '", command, StringComparison.Ordinal);
        // The reused-connection shape: dispatch goes through a held-open guest session, not a fresh per-step hop.
        Assert.Contains("New-PSSession -VMName 'Router01'", command, StringComparison.Ordinal);
        Assert.Contains("Invoke-Command -Session $__laGuestSession", command, StringComparison.Ordinal);
        // Self-heals when the connection is missing, severed by a reboot, or opened for a different identity.
        Assert.Contains("$__laGuestSession.State -ne 'Opened'", command, StringComparison.Ordinal);
        Assert.Contains("$__laGuestSessionUser -ne 'Administrator'", command, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecutePowerShellDirectAsync_PassesPasswordOnlyThroughSecureVariables()
    {
        var session = new CapturingSession();
        var executor = new HyperVPowerShellDirectGuestCommandExecutor(() => session);
        var credential = new V2RuntimeCredential { Username = "Administrator", Password = Password };

        await executor.ExecutePowerShellDirectAsync("Router01", credential, "Get-Date");

        // The plaintext must never appear in the command string that logging/diagnostics can capture.
        Assert.NotNull(session.LastCommand);
        Assert.DoesNotContain(Password, session.LastCommand!, StringComparison.Ordinal);

        // It is only carried in the out-of-band secure-variable channel.
        Assert.NotNull(session.LastSecureVariables);
        Assert.Equal(Password, session.LastSecureVariables!["__laGuestPassword"]);
    }

    private sealed class CapturingSession : IPersistentPowerShellSession
    {
        public string? LastCommand { get; private set; }

        public IReadOnlyDictionary<string, string>? LastSecureVariables { get; private set; }

        public Task<(string Output, string Error)> ExecuteAsync(string command)
            => ExecuteAsync(command, null, CancellationToken.None);

        public Task<(string Output, string Error)> ExecuteAsync(string command, CancellationToken cancellationToken)
            => ExecuteAsync(command, null, cancellationToken);

        public Task<(string Output, string Error)> ExecuteAsync(
            string command,
            IReadOnlyDictionary<string, string>? secureVariables,
            CancellationToken cancellationToken)
        {
            LastCommand = command;
            LastSecureVariables = secureVariables;
            return Task.FromResult((string.Empty, string.Empty));
        }

        public void Dispose()
        {
        }
    }
}
