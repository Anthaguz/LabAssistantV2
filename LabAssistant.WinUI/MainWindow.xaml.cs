using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using LabAssistant.WinUI.Theming;
using LabAssistant.WinUI.ViewModels;
using WinRT.Interop;

namespace LabAssistant.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly ShellViewModel _shellViewModel = new();
    private string _activeCapability = "Machines";
    private bool _isDrawerOpen;
    private bool _isInsightsOpen;
    private ElementTheme _theme = ElementTheme.Light;
    private int _issueCount = 3;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureShellIcons();
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

    private void ConfigureShellIcons()
    {
        HamburgerButton.Content = CreateIconGlyph(ShellIconToken.Menu);
        InsightsToggleButton.Content = CreateIconGlyph(ShellIconToken.Insights);

        MachinesRailButton.Content = CreateIconGlyph(ShellIconToken.Machines);
        DeployRailButton.Content = CreateIconGlyph(ShellIconToken.Deploy);
        TemplatesRailButton.Content = CreateIconGlyph(ShellIconToken.Templates);
        AssetsRailButton.Content = CreateIconGlyph(ShellIconToken.Assets);
        DiagnosticsRailButton.Content = CreateIconGlyph(ShellIconToken.Diagnostics);
        SettingsRailButton.Content = CreateIconGlyph(ShellIconToken.Settings);

        MachinesDrawerButton.Content = CreateDrawerButtonContent(ShellIconToken.Machines, "Machines");
        DeployDrawerButton.Content = CreateDrawerButtonContent(ShellIconToken.Deploy, "Deploy");
        TemplatesDrawerButton.Content = CreateDrawerButtonContent(ShellIconToken.Templates, "Templates");
        AssetsDrawerButton.Content = CreateDrawerButtonContent(ShellIconToken.Assets, "Assets");
        DiagnosticsDrawerButton.Content = CreateDrawerButtonContent(ShellIconToken.Diagnostics, "Diagnostics");
        SettingsDrawerButton.Content = CreateDrawerButtonContent(ShellIconToken.Settings, "Settings");
    }

    private object CreateDrawerButtonContent(string token, string label)
    {
        var container = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        container.Children.Add(CreateIconGlyph(token));
        container.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTopBarForegroundBrush"]
        });
        return container;
    }

    private TextBlock CreateIconGlyph(string token)
    {
        return new TextBlock
        {
            Text = ShellIconCatalog.GetGlyph(token),
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTopBarForegroundBrush"]
        };
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
        if (sender is Button { Tag: string capability } &&
            _shellViewModel.Capabilities.Any(entry => string.Equals(entry.DisplayName, capability, StringComparison.Ordinal)))
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
