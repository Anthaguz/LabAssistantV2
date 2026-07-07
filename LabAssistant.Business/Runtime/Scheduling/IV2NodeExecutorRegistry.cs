using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// Maps a <see cref="V2PlanNodeKind"/> to the executor responsible for it.
/// Resolution is fail-closed: an unregistered kind throws rather than being silently skipped.
/// </summary>
public interface IV2NodeExecutorRegistry
{
    /// <summary>Returns the executor for <paramref name="kind"/>, or throws when none is registered.</summary>
    IV2NodeExecutor Resolve(V2PlanNodeKind kind);

    /// <summary>Returns true when an executor is registered for <paramref name="kind"/>.</summary>
    bool IsRegistered(V2PlanNodeKind kind);
}
