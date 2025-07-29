using LabAssistant.Models;
using LabAssistant.Services.PowerShell;

using LabAssistant.Models.PowerShell;
using LabAssistant.Services.Logging;

namespace LabAssistant.Services.PowerShell;

public class SessionResolver : ISessionResolver, IDisposable
{
    private readonly Dictionary<Guid, IPersistentPowerShellSession> _sessions = new();

    public IPersistentPowerShellSession Resolve(PowerShellHandle handle)
    {
        DebugLogger.Log($"Resolving PowerShell session for handle: {handle.SessionId}");
        return _sessions[handle.SessionId];
    }

    public void RegisterSession(PowerShellHandle handle, IPersistentPowerShellSession session)
    {
        DebugLogger.Log($"Registering PowerShell session for handle: {handle.SessionId}");
        _sessions[handle.SessionId] = session;
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
            session.Dispose();
    }
}
