using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using LabAssistant.WinUI.Theming;
using LabAssistant.WinUI.ViewModels;
using WinRT.Interop;

namespace LabAssistant.WinUI;

public sealed partial class MainWindow : Window
{
    private const double DrawerWidth = 280;
    private const int DrawerAnimationDurationMs = 180;

    private readonly ShellViewModel _shellViewModel = new();
    private ShellCapability _activeCapability;
    private ShellSubview _activeSubview;
    private bool _isDrawerOpen;
    private bool _isInsightsOpen;
    private ElementTheme _theme = ElementTheme.Light;
    private int _issueCount = 3;

    public MainWindow()
    {
        InitializeComponent();
        _activeCapability = _shellViewModel.GetCapability("Machines");
        _activeSubview = _activeCapability.DefaultSubview;
        ConfigureShellIcons();
        InitializeDrawer();
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

    private void InitializeDrawer()
    {
        DrawerTranslateTransform.X = -DrawerWidth;
        CapabilityDrawer.Visibility = Visibility.Collapsed;
        DrawerScrim.Visibility = Visibility.Collapsed;
    }

    private void SetDrawerOpen(bool isOpen)
    {
        if (_isDrawerOpen == isOpen)
        {
            return;
        }

        _isDrawerOpen = isOpen;

        if (isOpen)
        {
            DrawerScrim.Visibility = Visibility.Visible;
            CapabilityDrawer.Visibility = Visibility.Visible;
            AnimateDrawer(-DrawerWidth, 0, onCompleted: null);
            return;
        }

        AnimateDrawer(DrawerTranslateTransform.X, -DrawerWidth, () =>
        {
            CapabilityDrawer.Visibility = Visibility.Collapsed;
            DrawerScrim.Visibility = Visibility.Collapsed;
        });
    }

    private void AnimateDrawer(double from, double to, Action? onCompleted)
    {
        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(DrawerAnimationDurationMs),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(animation, DrawerTranslateTransform);
        Storyboard.SetTargetProperty(animation, nameof(DrawerTranslateTransform.X));
        storyboard.Children.Add(animation);

        if (onCompleted is not null)
        {
            storyboard.Completed += (_, _) => onCompleted();
        }

        storyboard.Begin();
    }

    private void ApplyState()
    {
        BreadcrumbTextBlock.Text = $"{_activeCapability.DisplayName} > {_activeSubview.DisplayName}";
        ContentTitleTextBlock.Text = $"{_activeCapability.DisplayName} > {_activeSubview.DisplayName}";
        ContentDescriptionTextBlock.Text = $"{_activeCapability.DisplayName} subview scaffold is active. Feature behavior remains out of scope in AA2b.";
        SubviewPlaceholderTextBlock.Text = $"Placeholder content: {_activeCapability.DisplayName} / {_activeSubview.DisplayName}.";
        ToolbarLabelTextBlock.Text = $"{_activeSubview.DisplayName} actions";
        ThemeToggleButton.Content = _theme == ElementTheme.Light ? "Switch to dark" : "Switch to light";
        RootLayout.RequestedTheme = _theme;
        InsightsPanel.Visibility = _isInsightsOpen ? Visibility.Visible : Visibility.Collapsed;
        IssueBadge.Visibility = _issueCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        IssueBadgeTextBlock.Text = _issueCount.ToString();
        RenderSubviewSelector();
        RenderSubviewToolbar();
    }

    private void RenderSubviewSelector()
    {
        SubviewSelectorPanel.Children.Clear();

        foreach (var subview in _activeCapability.Subviews)
        {
            var button = new Button
            {
                Content = subview.DisplayName,
                Tag = subview.Key,
                Padding = new Thickness(12, 6, 12, 6),
                BorderThickness = new Thickness(1),
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellBorderBrush"],
                Foreground = subview.Key == _activeSubview.Key
                    ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTopBarForegroundBrush"]
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextPrimaryBrush"],
                Background = subview.Key == _activeSubview.Key
                    ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellAccentBrush"]
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellContentBackgroundBrush"]
            };

            button.Click += SubviewButton_Click;
            SubviewSelectorPanel.Children.Add(button);
        }
    }

    private void RenderSubviewToolbar()
    {
        SubviewToolbarPanel.Children.Clear();

        foreach (var action in _activeSubview.ToolbarActions)
        {
            var button = new Button
            {
                Content = action,
                IsEnabled = false,
                Padding = new Thickness(10, 6, 10, 6)
            };

            SubviewToolbarPanel.Children.Add(button);
        }
    }

    private void HamburgerButton_Click(object sender, RoutedEventArgs e)
    {
        SetDrawerOpen(!_isDrawerOpen);
        ApplyState();
    }

    private void CapabilityButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string capability } &&
            _shellViewModel.Capabilities.Any(entry => string.Equals(entry.DisplayName, capability, StringComparison.Ordinal)))
        {
            _activeCapability = _shellViewModel.GetCapability(capability);
            _activeSubview = _activeCapability.DefaultSubview;
            SetDrawerOpen(false);
            ApplyState();
        }
    }

    private void SubviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string subviewKey })
        {
            return;
        }

        var selectedSubview = _activeCapability.Subviews.FirstOrDefault(
            subview => string.Equals(subview.Key, subviewKey, StringComparison.Ordinal));

        if (selectedSubview is null)
        {
            return;
        }

        _activeSubview = selectedSubview;
        ApplyState();
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
        SetDrawerOpen(false);
        ApplyState();
    }

    private void RootLayout_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && _isDrawerOpen)
        {
            SetDrawerOpen(false);
            ApplyState();
            e.Handled = true;
        }
    }

    private void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_isDrawerOpen)
        {
            SetDrawerOpen(false);
            ApplyState();
            args.Handled = true;
        }
    }
}
