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
        var source = LoadDeployWorkspaceCompositionSource();
        var compositionSource = LoadDeployOnTheFlyWorkspaceCompositionSource();

        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyViewHost"));
        Assert.Contains("_onTheFlyWorkspaceComposition.ApplyShellState(_shellBridge.IsDeployOnTheFlyActive);", source);
        Assert.Contains("_view.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;", compositionSource);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.DeployOnTheFly);", source);
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
        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyVmEditorPanel"));
    }

    [Fact]
    public void MainWindow_WiresOnTheFlyReadinessCorrectionAndExecutionForAg3()
    {
        var source = LoadMainWindowSource();
        var controllerSource = LoadDeployOnTheFlyWorkspaceControllerSource();
        var compositionSource = LoadDeployOnTheFlyWorkspaceCompositionSource();
        var hostSource = LoadDeployOnTheFlyWorkspaceHostSource();
        var viewSource = LoadDeployOnTheFlyViewSource();

        Assert.Contains("private readonly DeployOnTheFlyWorkspaceController _deployOnTheFlyWorkspaceController;", source);
        Assert.Contains("private readonly DeployOnTheFlyWorkspaceComposition _deployOnTheFlyWorkspaceComposition;", source);
        Assert.Contains("new DeployOnTheFlyWorkspaceComposition(", source);
        Assert.Contains("new DeployOnTheFlyWorkspaceHost(", source);
        Assert.Contains("BuildOnTheFlyTemplate()", source);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.TemplatesEditor);", source);
        Assert.Contains("applyResolveSuggestionsAsync: ApplyDeployResolveSuggestionsAsync,", source);
        Assert.DoesNotContain("ResolveDeployOnTheFlySuggestionsAsync()", source);
        Assert.DoesNotContain("OpenDeployOnTheFlyTemplateEditorAsync()", source);
        Assert.DoesNotContain("AddDeployOnTheFlyVmEntry()", source);
        Assert.DoesNotContain("RemoveDeployOnTheFlyVmEntryAsync(VmTemplate? vmEntry)", source);
        Assert.DoesNotContain("UpdateDeployOnTheFlyResultRows()", source);
        Assert.DoesNotContain("UpdateDeployOnTheFlyIssueRows()", source);
        Assert.DoesNotContain("UpdateDeployOnTheFlyRowsFromSummary(DeploymentOutcomeSummary summary)", source);
        Assert.DoesNotContain("UpdateDeployOnTheFlyUi()", source);
        Assert.DoesNotContain("SetDeployOnTheFlyStatusText(string statusText)", source);
        Assert.DoesNotContain("UpdateDeployOnTheFlyVmEntryRows()", source);
        Assert.DoesNotContain("BuildDeployOnTheFlyEditorIssueSummaryText()", source);

        Assert.Contains("internal sealed class DeployOnTheFlyWorkspaceComposition", compositionSource);
        Assert.Contains("internal sealed class DeployOnTheFlyWorkspaceHost : IDeployOnTheFlyCompositionHost", hostSource);
        Assert.Contains("public Task OnStartRequestedAsync() => _onStartRequestedAsync();", hostSource);
        Assert.Contains("public void OnOpenResultsPanelRequested() => _onOpenResultsPanelRequested();", hostSource);
        Assert.Contains("_view.SetVmEntriesSource(_workspace.VmEntryRows);", compositionSource);
        Assert.Contains("_rightPanelView.SetResultRowsItemsSource(_workspace.ResultRows);", compositionSource);
        Assert.Contains("_ = _host.EnsureReferenceDataAsync(forceRefresh: false);", compositionSource);
        Assert.Contains("public void UpdateUi()", compositionSource);
        Assert.Contains("public void SetActionStatus(string statusText)", compositionSource);
        Assert.Contains("public Task EvaluateReadinessAsync(DeploymentPreflightMode mode) =>", compositionSource);
        Assert.Contains("CanStartDeploy: hasEntries && !hasBlockingFailures", compositionSource);
        Assert.Contains("public async Task ResolveSuggestionsAsync()", compositionSource);
        Assert.Contains("public async Task OpenTemplateEditorAsync()", compositionSource);
        Assert.Contains("public void AddVmEntry()", compositionSource);
        Assert.Contains("public async Task RemoveVmEntryAsync(VmTemplate? vmEntry)", compositionSource);
        Assert.Contains("public void RefreshResultRows()", compositionSource);
        Assert.Contains("public void RefreshIssueRows()", compositionSource);
        Assert.Contains("public void ApplyOutcomeSummary(DeploymentOutcomeSummary summary)", compositionSource);
        Assert.Contains("_view.EvaluateRequested += async (_, _) => await EvaluateReadinessAsync(DeploymentPreflightMode.Full);", compositionSource);
        Assert.Contains("_view.VmRemoveRequested += async vmEntry => await RemoveVmEntryAsync(vmEntry);", compositionSource);
        Assert.Contains("_view.AddVmRequested += (_, _) => AddVmEntry();", compositionSource);
        Assert.Contains("_view.RemoveSelectedVmRequested += async (_, _) => await RemoveVmEntryAsync(_workspace.SelectedVmEntry);", compositionSource);
        Assert.Contains("_view.ResolveSuggestionsRequested += async (_, _) => await ResolveSuggestionsAsync();", compositionSource);
        Assert.Contains("_view.OpenTemplateEditorRequested += async (_, _) => await OpenTemplateEditorAsync();", compositionSource);
        Assert.Contains("await EvaluateReadinessAsync(DeploymentPreflightMode.Full);", compositionSource);
        Assert.Contains("_view.StartDeployRequested += async (_, _) => await _host.OnStartRequestedAsync();", compositionSource);
        Assert.Contains("_view.OpenResultsPanelRequested += (_, _) => _host.OnOpenResultsPanelRequested();", compositionSource);
        Assert.Contains("var hasBlockingFailures = _workspace.HasBlockingFailures;", compositionSource);
        Assert.Contains("_view.ApplyWorkspaceState(new DeployOnTheFlyWorkspaceViewState(", compositionSource);
        Assert.Contains("GlobalIssuesBadgeText: $\"Blocking: {blockingIssueCount} | Warnings: {warningIssueCount}\"", compositionSource);
        Assert.Contains("private void UpdateVmEntryRows()", compositionSource);
        Assert.Contains("private string BuildEditorIssueSummaryText()", compositionSource);
        Assert.Contains("public void ApplyShellState(bool isActive)", compositionSource);
        Assert.Contains("public void ApplyResultsPanelState(bool isActive, bool showPanel, bool panelUnavailable)", compositionSource);
        Assert.DoesNotContain("DeployOnTheFlyVmEntriesListViewControl", compositionSource);
        Assert.DoesNotContain("DeployOnTheFlyStartButtonControl", compositionSource);

        Assert.Contains("internal sealed class DeployOnTheFlyWorkspaceController", controllerSource);
        Assert.Contains("await EvaluateReadinessAsync(DeploymentPreflightMode.Full);", controllerSource);
        Assert.Contains("PrepareDeployExecution(deployContext.MultiVmContext);", controllerSource);
        Assert.Contains("var summary = await _host.DeployAllAsync(deployContext.MultiVmContext);", controllerSource);
        Assert.Contains("ApplyDeploySummary(summary);", controllerSource);
        Assert.Contains("private void PrepareDeployExecution(MultiVmDeploymentContext context)", controllerSource);
        Assert.Contains("private void ApplyDeploySummary(DeploymentOutcomeSummary summary)", controllerSource);
        Assert.Contains("private void AttachProgressCallbacks(MultiVmDeploymentContext context)", controllerSource);
        Assert.DoesNotContain("_deployOnTheFlyWorkspace.ApplyOutcomeSummary(summary);", source);
        Assert.DoesNotContain("_deployOnTheFlyWorkspace.SetWorkflowState(\"Running\", 15, \"Preparing deployment...\");", source);
        Assert.Contains("void IDeployOnTheFlyWorkspaceControllerHost.EnqueueUiUpdate(Action updateAction)", source);

        Assert.Contains("public event EventHandler? VmDraftChanged;", viewSource);
        Assert.Contains("public void ApplyWorkspaceState(DeployOnTheFlyWorkspaceViewState state)", viewSource);
        Assert.DoesNotContain("public TextBox DeployOnTheFlyVmNameTextBoxControl =>", viewSource);
    }

    [Fact]
    public void DeployOnTheFlyRightPanelView_UsesCollapsedVmDetailsPatternForAg4()
    {
        var source = LoadDeployOnTheFlyRightPanelViewSource();

        Assert.Contains("IsExpanded=\"False\"", source);
        Assert.Contains("Text=\"Quick Deploy progress and VM step details.\"", source);
        Assert.Contains("x:Name=\"DeployOnTheFlyVmResultsListView\"", source);
    }

    [Fact]
    public void Ag2_PreservesFromTemplateRouteAndHost()
    {
        var xaml = LoadMainWindowXaml();
        var source = LoadDeployWorkspaceCompositionSource();

        Assert.NotNull(FindByName(xaml, "DeployFromTemplateViewHost"));
        Assert.Contains("_fromTemplateWorkspaceComposition.ApplyShellState(_shellBridge.IsDeployFromTemplateActive);", source);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.DeployFromTemplate);", source);
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

    private static string LoadDeployOnTheFlyViewSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployOnTheFlyView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployOnTheFlyRightPanelViewSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployOnTheFlyRightPanelView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
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

    private static string LoadDeployOnTheFlyWorkspaceControllerSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "QuickDeploy", "DeployOnTheFlyWorkspaceController.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployOnTheFlyWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "QuickDeploy", "DeployOnTheFlyWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployOnTheFlyWorkspaceHostSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "QuickDeploy", "DeployOnTheFlyWorkspaceHost.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
