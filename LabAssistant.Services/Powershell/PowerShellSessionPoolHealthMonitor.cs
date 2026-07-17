using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using System.Runtime.CompilerServices;

namespace LabAssistant.Services.PowerShell;

/// <summary>
/// Periodically runs health checks for a PowerShell session pool.
/// </summary>
public sealed class PowerShellSessionPoolHealthMonitor : IAsyncDisposable
{
    private readonly IPowerShellSessionPool _pool;
    private readonly TimeSpan _interval;
    private readonly IStructuredLogger _structuredLogger;
    private readonly string _operationId = Guid.NewGuid().ToString("N");
    private readonly object _lifecycleGate = new();

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _runTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerShellSessionPoolHealthMonitor"/> class.
    /// </summary>
    /// <param name="pool">The pool to monitor.</param>
    /// <param name="options">The pool options that define the monitor interval.</param>
    /// <param name="structuredLogger">The structured logger to use for monitor events.</param>
    public PowerShellSessionPoolHealthMonitor(
        IPowerShellSessionPool pool,
        SessionPoolOptions options,
        IStructuredLogger? structuredLogger = null)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(options);

        if (options.HealthCheckInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Health check interval must be greater than zero.");
        }

        _pool = pool;
        _interval = options.HealthCheckInterval;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
    }

    /// <summary>
    /// Gets a value indicating whether the monitor is currently running.
    /// </summary>
    public bool IsRunning
    {
        get
        {
            lock (_lifecycleGate)
            {
                return _runTask is { IsCompleted: false };
            }
        }
    }

    /// <summary>
    /// Starts the background health-check loop.
    /// </summary>
    public void Start()
    {
        lock (_lifecycleGate)
        {
            if (_runTask is { IsCompleted: false })
            {
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _runTask = RunAsync(_cancellationTokenSource.Token);
        }

        Log(
            LaStatus.InfraPowershell_HealthMonitorStarted,
            "started",
            new Dictionary<string, object?>
            {
                ["intervalSeconds"] = _interval.TotalSeconds
            });
    }

    /// <summary>
    /// Stops the background health-check loop.
    /// </summary>
    /// <returns>A task that completes when the loop stops.</returns>
    public async Task StopAsync()
    {
        Task? runTask;
        CancellationTokenSource? cancellationTokenSource;

        lock (_lifecycleGate)
        {
            runTask = _runTask;
            cancellationTokenSource = _cancellationTokenSource;
            _runTask = null;
            _cancellationTokenSource = null;
        }

        if (cancellationTokenSource is null)
        {
            return;
        }

        cancellationTokenSource.Cancel();

        try
        {
            if (runTask is not null)
            {
                await runTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }

        Log(LaStatus.InfraPowershell_HealthMonitorStopped, "stopped");
    }

    /// <summary>
    /// Stops the monitor and releases its resources.
    /// </summary>
    /// <returns>A value task that completes when disposal is finished.</returns>
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await _pool.HealthCheckAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log(
                        LaStatus.InfraPowershell_MonitorHealthCheckFailed,
                        "failed",
                        new Dictionary<string, object?>
                        {
                            ["exceptionType"] = ex.GetType().FullName,
                            ["message"] = ex.Message
                        });
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void Log(
        uint code,
        string result,
        IReadOnlyDictionary<string, object?>? context = null,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        _structuredLogger.Log(code, _operationId, result, context, callerFilePath, callerLineNumber);
    }
}
