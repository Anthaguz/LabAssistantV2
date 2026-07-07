using LabAssistant.Models.Deployment;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Services.GuestExecution;

public sealed class HyperVPowerShellDirectGuestCommandExecutor : IGuestCommandExecutor
{
    private const string GuestPasswordVariableName = "__laGuestPassword";

    private readonly Func<IPersistentPowerShellSession> _sessionFactory;

    public HyperVPowerShellDirectGuestCommandExecutor(Func<IPersistentPowerShellSession> sessionFactory)
    {
        _sessionFactory = sessionFactory;
    }

    public async Task<GuestCommandResult> ExecutePowerShellDirectAsync(
        string vmName,
        V2RuntimeCredential credential,
        string script,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(vmName);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(credential.Username);
        ArgumentException.ThrowIfNullOrWhiteSpace(script);

        using var session = _sessionFactory();
        var command = BuildPowerShellDirectCommand(vmName, credential.Username, script);

        // The password is provided out-of-band via a secure variable so it never appears in the command
        // text that could be captured by logs or diagnostics. The session removes it after execution.
        var secureVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [GuestPasswordVariableName] = credential.Password ?? string.Empty
        };

        var result = await session.ExecuteAsync(command, secureVariables, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        return new GuestCommandResult
        {
            Success = string.IsNullOrWhiteSpace(result.Error),
            Output = result.Output,
            Error = result.Error
        };
    }

    /// <summary>
    /// Builds the host-side command that establishes the guest credential and dispatches the caller's
    /// script into the guest. No secret is interpolated: the plaintext password is injected separately
    /// as the <c>$__laGuestPassword</c> runspace variable, so the returned string is safe to log.
    /// </summary>
    internal static string BuildPowerShellDirectCommand(string vmName, string username, string script)
    {
        return string.Join(
            Environment.NewLine,
            $"$guestSecurePassword = ConvertTo-SecureString ${GuestPasswordVariableName} -AsPlainText -Force",
            $"$guestCredential = New-Object System.Management.Automation.PSCredential ({PowerShellCommandBuilder.Quote(username)}, $guestSecurePassword)",
            $"try {{ Invoke-Command -VMName {PowerShellCommandBuilder.Quote(vmName)} -Credential $guestCredential -ErrorAction Stop -ScriptBlock {{",
            script,
            "} }",
            "finally { Remove-Variable -Name 'guestSecurePassword','guestCredential' -ErrorAction SilentlyContinue }");
    }
}
