using LabAssistant.Business.Machines;
using LabAssistant.WinUI.Views.Machines;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Machines;

internal interface IMachinesCapabilityShellBridge
{
    bool IsMachinesOverviewActive { get; }

    void UpdateReadinessPollingState();

    Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview);

    Task<bool> ShowDeleteConfirmationDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview, MachineDeleteScope effectiveScope);
}

internal sealed class MachinesCapabilityShellBridge : IMachinesCapabilityShellBridge
{
    private readonly Func<bool> _isMachinesOverviewActive;
    private readonly Action _updateReadinessPollingState;
    private readonly Func<XamlRoot?> _getXamlRoot;

    public MachinesCapabilityShellBridge(
        Func<bool> isMachinesOverviewActive,
        Action updateReadinessPollingState,
        Func<XamlRoot?> getXamlRoot)
    {
        _isMachinesOverviewActive = isMachinesOverviewActive;
        _updateReadinessPollingState = updateReadinessPollingState;
        _getXamlRoot = getXamlRoot;
    }

    public bool IsMachinesOverviewActive => _isMachinesOverviewActive();

    public void UpdateReadinessPollingState() => _updateReadinessPollingState();

    public async Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview)
    {
        var vmOnlyRadio = new RadioButton
        {
            Content = "VM registration only",
            IsChecked = preview.DefaultScope == MachineDeleteScope.VmRegistrationOnly
        };
        var vmAndStorageRadio = new RadioButton
        {
            IsChecked = preview.DefaultScope == MachineDeleteScope.VmAndStorage,
            Content = "VM + associated disks/files"
        };
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to delete '{vm.VmName}'."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = "Choose delete scope. This action is destructive.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Policy: {preview.PolicyMode} - {preview.PolicyMessage}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });

        foreach (var disk in preview.DiskClassifications)
        {
            content.Children.Add(new TextBlock
            {
                Text = $"Disk: {disk.DiskPath} | {disk.Classification} ({disk.Reason})",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
            });
        }

        content.Children.Add(vmOnlyRadio);
        content.Children.Add(vmAndStorageRadio);
        content.Children.Add(new TextBlock
        {
            Text = "If deleting with storage, associated disks/files will be removed where possible.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Delete VM",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = _getXamlRoot(),
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return vmAndStorageRadio.IsChecked == true
            ? MachineDeleteScope.VmAndStorage
            : MachineDeleteScope.VmRegistrationOnly;
    }

    public async Task<bool> ShowDeleteConfirmationDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview, MachineDeleteScope effectiveScope)
    {
        var scopeText = effectiveScope == MachineDeleteScope.VmAndStorage
            ? "VM + associated disks/files"
            : "VM registration only";
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to delete '{vm.VmName}'."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = $"Effective delete scope: {scopeText}",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Policy: {preview.PolicyMode} - {preview.PolicyMessage}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });
        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Delete VM",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = _getXamlRoot(),
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}

internal sealed class MachinesCapabilityRuntime
{
    private readonly MachinesViewModel _viewModel;

    public MachinesCapabilityRuntime(
        IMachinesCapabilityService machinesCapabilityService,
        MachinesOverviewView view,
        IMachinesCapabilityShellBridge shellBridge)
    {
        ArgumentNullException.ThrowIfNull(machinesCapabilityService);
        _viewModel = view.ViewModel;
        _viewModel.AttachShellBridge(shellBridge);
    }

    public bool HasInventory => _viewModel.HasInventory;

    public DateTimeOffset LastRdpReadinessRefreshUtc => _viewModel.LastRdpReadinessRefreshUtc;

    public void ApplyShellState() => _viewModel.ApplyShellState();

    public void DiscardEditDraft() => _viewModel.DiscardEditDraft();

    public Task EnsureInventoryAsync(bool forceRefresh) => _viewModel.EnsureInventoryAsync(forceRefresh);

    public Task RefreshRdpReadinessAsync(bool selectedOnly) => _viewModel.RefreshRdpReadinessAsync(selectedOnly);
}
