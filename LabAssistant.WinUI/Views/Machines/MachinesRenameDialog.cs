using LabAssistant.Business.Machines;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Machines;

/// <summary>
/// Builder for the Machines rename dialog (F08). Kept beside the delete dialog builders so the
/// capability page owns the shell seam while the dialog construction stays out of the page code.
/// </summary>
internal static class MachinesRenameDialog
{
    public static async Task<string?> ShowAsync(XamlRoot? xamlRoot, MachineInventoryItem vm)
    {
        var nameBox = new TextBox
        {
            Text = vm.VmName,
            SelectionStart = 0,
            SelectionLength = vm.VmName.Length,
            MaxLength = MachineNameValidator.MaxNameLength
        };
        AutomationProperties.SetName(nameBox, "New virtual machine name");

        var validationText = new TextBlock
        {
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellCriticalBrush"],
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new TextBlock
        {
            Text = $"Enter a new name for '{vm.VmName}'.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(nameBox);
        content.Children.Add(validationText);

        var dialog = new ContentDialog
        {
            Title = "Rename VM",
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            Content = content
        };

        void Revalidate()
        {
            var result = MachineNameValidator.Validate(nameBox.Text, vm.VmName);
            dialog.IsPrimaryButtonEnabled = result.IsValid;
            if (result.IsValid)
            {
                validationText.Visibility = Visibility.Collapsed;
            }
            else
            {
                validationText.Text = result.ErrorMessage;
                validationText.Visibility = Visibility.Visible;
            }
        }

        nameBox.TextChanged += (_, _) => Revalidate();
        Revalidate();

        var dialogResult = await dialog.ShowAsync();
        if (dialogResult != ContentDialogResult.Primary)
        {
            return null;
        }

        return nameBox.Text.Trim();
    }
}
