namespace LabAssistant.Services.PowerShell;

/// <summary>
/// Configuration for the PowerShell session pool.
/// </summary>
public sealed record SessionPoolOptions
{
    /// <summary>
    /// Number of sessions to pre-warm at startup.
    /// Default: 2.
    /// </summary>
    public int WarmupCount { get; init; } = 2;

    /// <summary>
    /// Maximum sessions the pool will manage.
    /// Default: 4.
    /// </summary>
    public int MaxPoolSize { get; init; } = 4;

    /// <summary>
    /// Timeout waiting for a session when the pool is exhausted.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan CheckoutTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Interval between pool health checks.
    /// Default: 60 seconds.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Idle time before a session is recycled.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan IdleRecycleTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
