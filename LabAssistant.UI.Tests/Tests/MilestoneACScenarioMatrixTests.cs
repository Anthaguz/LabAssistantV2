using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneACScenarioMatrixTests
{
    [Fact]
    public void MainWindow_DefinesShellNavigationInfrastructure_ForBaselineAndRuntimeCompactModes()
    {
        var xaml = LoadMainWindowXaml();
        var source = LoadMainWindowSource();
        var nav = FindByName(xaml, "GlobalNavigationView");

        Assert.Equal("LeftCompact", nav.Attribute("PaneDisplayMode")?.Value);
        Assert.Equal("False", nav.Attribute("IsPaneToggleButtonVisible")?.Value);
        Assert.Equal("False", nav.Attribute("IsSettingsVisible")?.Value);
        Assert.Equal("Collapsed", nav.Attribute("IsBackButtonVisible")?.Value);
        Assert.Equal("56", nav.Attribute("CompactPaneLength")?.Value);
        Assert.Equal("280", nav.Attribute("OpenPaneLength")?.Value);
        Assert.Equal("GlobalNavigationView_ItemInvoked", nav.Attribute("ItemInvoked")?.Value);
        Assert.Contains("private void ApplyShellNavigationMode(double width)", source);
        Assert.Contains("NavigationViewPaneDisplayMode.LeftMinimal", source);
    }

    [Fact]
    public void ShellViewModel_DefinesCanonicalRouteKeysAndStartupRoute()
    {
        var source = LoadShellViewModelSource();

        Assert.Contains("public const string MachinesOverview = \"machines.overview\";", source);
        Assert.Contains("public const string DeployOnTheFly = \"deploy.on_the_fly\";", source);
        Assert.Contains("public const string DeployFromTemplate = \"deploy.from_template\";", source);
        Assert.Contains("public const string TemplatesLibrary = \"templates.library\";", source);
        Assert.Contains("public const string TemplatesEditor = \"templates.editor\";", source);
        Assert.Contains("public const string AssetsBaseDisks = \"assets.base_disks\";", source);
        Assert.Contains("public const string AssetsSwitches = \"assets.switches\";", source);
        Assert.Contains("public const string DiagnosticsOverview = \"diagnostics.overview\";", source);
        Assert.Contains("public const string DiagnosticsLogs = \"diagnostics.logs\";", source);
        Assert.Contains("public const string SettingsGeneral = \"settings.general\";", source);
        Assert.Contains("public const string SettingsMachines = \"settings.machines\";", source);
        Assert.Contains("public string StartupRoute => ShellRouteKeys.MachinesOverview;", source);
    }

    [Fact]
    public void ShellViewModel_UsesDeterministicParentDefaults_AndFooterSettings()
    {
        var source = LoadShellViewModelSource();

        Assert.Contains("key: \"machines\"", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.MachinesOverview, \"Overview\"", source);
        Assert.Contains("key: \"deploy\"", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.DeployOnTheFly, \"Quick Deploy\"", source);
        Assert.Contains("key: \"templates\"", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.TemplatesLibrary, \"Library\"", source);
        Assert.Contains("key: \"assets\"", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.AssetsBaseDisks, \"Base Disks\"", source);
        Assert.Contains("key: \"diagnostics\"", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.DiagnosticsOverview, \"Overview\"", source);
        Assert.Contains("key: \"settings\"", source);
        Assert.Contains("isFooter: true", source);
        Assert.Contains("DefaultSubview = subviews[0];", source);
    }

    [Fact]
    public void ShellViewModel_DefinesExpectedEntitySet_AndContractLabels()
    {
        var source = LoadShellViewModelSource();

        Assert.Contains("displayName: \"Machines\"", source);
        Assert.Contains("displayName: \"Deploy\"", source);
        Assert.Contains("displayName: \"Templates\"", source);
        Assert.Contains("displayName: \"Assets\"", source);
        Assert.Contains("displayName: \"Diagnostics\"", source);
        Assert.Contains("displayName: \"Settings\"", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.DeployOnTheFly, \"Quick Deploy\"", source);
    }

    [Fact]
    public void MainWindow_WiresSettingsToFooter_AndUsesActiveNavSelectionSignal()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("GlobalNavigationView.FooterMenuItems.Add(parentItem);", source);
        Assert.Contains("GlobalNavigationView.SelectedItem = selectedNavigationItem;", source);
        Assert.DoesNotContain("Breadcrumb", source);
    }

    [Fact]
    public void MainWindow_ParentCapabilityClickRoutesToDefaultSubview()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("if (_shellViewModel.TryResolveCapability(key, out var capability))", source);
        Assert.Contains("NavigateToRoute(capability.DefaultSubview.RouteKey);", source);
        Assert.DoesNotContain("isCollapsedCompactPane", source);
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
