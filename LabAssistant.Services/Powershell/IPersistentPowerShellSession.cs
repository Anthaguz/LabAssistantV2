namespace LabAssistant.Services.PowerShell;

public interface IPersistentPowerShellSession : IDisposable
{
    Task<(string Output, string Error)> ExecuteAsync(string command);
}