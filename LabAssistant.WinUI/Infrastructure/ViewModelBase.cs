using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LabAssistant.WinUI.Infrastructure;

/// <summary>
/// Base class for all ViewModels. Provides ObservableObject + common infrastructure.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Gets a value indicating whether this ViewModel has been initialized.
    /// </summary>
    public bool IsInitialized { get; protected set; }

    /// <summary>
    /// Called when the view is navigated to. Override to load data.
    /// </summary>
    /// <param name="parameter">An optional navigation parameter.</param>
    /// <returns>A task that completes when initialization finishes.</returns>
    public virtual Task InitializeAsync(object? parameter = null)
    {
        IsInitialized = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the view is navigated away from. Override to release resources.
    /// </summary>
    /// <returns>A task that completes when cleanup finishes.</returns>
    public virtual Task CleanupAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes an asynchronous operation with loading state management.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
    protected async Task ExecuteWithLoadingAsync(Func<Task> operation)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Executes an asynchronous operation that returns a value with loading state management.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The operation result, or the default value when the operation fails.</returns>
    protected async Task<T?> ExecuteWithLoadingAsync<T>(Func<Task<T>> operation)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return default;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
