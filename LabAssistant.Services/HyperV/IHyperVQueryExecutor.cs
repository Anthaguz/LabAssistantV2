namespace LabAssistant.Services.HyperV;

/// <summary>
/// Executes read-heavy Hyper-V queries through a reusable PowerShell session.
/// </summary>
public interface IHyperVQueryExecutor
{
    Task<HyperVPowerShellExecutionResult> ExecuteAsync(
        string queryName,
        string script,
        CancellationToken cancellationToken = default);
}
