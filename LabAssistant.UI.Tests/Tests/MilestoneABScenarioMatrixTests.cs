using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneABScenarioMatrixTests
{
    [Fact]
    public void MainWindow_HostsExtractedMachinesAndDiagnosticsViews()
    {
        var xaml = LoadMainWindowXaml();

        var machinesHost = FindByName(xaml, "MachinesOverviewViewHost");
        var diagnosticsHost = FindByName(xaml, "DiagnosticsLogsViewHost");

        Assert.Equal("MachinesOverviewView", machinesHost.Name.LocalName);
        Assert.Equal("DiagnosticsLogsView", diagnosticsHost.Name.LocalName);
    }

    [Fact]
    public void MainWindow_UsesHostLevelVisibilityDefaults_ForExtractedViews()
    {
        var xaml = LoadMainWindowXaml();

        var machinesHost = FindByName(xaml, "MachinesOverviewViewHost");
        var diagnosticsHost = FindByName(xaml, "DiagnosticsLogsViewHost");

        Assert.Equal("Collapsed", machinesHost.Attribute("Visibility")?.Value);
        Assert.Equal("Collapsed", diagnosticsHost.Attribute("Visibility")?.Value);
    }

    [Fact]
    public void MainWindow_KeepsDrawerInContentRow_BelowTopBar()
    {
        var xaml = LoadMainWindowXaml();
        var drawer = FindByName(xaml, "CapabilityDrawer");
        var scrim = FindByName(xaml, "DrawerScrim");

        Assert.Equal("1", GetAttributeValue(drawer, "Grid.Row"));
        Assert.Equal("1", GetAttributeValue(scrim, "Grid.Row"));
        Assert.Equal("280", drawer.Attribute("Width")?.Value);
    }

    [Fact]
    public void DiagnosticsLogs_FilterActionsUseWrappedTwoRowGrid()
    {
        var xaml = LoadDiagnosticsLogsViewXaml();

        Assert.Equal("0", GetAttributeValue(FindByName(xaml, "LogFilterOperationIdTextBox"), "Grid.Row"));
        Assert.Equal("0", GetAttributeValue(FindByName(xaml, "LogFilterLevelTextBox"), "Grid.Row"));
        Assert.Equal("0", GetAttributeValue(FindByName(xaml, "LogFilterEventTextBox"), "Grid.Row"));
        Assert.Equal("0", GetAttributeValue(FindByName(xaml, "LogFilterTextSearchTextBox"), "Grid.Row"));

        var applyButton = FindByName(xaml, "ApplyLogFiltersButton");
        var clearButton = FindByName(xaml, "ClearLogFiltersButton");
        var reloadButton = FindByName(xaml, "ReloadLogsButton");
        var openRawButton = FindByName(xaml, "OpenRawJsonlButton");

        Assert.Equal("1", GetAttributeValue(applyButton, "Grid.Row"));

        var trailingActionsHost = clearButton.Parent;
        Assert.NotNull(trailingActionsHost);
        Assert.Equal("1", GetAttributeValue(trailingActionsHost!, "Grid.Row"));
        Assert.Equal("3", GetAttributeValue(trailingActionsHost, "Grid.Column"));
        Assert.Same(trailingActionsHost, reloadButton.Parent);
        Assert.Same(trailingActionsHost, openRawButton.Parent);
        Assert.Equal(3, trailingActionsHost.Elements().Count(element => element.Name.LocalName == "Button"));
    }

    [Fact]
    public void DiagnosticsLogs_DetailsPaneHasBoundedInternalScrollOwners()
    {
        var xaml = LoadDiagnosticsLogsViewXaml();

        var envelopeScroll = xaml
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "ScrollViewer" &&
                element.Attribute("MaxHeight")?.Value == "88");
        Assert.Equal("Auto", envelopeScroll.Attribute("VerticalScrollBarVisibility")?.Value);

        var contextTextBox = FindByName(xaml, "SelectedLogContextTextBox");
        Assert.Equal("Auto", GetAttributeValue(contextTextBox, "ScrollViewer.VerticalScrollBarVisibility"));
        Assert.Equal("Auto", GetAttributeValue(contextTextBox, "ScrollViewer.HorizontalScrollBarVisibility"));
        Assert.Equal("2", GetAttributeValue(contextTextBox, "Grid.Row"));
    }

    private static XDocument LoadMainWindowXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static XDocument LoadDiagnosticsLogsViewXaml()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "LabAssistant.WinUI",
            "Views",
            "Diagnostics",
            "DiagnosticsLogsView.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }

    private static string? GetAttributeValue(XElement element, string attributeName)
    {
        return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == attributeName)?.Value;
    }
}
