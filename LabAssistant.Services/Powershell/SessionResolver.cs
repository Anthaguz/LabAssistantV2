using System.Collections.Concurrent;
using LabAssistant.Models;
using LabAssistant.Models.PowerShell;
using LabAssistant.Services.Logging;

namespace LabAssistant.Services.PowerShell;

public class SessionResolver : ISessionResolver, IDisposable
{
    private readonly ConcurrentDictionary<Guid, IPersistentPowerShellSession> _sessions = new();

    public IPersistentPowerShellSession Resolve(PowerShellHandle handle)
    {
        DebugLogger.Log($"Resolving PowerShell session for handle: {handle.SessionId}");
        if (_sessions.TryGetValue(handle.SessionId, out var session))
        {
            return session;
        }

        throw new KeyNotFoundException($"No PowerShell session registered for handle: {handle.SessionId}");
    }

    public void RegisterSession(PowerShellHandle handle, IPersistentPowerShellSession session)
    {
        DebugLogger.Log($"Registering PowerShell session for handle: {handle.SessionId}");
        _sessions[handle.SessionId] = session;
    }

    public void RemoveSession(PowerShellHandle handle)
    {
        DebugLogger.Log($"Removing PowerShell session for handle: {handle.SessionId}");
        _sessions.TryRemove(handle.SessionId, out _);
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
            session.Dispose();
    }
}
