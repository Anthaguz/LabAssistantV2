using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace LabAssistant.WinUI.Shell;

internal sealed class ShellKeyboardHandler
{
    private readonly NavigationView _navigationView;

    public ShellKeyboardHandler(NavigationView navigationView)
    {
        _navigationView = navigationView;
    }

    public void HandleRootLayoutKeyDown(KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && _navigationView.IsPaneOpen)
        {
            _navigationView.IsPaneOpen = false;
            e.Handled = true;
        }
    }

    public void HandleEscapeAccelerator(KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_navigationView.IsPaneOpen)
        {
            _navigationView.IsPaneOpen = false;
            args.Handled = true;
        }
    }
}
