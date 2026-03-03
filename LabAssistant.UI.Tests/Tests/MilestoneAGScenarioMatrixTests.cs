using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneAGScenarioMatrixTests
{
    [Fact]
    public void ShellViewModel_ContainsDeployOnTheFlyRouteForAg()
    {
        var source = LoadShellViewModelSource();

        Assert.Contains("public const string DeployOnTheFly = \"deploy.on_the_fly\";", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.DeployOnTheFly", source);
    }

    [Fact]
    public void MainWindow_DefinesDeployOnTheFlyHostAndVisibilityWiring()
    {
        var xaml = LoadMainWindowXaml();
        var source = LoadMainWindowSource();

        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyViewHost"));
        Assert.Contains("private bool IsDeployOnTheFlyActive =>", source);
        Assert.Contains("DeployOnTheFlyPanel.Visibility = IsDeployOnTheFlyActive ? Visibility.Visible : Visibility.Collapsed;", source);
    }

    [Fact]
    public void DeployOnTheFlyView_DefinesRequiredAg2ScaffoldRegions()
    {
        var xaml = LoadDeployOnTheFlyViewXaml();

        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyVmEntriesPanel"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyVmEntriesListView"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyReadinessSummaryPanel"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyReadinessSummaryTextBlock"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyEvaluateButton"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyResolveSuggestionsButton"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyOpenTemplateEditorButton"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyStartButton"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyStatusTextBlock"));
    }

    [Fact]
    public void Ag2_RemainsScaffoldOnlyWithoutOnTheFlyBehaviorWiring()
    {
        var source = LoadMainWindowSource();

        Assert.DoesNotContain("DeployOnTheFlyEvaluateButton.Click +=", source);
        Assert.DoesNotContain("DeployOnTheFlyStartButton.Click +=", source);
        Assert.DoesNotContain("EvaluateDeployOnTheFlyReadinessAsync", source);
        Assert.DoesNotContain("DeployOnTheFlyStartButton_Click", source);
    }

    [Fact]
    public void Ag2_PreservesFromTemplateRouteAndHost()
    {
        var xaml = LoadMainWindowXaml();
        var source = LoadMainWindowSource();

        Assert.NotNull(FindByName(xaml, "DeployFromTemplateViewHost"));
        Assert.Contains("private bool IsDeployFromTemplateActive =>", source);
        Assert.Contains("DeployFromTemplatePanel.Visibility = IsDeployFromTemplateActive ? Visibility.Visible : Visibility.Collapsed;", source);
    }

    private static XDocument LoadMainWindowXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static XDocument LoadDeployOnTheFlyViewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployOnTheFlyView.xaml");
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
