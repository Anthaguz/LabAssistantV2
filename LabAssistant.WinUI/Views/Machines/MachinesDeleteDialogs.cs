using LabAssistant.Business.Machines;
using LabAssistant.WinUI.ViewModels.Machines;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Machines;

/// <summary>
/// Builders for the Machines destructive-delete confirmation dialogs. Relocated verbatim from the
/// former <c>MachinesCapabilityShellBridge</c> so the delete flow keeps its exact behavior while
/// the capability page owns the shell seam.
/// </summary>
internal static class MachinesDeleteDialogs
{
    public static async Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync(
        XamlRoot? xamlRoot,
        MachineInventoryItem vm,
        MachineDeletePreview preview)
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
            XamlRoot = xamlRoot,
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

    public static async Task<bool> ShowDeleteConfirmationDialogAsync(
        XamlRoot? xamlRoot,
        MachineInventoryItem vm,
        MachineDeletePreview preview,
        MachineDeleteScope effectiveScope)
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
            XamlRoot = xamlRoot,
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Batch delete confirmation for several selected VMs (F07). One scope choice applies to all:
    /// VM only never removes storage; VM + storage is an explicit override that removes storage for
    /// every selected VM, including any the policy flagged unsafe for automatic storage deletion.
    /// Those unsafe VMs are surfaced so the user makes an informed choice. Returns the chosen scope,
    /// or null when the user cancels.
    /// </summary>
    public static async Task<MachineDeleteScope?> ShowBulkDeleteScopeDialogAsync(
        XamlRoot? xamlRoot,
        IReadOnlyList<MachineBulkDeleteCandidate> candidates)
    {
        var secondaryBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"];

        var vmOnlyRadio = new RadioButton
        {
            Content = "Remove VM only",
            IsChecked = true
        };
        var vmAndStorageRadio = new RadioButton
        {
            IsChecked = false,
            Content = "Remove VM and delete storage"
        };
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to delete these {candidates.Count} VM(s)."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = $"Delete {candidates.Count} selected VM(s). This action is destructive.",
            TextWrapping = TextWrapping.Wrap
        });

        var vmList = new StackPanel { Spacing = 2 };
        foreach (var candidate in candidates)
        {
            vmList.Children.Add(new TextBlock
            {
                Text = $"- {candidate.Vm.VmName}",
                TextWrapping = TextWrapping.Wrap,
                Foreground = secondaryBrush
            });
        }

        content.Children.Add(vmList);

        var unsafeNames = candidates
            .Where(candidate => !candidate.Preview.SafeForAutomaticStorageDeletion)
            .Select(candidate => candidate.Vm.VmName)
            .ToList();

        if (unsafeNames.Count > 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = "Flagged as unsafe for automatic storage deletion: " + string.Join(", ", unsafeNames) +
                    ". Choosing \"Remove VM and delete storage\" overrides this and deletes their storage anyway.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellWarnBrush"]
            });
        }

        content.Children.Add(vmOnlyRadio);
        content.Children.Add(vmAndStorageRadio);
        content.Children.Add(new TextBlock
        {
            Text = "\"Remove VM only\" leaves disks and files on disk. \"Remove VM and delete storage\" removes associated disks/files where possible for every selected VM.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = secondaryBrush
        });
        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Delete VMs",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = xamlRoot,
            Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
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
}
