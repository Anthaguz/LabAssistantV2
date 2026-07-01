using System;

namespace LabAssistant.WinUI.Infrastructure;

/// <summary>
/// Service for showing non-blocking notifications (InfoBar-based).
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows a success notification.
    /// </summary>
    /// <param name="message">The notification message.</param>
    /// <param name="title">The optional notification title.</param>
    void ShowSuccess(string message, string? title = null);

    /// <summary>
    /// Shows a warning notification.
    /// </summary>
    /// <param name="message">The notification message.</param>
    /// <param name="title">The optional notification title.</param>
    void ShowWarning(string message, string? title = null);

    /// <summary>
    /// Shows an error notification.
    /// </summary>
    /// <param name="message">The notification message.</param>
    /// <param name="title">The optional notification title.</param>
    void ShowError(string message, string? title = null);

    /// <summary>
    /// Shows an informational notification.
    /// </summary>
    /// <param name="message">The notification message.</param>
    /// <param name="title">The optional notification title.</param>
    void ShowInfo(string message, string? title = null);

    /// <summary>
    /// Dismisses the current notification.
    /// </summary>
    void Dismiss();

    /// <summary>
    /// Raised when a notification should be displayed.
    /// </summary>
    event EventHandler<NotificationEventArgs>? NotificationRequested;

    /// <summary>
    /// Raised when the current notification should be dismissed.
    /// </summary>
    event EventHandler? DismissRequested;
}

/// <summary>
/// The severity of an application notification.
/// </summary>
public enum NotificationSeverity
{
    /// <summary>
    /// Informational notification.
    /// </summary>
    Info,

    /// <summary>
    /// Success notification.
    /// </summary>
    Success,

    /// <summary>
    /// Warning notification.
    /// </summary>
    Warning,

    /// <summary>
    /// Error notification.
    /// </summary>
    Error,
}

/// <summary>
/// Event data for notification requests.
/// </summary>
public sealed class NotificationEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationEventArgs"/> class.
    /// </summary>
    /// <param name="message">The notification message.</param>
    /// <param name="title">The optional notification title.</param>
    /// <param name="severity">The notification severity.</param>
    public NotificationEventArgs(string message, string? title, NotificationSeverity severity)
    {
        Message = message;
        Title = title;
        Severity = severity;
    }

    /// <summary>
    /// Gets the notification message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the optional notification title.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Gets the notification severity.
    /// </summary>
    public NotificationSeverity Severity { get; }
}
