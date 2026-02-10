using LabAssistant.Models;
using LabAssistant.Models.PowerShell;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Services.PowerShell;

public interface ISessionResolver
{
    IPersistentPowerShellSession Resolve(PowerShellHandle handle);
    void RegisterSession(PowerShellHandle handle, IPersistentPowerShellSession session);
    void RemoveSession(PowerShellHandle handle);
}
