using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneAHScenarioMatrixTests
{
    [Fact]
    public void DeployTimeline_DefinesCanonicalStepStateModel()
    {
        var stateSource = LoadTimelineStateSource();
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains("public enum DeployTimelineStepState", stateSource);
        Assert.Contains("Pending", stateSource);
        Assert.Contains("Running", stateSource);
        Assert.Contains("Succeeded", stateSource);
        Assert.Contains("Failed", stateSource);
        Assert.Contains("Skipped", stateSource);
        Assert.Contains("private sealed record DeployTimelineStepDefinition", mainWindowSource);
        Assert.Contains("private sealed record DeployTimelineStepRow", mainWindowSource);
    }

    [Fact]
    public void DeployTimeline_UsesStateDrivenIconCatalog()
    {
        var iconCatalogSource = LoadTimelineIconCatalogSource();
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains("GlyphByState", iconCatalogSource);
        Assert.Contains("[DeployTimelineStepState.Pending]", iconCatalogSource);
        Assert.Contains("[DeployTimelineStepState.Running]", iconCatalogSource);
        Assert.Contains("[DeployTimelineStepState.Succeeded]", iconCatalogSource);
        Assert.Contains("[DeployTimelineStepState.Failed]", iconCatalogSource);
        Assert.Contains("[DeployTimelineStepState.Skipped]", iconCatalogSource);
        Assert.Contains("DeployTimelineIconCatalog.GetGlyph(State)", mainWindowSource);
    }

    [Fact]
    public void DeployTimeline_HidesSkippedRowsAndUsesDeterministicTransitions()
    {
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains(".Where(step => step.State != DeployTimelineStepState.Skipped)", mainWindowSource);
        Assert.Contains("_stepStates[label] == DeployTimelineStepState.Running", mainWindowSource);
        Assert.Contains("_stepStates[label] = DeployTimelineStepState.Succeeded;", mainWindowSource);
        Assert.Contains("public void MarkCompleted(string status, string summary)", mainWindowSource);
    }

    [Fact]
    public void DeployViews_RenderSingleLabelTimelineRowsWithStateIndicators()
    {
        var fromTemplateXaml = LoadDeployFromTemplateViewXaml();
        var onTheFlyXaml = LoadDeployOnTheFlyViewXaml();

        Assert.NotNull(FindByName(fromTemplateXaml, "DeployVmResultsListView"));
        Assert.NotNull(FindByName(onTheFlyXaml, "DeployOnTheFlyVmResultsListView"));

        var fromTemplateSource = LoadDeployFromTemplateViewSource();
        var onTheFlySource = LoadDeployOnTheFlyViewSource();

        Assert.Contains("ItemsSource=\"{Binding TimelineSteps}\"", fromTemplateSource);
        Assert.Contains("ItemsSource=\"{Binding TimelineSteps}\"", onTheFlySource);
        Assert.Contains("Text=\"{Binding Label}\"", fromTemplateSource);
        Assert.Contains("Text=\"{Binding Label}\"", onTheFlySource);
        Assert.Contains("IsActive=\"{Binding IsRunning}\"", fromTemplateSource);
        Assert.Contains("IsActive=\"{Binding IsRunning}\"", onTheFlySource);
    }

    [Fact]
    public void AhTimelineChanges_DoNotRegressAgAndAfRoutes()
    {
        var shellSource = LoadShellViewModelSource();
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains("public const string DeployFromTemplate = \"deploy.from_template\";", shellSource);
        Assert.Contains("public const string DeployOnTheFly = \"deploy.on_the_fly\";", shellSource);
        Assert.Contains("private bool IsDeployFromTemplateActive =>", mainWindowSource);
        Assert.Contains("private bool IsDeployOnTheFlyActive =>", mainWindowSource);
    }

    private static string LoadTimelineStateSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Models", "Deploy", "DeployTimelineStepState.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTimelineIconCatalogSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Theming", "DeployTimelineIconCatalog.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadMainWindowSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XDocument LoadDeployFromTemplateViewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployFromTemplateView.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static string LoadDeployFromTemplateViewSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployFromTemplateView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XDocument LoadDeployOnTheFlyViewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployOnTheFlyView.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static string LoadDeployOnTheFlyViewSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployOnTheFlyView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadShellViewModelSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "ShellViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
