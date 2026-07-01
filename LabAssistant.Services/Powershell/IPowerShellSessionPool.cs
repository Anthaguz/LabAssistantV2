namespace LabAssistant.Services.PowerShell;

/// <summary>
/// Manages a pool of pre-warmed PowerShell sessions for fast query execution.
/// Sessions are checked out, used, and returned. The pool maintains health and replaces dead sessions.
/// </summary>
public interface IPowerShellSessionPool : IAsyncDisposable
{
    /// <summary>
    /// Checks out a warm session from the pool.
    /// Returns immediately if one is available.
    /// </summary>
    /// <param name="cancellationToken">Cancels the checkout request.</param>
    /// <returns>A handle that returns the session to the pool when disposed.</returns>
    Task<PooledSessionHandle> CheckoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of sessions currently available in the pool.
    /// </summary>
    int AvailableCount { get; }

    /// <summary>
    /// Gets the total number of sessions managed by the pool.
    /// </summary>
    int TotalCount { get; }

    /// <summary>
    /// Pre-warms the pool with the specified number of sessions.
    /// </summary>
    /// <param name="count">The number of sessions to create and enqueue.</param>
    /// <param name="cancellationToken">Cancels the warmup request.</param>
    Task WarmupAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the health of all pooled sessions and replaces any that are dead.
    /// </summary>
    /// <param name="cancellationToken">Cancels the health check.</param>
    Task HealthCheckAsync(CancellationToken cancellationToken = default);
}
