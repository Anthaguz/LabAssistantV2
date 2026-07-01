using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Infrastructure;

/// <summary>
/// Default <see cref="IDialogService"/> implementation backed by <see cref="ContentDialog"/>.
/// </summary>
public sealed class DialogService : IDialogService
{
    private XamlRoot? _xamlRoot;

    /// <summary>
    /// Sets the <see cref="XamlRoot"/> used to display dialogs.
    /// </summary>
    /// <param name="xamlRoot">The active XAML root for the current window.</param>
    public void SetXamlRoot(XamlRoot xamlRoot)
    {
        _xamlRoot = xamlRoot ?? throw new ArgumentNullException(nameof(xamlRoot));
    }

    /// <inheritdoc />
    public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel")
    {
        var dialog = CreateDialog(title, message, confirmText, cancelText);
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    /// <inheritdoc />
    public async Task ShowErrorAsync(string title, string message)
    {
        var dialog = CreateDialog(title, message, "OK");
        await dialog.ShowAsync();
    }

    /// <inheritdoc />
    public async Task ShowInfoAsync(string title, string message)
    {
        var dialog = CreateDialog(title, message, "OK");
        await dialog.ShowAsync();
    }

    /// <inheritdoc />
    public async Task<string?> ShowInputAsync(string title, string placeholder, string? defaultValue = null)
    {
        var textBox = new TextBox
        {
            PlaceholderText = placeholder,
            Text = defaultValue ?? string.Empty,
        };

        var dialog = CreateDialog(title, textBox, "OK", "Cancel");
        var result = await dialog.ShowAsync();

        return result == ContentDialogResult.Primary ? textBox.Text : null;
    }

    private ContentDialog CreateDialog(string title, object content, string primaryButtonText, string? closeButtonText = null)
    {
        return new ContentDialog
        {
            XamlRoot = EnsureXamlRoot(),
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText ?? string.Empty,
            DefaultButton = ContentDialogButton.Primary,
        };
    }

    private XamlRoot EnsureXamlRoot()
    {
        return _xamlRoot ?? throw new InvalidOperationException("DialogService requires a XamlRoot. Call SetXamlRoot after the window has loaded.");
    }
}
