using System.Xml.Linq;
using LabAssistant.WinUI.ViewModels;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class WinUIShellRegressionCoverageTests
{
    [Fact]
    public void ShellViewModel_ExposesDeterministicCapabilityDefaults_ForShellEntryRouting()
    {
        var shell = new ShellViewModel();

        Assert.Equal(ShellRouteKeys.MachinesOverview, shell.StartupRoute);

        AssertCapabilityDefault(shell, "machines", ShellRouteKeys.MachinesOverview);
        AssertCapabilityDefault(shell, "deploy", ShellRouteKeys.DeployOverview);
        AssertCapabilityDefault(shell, "templates", ShellRouteKeys.TemplatesLibrary);
        AssertCapabilityDefault(shell, "assets", ShellRouteKeys.AssetsOverview);
        AssertCapabilityDefault(shell, "diagnostics", ShellRouteKeys.DiagnosticsOverview);
        AssertCapabilityDefault(shell, "settings", ShellRouteKeys.SettingsMachines);
    }

    [Fact]
    public void ShellViewModel_ResolvesCanonicalRoutes_BackToExpectedCapabilityAndSubview()
    {
        var shell = new ShellViewModel();

        AssertRoute(shell, ShellRouteKeys.MachinesOverview, "machines", "Overview");
        AssertRoute(shell, ShellRouteKeys.DeployOnTheFly, "deploy", "Quick Deploy");
        AssertRoute(shell, ShellRouteKeys.DeployFromTemplate, "deploy", "From Template");
        AssertRoute(shell, ShellRouteKeys.TemplatesLibrary, "templates", "Library");
        AssertRoute(shell, ShellRouteKeys.TemplatesEditor, "templates", "Editor");
        AssertRoute(shell, ShellRouteKeys.AssetsBaseDisks, "assets", "Base Disks");
        AssertRoute(shell, ShellRouteKeys.AssetsSwitches, "assets", "Virtual Switches");
        AssertRoute(shell, ShellRouteKeys.DiagnosticsLogs, "diagnostics", "Logs");
        AssertRoute(shell, ShellRouteKeys.SettingsGeneral, "settings", "General");
    }

    [Fact]
    public void WinUI_DemoHardeningSurface_UsesLabeledPrimaryActions_AndRemovesScaffoldingCopy()
    {
        var mainWindow = LoadXaml("MainWindow.xaml");
        var machines = LoadXaml(Path.Combine("Views", "Machines", "MachinesOverviewView.xaml"));
        var baseDisks = LoadXaml(Path.Combine("Views", "Assets", "AssetsBaseDisksView.xaml"));
        var switches = LoadXaml(Path.Combine("Views", "Assets", "AssetsSwitchesView.xaml"));
        var deployFromTemplate = LoadXaml(Path.Combine("Views", "Deploy", "DeployFromTemplateView.xaml"));
        var quickDeploy = LoadXaml(Path.Combine("Views", "Deploy", "DeployOnTheFlyView.xaml"));

        Assert.Equal("Select a capability to continue.", FindByName(mainWindow, "NonMachinesPlaceholderTextBlock").Attribute("Text")?.Value);
        Assert.Equal(
            "Progress and results appear here when the active workflow uses the details panel.",
            mainWindow.Descendants().Single(element =>
                element.Name.LocalName == "TextBlock" &&
                string.Equals(element.Attribute("Text")?.Value, "Progress and results appear here when the active workflow uses the details panel.", StringComparison.Ordinal))
                .Attribute("Text")?.Value);
        Assert.Equal("Select a template", FindByName(deployFromTemplate, "DeployTemplateSelectorComboBox").Attribute("PlaceholderText")?.Value);
        Assert.Equal("Reload", FindByName(deployFromTemplate, "DeployReloadTemplatesButton").Attribute("Content")?.Value);
        Assert.Equal("Refresh", FindByName(machines, "RefreshMachinesButton").Attribute("Content")?.Value);
        Assert.Equal("Save Changes", FindByName(machines, "ApplyMachineEditsButton").Attribute("Content")?.Value);
        Assert.Equal("Delete VM", FindByName(machines, "DeleteVmButton").Attribute("Content")?.Value);
        Assert.Equal("Refresh", FindByName(baseDisks, "AssetsBaseDisksRefreshButton").Attribute("Content")?.Value);
        Assert.Equal("Import Disk", FindByName(baseDisks, "AssetsBaseDisksImportButton").Attribute("Content")?.Value);
        Assert.Equal("Remove from Catalog", FindByName(baseDisks, "AssetsBaseDisksRemoveButton").Attribute("Content")?.Value);
        Assert.Equal("Refresh", FindByName(switches, "AssetsSwitchesRefreshButton").Attribute("Content")?.Value);
        Assert.Equal("New Switch", FindByName(switches, "AssetsSwitchesCreateButton").Attribute("Content")?.Value);
        Assert.Equal("Delete Switch", FindByName(switches, "AssetsSwitchesDeleteButton").Attribute("Content")?.Value);
        Assert.Equal("Add VM", FindByName(quickDeploy, "DeployOnTheFlyAddVmButton").Attribute("Content")?.Value);
    }

    private static void AssertCapabilityDefault(ShellViewModel shell, string capabilityKey, string expectedRoute)
    {
        Assert.True(shell.TryResolveCapability(capabilityKey, out var capability));
        Assert.Equal(expectedRoute, capability.DefaultSubview.RouteKey);
    }

    private static void AssertRoute(ShellViewModel shell, string routeKey, string expectedCapabilityKey, string expectedSubviewDisplayName)
    {
        Assert.True(shell.TryResolveRoute(routeKey, out var capability, out var subview));
        Assert.Equal(expectedCapabilityKey, capability.Key);
        Assert.Equal(expectedSubviewDisplayName, subview.DisplayName);
    }

    private static XDocument LoadXaml(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", relativePath);
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
