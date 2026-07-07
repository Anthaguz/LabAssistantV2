using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// Thrown when an executor is requested for a node kind that has no registered executor.
/// This is the fail-closed signal that surfaces dead or unimplemented kinds (for example <c>ApplyCapabilityRole</c>).
/// </summary>
public sealed class V2NodeExecutorNotRegisteredException : Exception
{
    /// <summary>Creates the exception for <paramref name="kind"/>.</summary>
    public V2NodeExecutorNotRegisteredException(V2PlanNodeKind kind)
        : base($"No V2 node executor is registered for kind '{kind}'.")
    {
        Kind = kind;
    }

    /// <summary>The unregistered node kind.</summary>
    public V2PlanNodeKind Kind { get; }
}
