using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// An <see cref="IV2NodeExecutor"/> whose execute and cleanup behavior is supplied as delegates.
/// The runtime registry builder constructs one instance per node kind, each delegate wrapping the existing coordinator,
/// script-builder, or stage call for that kind. This keeps a single, DRY executor type while still giving one executor
/// per kind, as required by the registry contract.
/// </summary>
public sealed class V2DelegatingNodeExecutor : IV2NodeExecutor
{
    private readonly Func<V2NodeExecutionContext, CancellationToken, Task> _execute;
    private readonly Func<V2NodeExecutionContext, CancellationToken, Task>? _cleanup;

    /// <summary>Creates a delegating executor for <paramref name="kind"/>.</summary>
    /// <param name="kind">The node kind this executor handles.</param>
    /// <param name="execute">The work delegate. Required.</param>
    /// <param name="cleanup">The undo delegate. Optional; when null, cleanup is a no-op.</param>
    public V2DelegatingNodeExecutor(
        V2PlanNodeKind kind,
        Func<V2NodeExecutionContext, CancellationToken, Task> execute,
        Func<V2NodeExecutionContext, CancellationToken, Task>? cleanup = null)
    {
        Kind = kind;
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _cleanup = cleanup;
    }

    /// <inheritdoc />
    public V2PlanNodeKind Kind { get; }

    /// <inheritdoc />
    public Task ExecuteAsync(V2NodeExecutionContext context, CancellationToken cancellationToken)
        => _execute(context, cancellationToken);

    /// <inheritdoc />
    public Task CleanupAsync(V2NodeExecutionContext context, CancellationToken cancellationToken)
        => _cleanup?.Invoke(context, cancellationToken) ?? Task.CompletedTask;
}
