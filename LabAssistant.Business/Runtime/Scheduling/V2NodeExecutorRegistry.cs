using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// Dictionary-backed <see cref="IV2NodeExecutorRegistry"/>. Resolution is fail-closed.
/// </summary>
public sealed class V2NodeExecutorRegistry : IV2NodeExecutorRegistry
{
    private readonly IReadOnlyDictionary<V2PlanNodeKind, IV2NodeExecutor> _executors;

    /// <summary>Creates a registry from a set of executors, keyed by their <see cref="IV2NodeExecutor.Kind"/>.</summary>
    /// <exception cref="ArgumentException">Thrown when two executors declare the same kind.</exception>
    public V2NodeExecutorRegistry(IEnumerable<IV2NodeExecutor> executors)
    {
        ArgumentNullException.ThrowIfNull(executors);

        var map = new Dictionary<V2PlanNodeKind, IV2NodeExecutor>();
        foreach (var executor in executors)
        {
            ArgumentNullException.ThrowIfNull(executor);
            if (!map.TryAdd(executor.Kind, executor))
            {
                throw new ArgumentException($"Duplicate executor registered for kind '{executor.Kind}'.", nameof(executors));
            }
        }

        _executors = map;
    }

    /// <inheritdoc />
    public bool IsRegistered(V2PlanNodeKind kind) => _executors.ContainsKey(kind);

    /// <inheritdoc />
    public IV2NodeExecutor Resolve(V2PlanNodeKind kind)
    {
        if (_executors.TryGetValue(kind, out var executor))
        {
            return executor;
        }

        throw new V2NodeExecutorNotRegisteredException(kind);
    }
}
