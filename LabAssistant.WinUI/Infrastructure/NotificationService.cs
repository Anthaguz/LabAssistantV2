using System;

namespace LabAssistant.WinUI.Infrastructure;

/// <summary>
/// Default <see cref="INotificationService"/> implementation that raises notification events for the UI layer.
/// </summary>
public sealed class NotificationService : INotificationService
{
    /// <inheritdoc />
    public event EventHandler<NotificationEventArgs>? NotificationRequested;

    /// <inheritdoc />
    public event EventHandler? DismissRequested;

    /// <inheritdoc />
    public void ShowSuccess(string message, string? title = null)
    {
        RaiseNotification(message, title, NotificationSeverity.Success);
    }

    /// <inheritdoc />
    public void ShowWarning(string message, string? title = null)
    {
        RaiseNotification(message, title, NotificationSeverity.Warning);
    }

    /// <inheritdoc />
    public void ShowError(string message, string? title = null)
    {
        RaiseNotification(message, title, NotificationSeverity.Error);
    }

    /// <inheritdoc />
    public void ShowInfo(string message, string? title = null)
    {
        RaiseNotification(message, title, NotificationSeverity.Info);
    }

    /// <inheritdoc />
    public void Dismiss()
    {
        DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseNotification(string message, string? title, NotificationSeverity severity)
    {
        NotificationRequested?.Invoke(this, new NotificationEventArgs(message, title, severity));
    }
}
