using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace LabAssistant.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly string[] _capabilities = ["Machines", "Deploy", "Templates", "Assets", "Diagnostics", "Settings"];
    private string _activeCapability = "Machines";
    private bool _isDrawerOpen;
    private bool _isInsightsOpen;
    private ElementTheme _theme = ElementTheme.Light;
    private int _issueCount = 3;

    public MainWindow()
    {
        InitializeComponent();
        Title = "LabAssistant.WinUI";
        SetInitialSize(1280, 800);
        RootLayout.KeyDown += RootLayout_KeyDown;
        RootLayout.Loaded += (_, _) => RootLayout.Focus(FocusState.Programmatic);
        ApplyState();
    }

    private void SetInitialSize(int width, int height)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow?.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    private void ApplyState()
    {
        BreadcrumbTextBlock.Text = $"Capability / {_activeCapability}";
        ContentTitleTextBlock.Text = $"{_activeCapability} Shell Host";
        ContentDescriptionTextBlock.Text = $"{_activeCapability} feature content is intentionally out of scope for #266.";
        ThemeToggleButton.Content = _theme == ElementTheme.Light ? "Switch to dark" : "Switch to light";
        RootLayout.RequestedTheme = _theme;
        CapabilityDrawer.Visibility = _isDrawerOpen ? Visibility.Visible : Visibility.Collapsed;
        DrawerScrim.Visibility = _isDrawerOpen ? Visibility.Visible : Visibility.Collapsed;
        InsightsPanel.Visibility = _isInsightsOpen ? Visibility.Visible : Visibility.Collapsed;
        IssueBadge.Visibility = _issueCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        IssueBadgeTextBlock.Text = _issueCount.ToString();
    }

    private void HamburgerButton_Click(object sender, RoutedEventArgs e)
    {
        _isDrawerOpen = !_isDrawerOpen;
        ApplyState();
    }

    private void CapabilityButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string capability } && _capabilities.Contains(capability, StringComparer.Ordinal))
        {
            _activeCapability = capability;
            _isDrawerOpen = false;
            ApplyState();
        }
    }

    private void InsightsButton_Click(object sender, RoutedEventArgs e)
    {
        _isInsightsOpen = !_isInsightsOpen;
        ApplyState();
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _theme = _theme == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;
        ApplyState();
    }

    private void DrawerScrim_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _isDrawerOpen = false;
        ApplyState();
    }

    private void RootLayout_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && _isDrawerOpen)
        {
            _isDrawerOpen = false;
            ApplyState();
            e.Handled = true;
        }
    }

    private void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_isDrawerOpen)
        {
            _isDrawerOpen = false;
            ApplyState();
            args.Handled = true;
        }
    }
}
