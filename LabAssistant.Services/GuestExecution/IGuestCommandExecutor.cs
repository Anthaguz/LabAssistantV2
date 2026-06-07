using LabAssistant.Models.Deployment;

namespace LabAssistant.Services.GuestExecution;

public interface IGuestCommandExecutor
{
    Task<GuestCommandResult> ExecutePowerShellDirectAsync(
        string vmName,
        V2RuntimeCredential credential,
        string script,
        CancellationToken cancellationToken = default);
}

public sealed class GuestCommandResult
{
    public bool Success { get; init; }

    public string Output { get; init; } = string.Empty;

    public string Error { get; init; } = string.Empty;
}
