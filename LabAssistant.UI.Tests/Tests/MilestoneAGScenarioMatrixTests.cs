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
    public void DeployOnTheFlyView_DefinesRequiredAgScaffoldAndBehaviorRegions()
    {
        var xaml = LoadDeployOnTheFlyViewXaml();

        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyVmEntriesPanel"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyVmEntriesListView"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyAddVmButton"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyRemoveVmButton"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyVmNameTextBox"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyVmMemoryTextBox"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyVmCpuTextBox"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyVmVhdxCatalogComboBox"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyVmSwitchComboBox"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyVmSwitchGuidanceTextBlock"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyVmVhdxGuidanceTextBlock"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyApplyVmChangesButton"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyReadinessSummaryPanel"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyOverallStateTextBlock"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyProgressBar"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyProgressSummaryTextBlock"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyGlobalIssuesBadgeTextBlock"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyReadinessSummaryTextBlock"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyEvaluateButton"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyResolveSuggestionsButton"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyOpenTemplateEditorButton"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyStartButton"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyStatusTextBlock"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyVmResultsListView"));
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyVmEditorPanel"));
    }

    [Fact]
    public void MainWindow_WiresOnTheFlyReadinessCorrectionAndExecutionForAg3()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("DeployOnTheFlyEvaluateButton.Click += DeployOnTheFlyEvaluateButton_Click;", source);
        Assert.Contains("DeployOnTheFlyResolveSuggestionsButton.Click += DeployOnTheFlyResolveSuggestionsButton_Click;", source);
        Assert.Contains("DeployOnTheFlyOpenTemplateEditorButton.Click += DeployOnTheFlyOpenTemplateEditorButton_Click;", source);
        Assert.Contains("DeployOnTheFlyStartButton.Click += DeployOnTheFlyStartButton_Click;", source);
        Assert.Contains("await EvaluateDeployOnTheFlyReadinessAsync(DeploymentPreflightMode.Quick);", source);
        Assert.Contains("await EvaluateDeployOnTheFlyReadinessAsync(DeploymentPreflightMode.Full);", source);
        Assert.Contains("DeployOnTheFlyStartButton.IsEnabled = hasEntries && !hasBlockingFailures", source);
        Assert.Contains("await _deploymentCoordinator.DeployAllAsync(deployContext.MultiVmContext);", source);
        Assert.Contains("BuildOnTheFlyTemplate()", source);
        Assert.Contains("_ = EnsureDeployOnTheFlyReferenceDataAsync(forceRefresh: false);", source);
        Assert.Contains("DeployOnTheFlyVmSwitchComboBox.SelectionChanged += DeployOnTheFlyVmSwitchComboBox_SelectionChanged;", source);
        Assert.Contains("DeployOnTheFlyVmVhdxCatalogComboBox.SelectionChanged += DeployOnTheFlyVmVhdxCatalogComboBox_SelectionChanged;", source);
        Assert.Contains("DeployOnTheFlyVmResultsListView.ItemsSource = _deployOnTheFlyVmResultRows;", source);
        Assert.Contains("AttachDeployOnTheFlyProgressCallbacks(deployContext.MultiVmContext);", source);
        Assert.Contains("UpdateDeployOnTheFlyRowsFromSummary(summary);", source);
        Assert.Contains("DeployOnTheFlyProgressBar.Value = _deployOnTheFlyProgressPercent;", source);
        Assert.Contains("DeployOnTheFlyGlobalIssuesBadgeTextBlock.Text = $\"Blocking: {blockingIssueCount} | Warnings: {warningIssueCount}\";", source);
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
