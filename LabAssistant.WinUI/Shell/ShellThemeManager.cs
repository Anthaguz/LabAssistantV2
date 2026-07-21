using System.Diagnostics;
using LabAssistant.Models.Configuration;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace LabAssistant.WinUI.Shell;

internal sealed class ShellThemeManager
{
    private const string ShellBrandingIconRelativePath = @"Assets\Branding\AppIcon.ico";
    private const string ShellBrandingImageRelativePath = @"Assets\Branding\AppIcon.png";

    private readonly FrameworkElement _rootLayout;
    private readonly Button _themeToggleButton;
    private readonly Image _shellBrandingImage;
    private readonly Func<nint> _getWindowHandle;
    private readonly ShellThemeState _themeState;

    public ShellThemeManager(
        FrameworkElement rootLayout,
        Button themeToggleButton,
        Image shellBrandingImage,
        Func<nint> getWindowHandle,
        IAppSettingsStore settingsStore)
    {
        _rootLayout = rootLayout;
        _themeToggleButton = themeToggleButton;
        _shellBrandingImage = shellBrandingImage;
        _getWindowHandle = getWindowHandle;
        _themeState = new ShellThemeState(settingsStore);
    }

    public void ToggleTheme()
    {
        _themeState.Toggle();
        ApplyTheme();
    }

    public void ApplyTheme()
    {
        var elementTheme = ToElementTheme(_themeState.Theme);
        _themeToggleButton.Content = elementTheme == ElementTheme.Light ? "Switch to dark" : "Switch to light";
        _rootLayout.RequestedTheme = elementTheme;
    }

    private static ElementTheme ToElementTheme(AppTheme theme) =>
        theme == AppTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;

    public void InitializeShellBranding()
    {
        TryApplyShellHeaderBranding(GetBrandingAssetPath(ShellBrandingImageRelativePath));
        TryApplyNativeWindowIcon(GetBrandingAssetPath(ShellBrandingIconRelativePath));
    }

    private static string GetBrandingAssetPath(string relativePath)
    {
        return Path.Combine(AppContext.BaseDirectory, relativePath);
    }

    private void TryApplyShellHeaderBranding(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            Debug.WriteLine($"[ShellBranding] Header image asset not found: {imagePath}");
            _shellBrandingImage.Visibility = Visibility.Collapsed;
            return;
        }

        _shellBrandingImage.Source = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
        _shellBrandingImage.Visibility = Visibility.Visible;
    }

    private void TryApplyNativeWindowIcon(string iconPath)
    {
        if (!File.Exists(iconPath))
        {
            Debug.WriteLine($"[ShellBranding] Native icon asset not found: {iconPath}");
            return;
        }

        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_getWindowHandle());
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow?.SetIcon(iconPath);
    }
}
