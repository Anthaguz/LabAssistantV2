namespace LabAssistant.Services.HyperV;

/// <summary>
/// Executes one-shot Hyper-V administrative commands in isolated PowerShell sessions.
/// </summary>
public interface IHyperVAdministrativeCommandExecutor
{
    Task<HyperVPowerShellExecutionResult> ExecuteAsync(
        string commandName,
        string script,
        CancellationToken cancellationToken = default);
}
