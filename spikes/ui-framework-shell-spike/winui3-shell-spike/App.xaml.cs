using Microsoft.UI.Xaml;

namespace WinUi3ShellSpike;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        RequestedTheme = ApplicationTheme.Light;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
