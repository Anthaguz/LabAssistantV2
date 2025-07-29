namespace LabAssistant.Services.PowerShell;

public interface IPowerShellExecutor
{
    Task<string> ExecuteAsync(string script, CancellationToken cancellationToken = default);
    Task<(string Output, string Error)> ExecuteWithResultAsync(string script, CancellationToken cancellationToken = default);
}
