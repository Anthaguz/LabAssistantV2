using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneAFScenarioMatrixTests
{
    [Fact]
    public void ShellViewModel_DeployParentDefaultsToOverview_WithApprovedSubviewOrder()
    {
        var source = LoadShellViewModelSource();

        var overviewIndex = source.IndexOf("new ShellSubview(ShellRouteKeys.DeployOverview", StringComparison.Ordinal);
        var onTheFlyIndex = source.IndexOf("new ShellSubview(ShellRouteKeys.DeployOnTheFly", StringComparison.Ordinal);
        var fromTemplateIndex = source.IndexOf("new ShellSubview(ShellRouteKeys.DeployFromTemplate", StringComparison.Ordinal);

        Assert.Contains("public const string DeployOverview = \"deploy.overview\";", source);
        Assert.Contains("public const string DeployFromTemplate = \"deploy.from_template\";", source);
        Assert.True(overviewIndex >= 0, "Deploy overview route must exist.");
        Assert.True(fromTemplateIndex >= 0, "Deploy from-template route must exist.");
        Assert.True(onTheFlyIndex >= 0, "Deploy on-the-fly route must exist.");
        Assert.True(overviewIndex < onTheFlyIndex, "Deploy overview must be the first local subview.");
        Assert.True(onTheFlyIndex < fromTemplateIndex, "Quick Deploy must remain ahead of From Template in local ordering.");
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
    public void DeployFromTemplateView_AndRightPanel_DefineRequiredAf4CompactResultsControls()
    {
        var xaml = LoadDeployFromTemplateViewXaml();
        var rightPanelXaml = LoadDeployFromTemplateRightPanelViewXaml();

        Assert.NotNull(FindByName(xaml, "DeployTemplateSelectorComboBox"));
        Assert.NotNull(FindByName(xaml, "DeployReadinessSummaryPanel"));
        Assert.NotNull(FindByName(xaml, "DeployOverallStateTextBlock"));
        Assert.NotNull(FindByName(xaml, "DeployProgressBar"));
        Assert.NotNull(FindByName(xaml, "DeployProgressSummaryTextBlock"));
        Assert.NotNull(FindByName(xaml, "DeployGlobalIssuesBadgeTextBlock"));
        Assert.NotNull(FindByName(xaml, "DeployReadinessSummaryTextBlock"));
        Assert.NotNull(FindByName(xaml, "DeployEvaluateReadinessButton"));
        Assert.NotNull(FindByName(xaml, "DeployResolveSuggestionsButton"));
        Assert.NotNull(FindByName(xaml, "DeployOpenTemplateEditorButton"));
        Assert.NotNull(FindByName(xaml, "DeployStartButton"));
        Assert.NotNull(FindByName(xaml, "DeployActionStatusTextBlock"));
        var globalIssuesExpander = FindByName(rightPanelXaml, "DeployGlobalIssuesExpander");
        Assert.Equal("False", globalIssuesExpander.Attribute("IsExpanded")?.Value);
        Assert.NotNull(FindByName(rightPanelXaml, "DeployGlobalIssuesListView"));
        Assert.NotNull(FindByName(rightPanelXaml, "DeployVmResultsListView"));
    }

    [Fact]
    public void MainWindow_WiresDeployReadinessAndExecutionFlowForAf3()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("WireDeployHandlers()", source);
        Assert.Contains("DeployEvaluateReadinessButton.Click += DeployEvaluateReadinessButton_Click;", source);
        Assert.Contains("DeployStartButton.Click += DeployStartButton_Click;", source);
        Assert.Contains("DeployTemplateSelectorComboBox.SelectionChanged += DeployTemplateSelectorComboBox_SelectionChanged;", source);
        Assert.Contains("await EvaluateDeployReadinessAsync(DeploymentPreflightMode.Quick);", source);
        Assert.Contains("await EvaluateDeployReadinessAsync(DeploymentPreflightMode.Full);", source);
        Assert.Contains("await _deploymentCoordinator.DeployAllAsync(deployContext.MultiVmContext);", source);
        Assert.Contains("Deploy blocked by readiness failures. Resolve blocking items first.", source);
        Assert.Contains("DeployResolveSuggestionsButton.Click += DeployResolveSuggestionsButton_Click;", source);
        Assert.Contains("DeployOpenTemplateEditorButton.Click += DeployOpenTemplateEditorButton_Click;", source);
        Assert.Contains("await OpenTemplateInEditorAsync(_selectedDeployTemplateLibraryItem, fromDeploy: true);", source);
        Assert.Contains("private static DeployDiskResolution ResolveDeployDiskIdentity", source);
        Assert.Contains("private static DeploySwitchResolution ResolveDeploySwitches", source);
        Assert.Contains("private void UpdateDeployRowsFromSummary(DeploymentOutcomeSummary summary)", source);
        Assert.Contains("DeployProgressBar.Value = _deployProgressPercent;", source);
        Assert.Contains("DeployGlobalIssuesBadgeTextBlock.Text = $\"Issues: {_deployIssueRows.Count}\";", source);
        Assert.Contains("Switch mapping partial/missing", source);
        Assert.Contains("Disk identity conflict detected.", source);
        Assert.Contains("DeployStartButton.IsEnabled = hasTemplate && !hasBlockingFailures", source);
    }

    [Fact]
    public void DeployFromTemplateRightPanelView_PerVmAndGlobalDetailsDefaultToCollapsed()
    {
        var xaml = LoadDeployFromTemplateRightPanelViewXaml();
        var expanders = xaml.Descendants().Where(element => element.Name.LocalName == "Expander").ToList();

        Assert.True(expanders.Count >= 2, "Expected global issues expander and per-VM row expander.");
        Assert.True(expanders.Count(element => string.Equals(element.Attribute("IsExpanded")?.Value, "False", StringComparison.Ordinal)) >= 2);
    }

    [Fact]
    public void AfDeployChanges_PreserveTemplatesRouteAndGlobalNavigationContractSignals()
    {
        var mainWindowSource = LoadMainWindowSource();
        var shellSource = LoadShellViewModelSource();

        Assert.Contains("NavigateToRoute(ShellRouteKeys.TemplatesEditor);", mainWindowSource);
        Assert.Contains("private void GlobalNavigationView_ItemInvoked", mainWindowSource);
        Assert.Contains("public const string TemplatesLibrary = \"templates.library\";", shellSource);
        Assert.Contains("public const string TemplatesEditor = \"templates.editor\";", shellSource);
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

    private static XDocument LoadDeployFromTemplateRightPanelViewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployFromTemplateRightPanelView.xaml");
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
