using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Exposes only the shell-owned interactions that the Quick Deploy owner still needs.
/// </summary>
internal sealed class DeployOnTheFlyWorkspaceShellBridge
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Func<XamlRoot?> _getXamlRoot;
    private readonly Action _requestResultsPanelToggle;
    private readonly Action _refreshResultsPanelState;

    public DeployOnTheFlyWorkspaceShellBridge(
        DispatcherQueue dispatcherQueue,
        Func<XamlRoot?> getXamlRoot,
        Action requestResultsPanelToggle,
        Action refreshResultsPanelState)
    {
        _dispatcherQueue = dispatcherQueue;
        _getXamlRoot = getXamlRoot;
        _requestResultsPanelToggle = requestResultsPanelToggle;
        _refreshResultsPanelState = refreshResultsPanelState;
    }

    public void EnqueueUiUpdate(Action updateAction)
    {
        _dispatcherQueue.TryEnqueue(() => updateAction());
    }

    public async Task<bool> ShowRemoveVmEntryConfirmationDialogAsync(string vmName)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = _getXamlRoot(),
            Title = "Remove VM Entry",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            Content = $"Remove '{vmName}' from quick deploy configuration?",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public void RequestResultsPanelToggle() => _requestResultsPanelToggle();

    public void RefreshResultsPanelState() => _refreshResultsPanelState();
}
