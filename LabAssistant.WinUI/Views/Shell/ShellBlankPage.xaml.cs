using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Shell;

/// <summary>
/// Empty placeholder page the capability frame navigates to when the active capability is not
/// (yet) served by an on-demand page. Navigating here forces the previous capability page through
/// its real <c>OnNavigatedFrom</c>/<c>Unloaded</c> teardown instead of leaving it loaded-but-hidden,
/// which is required for cleanup of page-owned resources (timers, in-flight work).
/// </summary>
public sealed partial class ShellBlankPage : Page
{
    public ShellBlankPage()
    {
        InitializeComponent();
    }
}
