using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LabAssistant.WinUI.Infrastructure;

/// <summary>
/// Base class for all ViewModels. Provides lifecycle management, cancellation,
/// structured error handling, and loading state coordination.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject, IAsyncDisposable
{
    private CancellationTokenSource? _lifecycleCts = new();
    private int _disposed;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Gets a value indicating whether this ViewModel has been initialized.
    /// </summary>
    public bool IsInitialized { get; protected set; }

    /// <summary>
    /// Gets a cancellation token that is cancelled when this ViewModel is disposed or cleaned up.
    /// Use this to link all async operations to the ViewModel lifecycle.
    /// </summary>
    protected CancellationToken LifecycleToken => _lifecycleCts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Called when the view is navigated to. Override to load data.
    /// The provided cancellation token is linked to the ViewModel lifecycle —
    /// it is cancelled if the ViewModel is disposed before initialization completes.
    /// </summary>
    public virtual Task InitializeAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        IsInitialized = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the view is navigated away from. Cancels all in-flight operations
    /// and releases resources. Override to add custom cleanup — always call base.
    /// </summary>
    public virtual Task CleanupAsync()
    {
        CancelLifecycleOperations();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes this ViewModel, cancelling all in-flight operations.
    /// Safe to call multiple times.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return ValueTask.CompletedTask;
        }

        CancelLifecycleOperations();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Executes an asynchronous operation with loading state management and proper error handling.
    /// Cancellation is automatically linked to the ViewModel lifecycle.
    /// </summary>
    /// <param name="operation">The operation to execute. Receives a CancellationToken linked to the ViewModel lifecycle.</param>
    /// <param name="cancellationToken">An additional external cancellation token to link.</param>
    protected async Task ExecuteWithLoadingAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        if (_disposed == 1) return;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(LifecycleToken, cancellationToken);
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await operation(linkedCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            // Cancellation is not an error — silently stop.
            Debug.WriteLine($"[{GetType().Name}] Operation cancelled.");
        }
        catch (Exception ex)
        {
            OnOperationFailed(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Executes an asynchronous operation that returns a value with loading state management.
    /// Cancellation is automatically linked to the ViewModel lifecycle.
    /// </summary>
    protected async Task<T?> ExecuteWithLoadingAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (_disposed == 1) return default;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(LifecycleToken, cancellationToken);
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            return await operation(linkedCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            Debug.WriteLine($"[{GetType().Name}] Operation cancelled.");
            return default;
        }
        catch (Exception ex)
        {
            OnOperationFailed(ex);
            return default;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Executes an asynchronous operation without showing loading state (for background refreshes).
    /// Still provides cancellation and error handling.
    /// </summary>
    protected async Task ExecuteSilentAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        if (_disposed == 1) return;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(LifecycleToken, cancellationToken);
        try
        {
            await operation(linkedCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            // Silently ignore cancellation.
        }
        catch (Exception ex)
        {
            OnOperationFailed(ex);
        }
    }

    /// <summary>
    /// Called when an operation fails with a non-cancellation exception.
    /// Override to add custom logging or telemetry. Always call base to set ErrorMessage.
    /// </summary>
    protected virtual void OnOperationFailed(Exception ex)
    {
        var message = ex.InnerException is not null
            ? $"{ex.Message} → {ex.InnerException.Message}"
            : ex.Message;

        ErrorMessage = message;

        Debug.WriteLine($"[{GetType().Name}] Operation failed: {ex}");
    }

    /// <summary>
    /// Invokes a bridge callback that forwards a command to the view or composition layer.
    /// When the callback is not wired, the missing handler is surfaced as a diagnostic instead
    /// of being silently dropped, so wiring gaps are detectable during development rather than
    /// presenting as a dead control at runtime. Returns whether a handler ran so callers and
    /// tests can distinguish a real invocation from an unwired no-op.
    /// </summary>
    /// <param name="callback">The bridge callback to invoke; may be <c>null</c> when unwired.</param>
    /// <param name="commandName">The invoking command name, supplied automatically by the compiler.</param>
    /// <returns><c>true</c> if a handler was invoked; otherwise <c>false</c>.</returns>
    protected bool InvokeBridgeCallback(Action? callback, [CallerMemberName] string? commandName = null)
    {
        if (callback is null)
        {
            Debug.WriteLine($"[{GetType().Name}] Bridge command '{commandName}' was invoked but no handler is wired; the action had no effect.");
            return false;
        }

        callback();
        return true;
    }

    /// <summary>
    /// Cancels the lifecycle token and creates a new one for any subsequent operations.
    /// Called by CleanupAsync and DisposeAsync.
    /// </summary>
    private void CancelLifecycleOperations()
    {
        var cts = Interlocked.Exchange(ref _lifecycleCts, null);
        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException) { }
            finally
            {
                cts.Dispose();
            }
        }
    }
}
