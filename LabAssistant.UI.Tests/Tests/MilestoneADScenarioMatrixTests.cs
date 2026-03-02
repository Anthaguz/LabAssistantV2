using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneADScenarioMatrixTests
{
    [Fact]
    public void ShellViewModel_DefinesTemplatesCanonicalRoutes_AndDefaultChild()
    {
        var source = LoadShellViewModelSource();

        Assert.Contains("public const string TemplatesLibrary = \"templates.library\";", source);
        Assert.Contains("public const string TemplatesEditor = \"templates.editor\";", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.TemplatesLibrary, \"Library\"", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.TemplatesEditor, \"Editor\"", source);
        Assert.Contains("DefaultSubview = subviews[0];", source);
    }

    [Fact]
    public void MainWindow_DefinesTemplatesScaffoldHosts_AndLocalSubNavigation()
    {
        var xaml = LoadMainWindowXaml();

        Assert.NotNull(FindByName(xaml, "TemplatesLocalNavigationPanel"));
        Assert.NotNull(FindByName(xaml, "TemplatesSubviewTabView"));
        Assert.NotNull(FindByName(xaml, "TemplatesLibraryTabViewItem"));
        Assert.NotNull(FindByName(xaml, "TemplatesEditorTabViewItem"));
        Assert.NotNull(FindByName(xaml, "TemplatesLibraryViewHost"));
        Assert.NotNull(FindByName(xaml, "TemplatesEditorViewHost"));
    }

    [Fact]
    public void MainWindow_WiresTemplatesLocalNavigation_WithoutChangingGlobalFooterContract()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("TemplatesSubviewTabView_SelectionChanged", source);
        Assert.Contains("SyncTemplatesSubviewSelection()", source);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.TemplatesLibrary);", source);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.TemplatesEditor);", source);
        Assert.Contains("GlobalNavigationView.FooterMenuItems.Add(parentItem);", source);
    }

    private static XDocument LoadMainWindowXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static string LoadShellViewModelSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "ShellViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadMainWindowSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
