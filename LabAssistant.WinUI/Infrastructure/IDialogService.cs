using System.Threading.Tasks;

namespace LabAssistant.WinUI.Infrastructure;

/// <summary>
/// Abstraction for showing dialogs, decoupled from XamlRoot.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a confirmation dialog and returns whether the user confirmed.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The dialog message.</param>
    /// <param name="confirmText">The primary action text.</param>
    /// <param name="cancelText">The close action text.</param>
    /// <returns><see langword="true"/> when the user confirms; otherwise <see langword="false"/>.</returns>
    Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel");

    /// <summary>
    /// Shows an error dialog.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The dialog message.</param>
    /// <returns>A task that completes when the dialog closes.</returns>
    Task ShowErrorAsync(string title, string message);

    /// <summary>
    /// Shows an informational dialog.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The dialog message.</param>
    /// <returns>A task that completes when the dialog closes.</returns>
    Task ShowInfoAsync(string title, string message);

    /// <summary>
    /// Shows an input dialog and returns the entered text when confirmed.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="placeholder">The input placeholder text.</param>
    /// <param name="defaultValue">The initial input value.</param>
    /// <returns>The entered text when confirmed; otherwise <see langword="null"/>.</returns>
    Task<string?> ShowInputAsync(string title, string placeholder, string? defaultValue = null);
}
