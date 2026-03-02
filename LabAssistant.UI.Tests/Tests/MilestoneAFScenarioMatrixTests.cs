using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneAFScenarioMatrixTests
{
    [Fact]
    public void ShellViewModel_DeployParentDefaultsToFromTemplateForAf()
    {
        var source = LoadShellViewModelSource();

        var fromTemplateIndex = source.IndexOf("new ShellSubview(ShellRouteKeys.DeployFromTemplate", StringComparison.Ordinal);
        var onTheFlyIndex = source.IndexOf("new ShellSubview(ShellRouteKeys.DeployOnTheFly", StringComparison.Ordinal);

        Assert.Contains("public const string DeployFromTemplate = \"deploy.from_template\";", source);
        Assert.True(fromTemplateIndex >= 0, "Deploy from-template route must exist.");
        Assert.True(onTheFlyIndex >= 0, "Deploy on-the-fly route must exist.");
        Assert.True(fromTemplateIndex < onTheFlyIndex, "Deploy from-template must be the default child ordering for AF.");
    }

    [Fact]
    public void MainWindow_DefinesDeployFromTemplateHostAndRouteState()
    {
        var xaml = LoadMainWindowXaml();
        var source = LoadMainWindowSource();

        Assert.NotNull(FindByName(xaml, "DeployFromTemplateViewHost"));
        Assert.Contains("private bool IsDeployFromTemplateActive =>", source);
        Assert.Contains("DeployFromTemplatePanel.Visibility = IsDeployFromTemplateActive ? Visibility.Visible : Visibility.Collapsed;", source);
    }

    [Fact]
    public void DeployFromTemplateView_DefinesRequiredAf2ScaffoldControls()
    {
        var xaml = LoadDeployFromTemplateViewXaml();

        Assert.NotNull(FindByName(xaml, "DeployTemplateSelectorComboBox"));
        Assert.NotNull(FindByName(xaml, "DeployReadinessSummaryPanel"));
        Assert.NotNull(FindByName(xaml, "DeployReadinessSummaryTextBlock"));
        Assert.NotNull(FindByName(xaml, "DeployResolveSuggestionsButton"));
        Assert.NotNull(FindByName(xaml, "DeployOpenTemplateEditorButton"));
        Assert.NotNull(FindByName(xaml, "DeployActionStatusTextBlock"));
    }

    [Fact]
    public void MainWindow_WiresDeployScaffoldActionsWithoutExecutionSemantics()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("WireDeployHandlers()", source);
        Assert.Contains("DeployResolveSuggestionsButton.Click += DeployResolveSuggestionsButton_Click;", source);
        Assert.Contains("DeployOpenTemplateEditorButton.Click += DeployOpenTemplateEditorButton_Click;", source);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.TemplatesEditor);", source);
        Assert.DoesNotContain("StartDeploymentAsync(", source);
    }

    private static XDocument LoadMainWindowXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static XDocument LoadDeployFromTemplateViewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployFromTemplateView.xaml");
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
