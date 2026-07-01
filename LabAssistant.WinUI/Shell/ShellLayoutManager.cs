using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Shell;

internal sealed class ShellLayoutManager
{
    public const double ShellRightPanelCompactThreshold = 1200;
    public const double ShellNavigationDrawerThreshold = 1100;

    private readonly NavigationView _navigationView;
    private bool _isCompactFallback;

    public ShellLayoutManager(NavigationView navigationView)
    {
        _navigationView = navigationView;
    }

    public event EventHandler<bool>? CompactFallbackChanged;

    public void ApplyShellNavigationMode(double width)
    {
        var useDrawerMode = width < ShellNavigationDrawerThreshold;
        _navigationView.PaneDisplayMode = useDrawerMode
            ? NavigationViewPaneDisplayMode.LeftMinimal
            : NavigationViewPaneDisplayMode.LeftCompact;
        _navigationView.CompactPaneLength = useDrawerMode ? 0 : 56;

        if (useDrawerMode)
        {
            _navigationView.IsPaneOpen = false;
        }
    }

    public void HandleRootLayoutSizeChanged(double width)
    {
        ApplyShellNavigationMode(width);

        var isCompact = width < ShellRightPanelCompactThreshold;
        if (_isCompactFallback == isCompact)
        {
            return;
        }

        _isCompactFallback = isCompact;
        CompactFallbackChanged?.Invoke(this, isCompact);
    }
}
