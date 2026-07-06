using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Shell;

/// <summary>
/// Empty placeholder page the capability frame navigates to from <see cref="MainWindow"/>'s
/// <c>Closed</c> handler, forcing whatever capability page is currently live through its real
/// <c>OnNavigatedFrom</c>/<c>Unloaded</c> teardown (timers, in-flight work) before the window is
/// destroyed, instead of leaving that cleanup to process exit.
/// </summary>
public sealed partial class ShellBlankPage : Page
{
    public ShellBlankPage()
    {
        InitializeComponent();
    }
}
