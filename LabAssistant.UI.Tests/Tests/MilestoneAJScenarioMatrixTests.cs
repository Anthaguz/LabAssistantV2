using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneAJScenarioMatrixTests
{
    [Fact]
    public void ShellViewModel_DefinesAssetsBaseDisksRouteAndSubview()
    {
        var source = LoadShellViewModelSource();

        Assert.Contains("public const string AssetsBaseDisks = \"assets.base_disks\";", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.AssetsBaseDisks, \"Base Disks\"", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.AssetsSwitches, \"Virtual Switches\"", source);
    }

    [Fact]
    public void MainWindow_HostsAssetsBaseDisksViewInShell()
    {
        var xaml = LoadMainWindowXaml();
        var xamlSource = LoadMainWindowXamlSource();
        var source = LoadMainWindowSource();

        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksViewHost"));
        Assert.Contains("xmlns:assetsViews=\"using:LabAssistant.WinUI.Views.Assets\"", xamlSource);
        Assert.Contains("private AssetsBaseDisksView AssetsBaseDisksView => AssetsBaseDisksViewHost;", source);
        Assert.Contains("private FrameworkElement AssetsBaseDisksPanel => AssetsBaseDisksViewHost;", source);
        Assert.Contains("AssetsBaseDisksPanel.Visibility = IsAssetsBaseDisksActive ? Visibility.Visible : Visibility.Collapsed;", source);
    }

    [Fact]
    public void AssetsBaseDisksView_DefinesScaffoldRegionsAndExplicitStateContainers()
    {
        var xaml = LoadAssetsBaseDisksViewXaml();

        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksActionsRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksStatusRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksListRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksDetailsRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksMetadataEditRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksLoadingStatePanel"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksEmptyStatePanel"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksErrorStatePanel"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksRefreshButton"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksImportButton"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksRemoveButton"));
    }

    [Fact]
    public void Aj2_RemainsScaffoldOnly_WithoutAssetsRuntimeOperationWiring()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("private bool IsAssetsBaseDisksActive =>", source);
        Assert.DoesNotContain("EnsureAssetsBaseDisks", source);
        Assert.DoesNotContain("LoadAssetsBaseDisks", source);
        Assert.DoesNotContain("RegisterBaseDisk", source);
        Assert.DoesNotContain("RemoveBaseDisk", source);
    }

    private static string LoadShellViewModelSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "ShellViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XDocument LoadMainWindowXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static string LoadMainWindowSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadMainWindowXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XDocument LoadAssetsBaseDisksViewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsBaseDisksView.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
