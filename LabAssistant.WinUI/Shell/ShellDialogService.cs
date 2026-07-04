using LabAssistant.Business.Assets;
using LabAssistant.Business.Templates;
using LabAssistant.WinUI.Interop;
using LabAssistant.WinUI.Models.Assets;
using LabAssistant.WinUI.ViewModels.Assets;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Shell;

internal sealed class ShellDialogService
{
    private readonly Func<nint> _getWindowHandle;
    private readonly Func<XamlRoot?> _getXamlRoot;

    public ShellDialogService(Func<nint> getWindowHandle, Func<XamlRoot?> getXamlRoot)
    {
        _getWindowHandle = getWindowHandle;
        _getXamlRoot = getXamlRoot;
    }

    public string? PickBaseDiskFilePath()
    {
        var selectedPath = NativeFileDialogs.ShowOpenVhdxDialog(_getWindowHandle());
        return string.IsNullOrWhiteSpace(selectedPath) ? null : selectedPath;
    }

    public Task<string?> PickTemplateFileForOpenAsync()
    {
        return Task.FromResult(NativeFileDialogs.ShowOpenJsonDialog(_getWindowHandle()));
    }

    public Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName)
    {
        return Task.FromResult(NativeFileDialogs.ShowSaveJsonDialog(_getWindowHandle(), suggestedFileName));
    }

    public async Task<bool> ShowAssetsBaseDiskRemoveConfirmationDialogAsync(
        AssetsBaseDiskListRow row,
        AssetsBaseDiskRemovalAssessment assessment)
    {
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to remove '{row.DisplayName}' from the registry."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = "This removes the base disk from the LabAssistant registry only. It does not delete the underlying VHDX file.",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = assessment.ReferenceSignalSummary,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });

        foreach (var warning in assessment.WarningReasons)
        {
            content.Children.Add(new TextBlock
            {
                Text = "- " + warning,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellWarnBrush"]
            });
        }

        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Remove Base Disk",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = _getXamlRoot(),
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task<bool> ShowAssetsSwitchDeleteConfirmationDialogAsync(
        AssetsSwitchListRow row,
        AssetsSwitchDeleteAssessment assessment)
    {
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to delete virtual switch '{row.Name}'."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = "Delete is allowed only when no Hyper-V VM is attached to the switch. VM power state does not make delete safe.",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = assessment.Summary,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });
        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Delete Virtual Switch",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = _getXamlRoot(),
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task<bool> ShowDeleteTemplateConfirmationDialogAsync(TemplateLibraryItem selectedTemplate)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = _getXamlRoot(),
            Title = "Delete Template",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            Content = $"Delete '{selectedTemplate.Name}'? This removes the template file.",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task<bool> ShowRemoveTemplateVmConfirmationDialogAsync(string vmName)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = _getXamlRoot(),
            Title = "Remove VM Entry",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            Content = $"Remove VM entry '{vmName}' from this template draft?",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
