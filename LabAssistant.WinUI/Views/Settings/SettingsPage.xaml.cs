using LabAssistant.WinUI.Shell;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace LabAssistant.WinUI.Views.Settings;

/// <summary>
/// Capability page that owns the Settings surface while it is the active shell content. Settings
/// currently exposes a single subview (Machines deletion policy), so the page simply hosts the
/// Machines settings view; the view resolves and drives its own view model. The page is created
/// on route entry and torn down on leave like the other migrated capability pages, and holds no
/// orchestration state of its own.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private IShellHost? _shellHost;

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ShellNavigationRequest request)
        {
            _shellHost = request.Host;
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _shellHost = null;
    }
}
