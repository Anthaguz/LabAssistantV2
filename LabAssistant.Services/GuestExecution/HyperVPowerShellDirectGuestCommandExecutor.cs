using LabAssistant.Models.Deployment;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Services.GuestExecution;

public sealed class HyperVPowerShellDirectGuestCommandExecutor : IGuestCommandExecutor
{
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
        var command = BuildPowerShellDirectCommand(vmName, credential, script);
        var result = await session.ExecuteAsync(command);
        cancellationToken.ThrowIfCancellationRequested();

        return new GuestCommandResult
        {
            Success = string.IsNullOrWhiteSpace(result.Error),
            Output = result.Output,
            Error = result.Error
        };
    }

    private static string BuildPowerShellDirectCommand(string vmName, V2RuntimeCredential credential, string script)
    {
        return string.Join(
            Environment.NewLine,
            $"$guestPassword = ConvertTo-SecureString '{EscapeSingleQuotedLiteral(credential.Password)}' -AsPlainText -Force",
            $"$guestCredential = New-Object System.Management.Automation.PSCredential ('{EscapeSingleQuotedLiteral(credential.Username)}', $guestPassword)",
            $"Invoke-Command -VMName '{EscapeSingleQuotedLiteral(vmName)}' -Credential $guestCredential -ErrorAction Stop -ScriptBlock {{",
            script,
            "}");
    }

    private static string EscapeSingleQuotedLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
