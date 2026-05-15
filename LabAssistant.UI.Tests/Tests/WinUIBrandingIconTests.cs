using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class WinUIBrandingIconTests
{
    [Fact]
    public void WinUIProject_OwnsApplicationIconAndBrandingAssets()
    {
        var projectSource = LoadWinUiProjectSource();

        Assert.Contains("<ApplicationIcon>Assets\\Branding\\AppIcon.ico</ApplicationIcon>", projectSource);
        Assert.Contains("<None Update=\"Assets\\Branding\\AppIcon.ico\">", projectSource);
        Assert.Contains("<None Update=\"Assets\\Branding\\AppIcon.png\">", projectSource);
        Assert.Contains("<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>", projectSource);
    }

    [Fact]
    public void MainWindow_TopBarHostsBrandingImageAndNativeIconBootstrap()
    {
        var mainWindowXaml = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "LabAssistant.WinUI",
            "MainWindow.xaml"));
        var mainWindowSource = LoadMainWindowSource();

        Assert.NotNull(FindByName(mainWindowXaml, "ShellBrandingImage"));
        Assert.Contains("private const string ShellBrandingIconRelativePath = @\"Assets\\Branding\\AppIcon.ico\";", mainWindowSource);
        Assert.Contains("private const string ShellBrandingImageRelativePath = @\"Assets\\Branding\\AppIcon.png\";", mainWindowSource);
        Assert.Contains("InitializeShellBranding();", mainWindowSource);
        Assert.Contains("TryApplyShellHeaderBranding(GetBrandingAssetPath(ShellBrandingImageRelativePath));", mainWindowSource);
        Assert.Contains("TryApplyNativeWindowIcon(GetBrandingAssetPath(ShellBrandingIconRelativePath));", mainWindowSource);
        Assert.Contains("appWindow?.SetIcon(iconPath);", mainWindowSource);
    }

    private static string LoadWinUiProjectSource()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "LabAssistant.WinUI",
            "LabAssistant.WinUI.csproj");
        return File.ReadAllText(path);
    }

    private static string LoadMainWindowSource()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "LabAssistant.WinUI",
            "MainWindow.xaml.cs");
        return File.ReadAllText(path);
    }

    private static XElement? FindByName(XContainer root, string name)
    {
        return root.Descendants().FirstOrDefault(element =>
            string.Equals((string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")), name, StringComparison.Ordinal) ||
            string.Equals((string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")), name, StringComparison.Ordinal));
    }
}
