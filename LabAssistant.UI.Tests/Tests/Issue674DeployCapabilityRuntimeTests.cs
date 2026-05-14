using System.IO;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class Issue674DeployCapabilityRuntimeTests
{
    [Fact]
    public void MainWindow_UsesSingleDeployCapabilityRuntimeBoundary()
    {
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains("private readonly DeployCapabilityRuntime _deployCapabilityRuntime;", mainWindowSource);
        Assert.Contains("_deployCapabilityRuntime = CreateDeployCapabilityRuntime();", mainWindowSource);
        Assert.Contains("private DeployCapabilityRuntime CreateDeployCapabilityRuntime()", mainWindowSource);
        Assert.Contains("new DeployCapabilityShellBridge(", mainWindowSource);
        Assert.Contains("private DeployTemplatesShellAdapter CreateDeployTemplatesShellAdapter()", mainWindowSource);
        Assert.Contains("return new DeployTemplatesShellAdapter(", mainWindowSource);
        Assert.Contains("var quickDeployLane = new DeployOnTheFlyWorkspaceOwner(", mainWindowSource);
        Assert.Contains("var fromTemplateLane = new DeployFromTemplateWorkspaceComposition(", mainWindowSource);
        Assert.Contains("workspaceComposition = new DeployWorkspaceComposition(", mainWindowSource);
        Assert.Contains("var resultsPanelCoordinator = new DeployResultsPanelCoordinator(", mainWindowSource);
        Assert.Contains("_deployCapabilityRuntime.ApplyShellState();", mainWindowSource);
        Assert.Contains("_deployCapabilityRuntime.ResetRightPanelBehavior();", mainWindowSource);
        Assert.Contains("var deployCapabilityRuntime = _deployCapabilityRuntime;", mainWindowSource);
        Assert.Contains("RightPanelTitleTextBlock.Text = deployCapabilityRuntime.GetRightPanelTitleText();", mainWindowSource);
        Assert.Contains("deployCapabilityRuntime.ApplyRightPanelState(showPanel, _isShellRightPanelInCompactFallback);", mainWindowSource);

        Assert.DoesNotContain("private readonly DeployWorkspaceComposition _deployWorkspaceComposition;", mainWindowSource);
        Assert.DoesNotContain("private readonly DeployReferenceDataService _deployReferenceDataService;", mainWindowSource);
        Assert.DoesNotContain("private readonly DeployOnTheFlyWorkspaceOwner _deployOnTheFlyWorkspaceOwner;", mainWindowSource);
        Assert.DoesNotContain("private readonly DeployFromTemplateWorkspaceComposition _deployFromTemplateWorkspaceComposition;", mainWindowSource);
        Assert.DoesNotContain("private readonly DeployResultsPanelCoordinator _deployResultsPanelCoordinator;", mainWindowSource);
        Assert.DoesNotContain("DeployCapabilityBootstrap.Bootstrap(", mainWindowSource);
        Assert.DoesNotContain("DeployCapabilityBootstrapContext", mainWindowSource);
        Assert.DoesNotContain("private DeployCapabilityShellViewHosts CreateDeployCapabilityShellViewHosts()", mainWindowSource);
        Assert.DoesNotContain("private DeployQuickDeployShellViewHosts CreateDeployQuickDeployShellViewHosts()", mainWindowSource);
        Assert.DoesNotContain("private DeployFromTemplateShellViewHosts CreateDeployFromTemplateShellViewHosts()", mainWindowSource);
        Assert.DoesNotContain("private DeployCapabilityShellNavigationHosts CreateDeployCapabilityShellNavigationHosts()", mainWindowSource);
        Assert.DoesNotContain("if (IsDeployFromTemplateActive)", mainWindowSource);
        Assert.DoesNotContain("_ = _deployFromTemplateWorkspaceComposition.EnsureTemplatesLoadedAsync(forceRefresh: false);", mainWindowSource);
    }

    [Fact]
    public void MainWindow_BuildsDeployThroughExplicitNamedAssemblySteps()
    {
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains("var overviewView = DeployOverviewViewHost;", mainWindowSource);
        Assert.Contains("var localNavigationHost = DeployLocalNavigationPanel;", mainWindowSource);
        Assert.Contains("var quickDeployView = DeployOnTheFlyViewHost;", mainWindowSource);
        Assert.Contains("var fromTemplateView = DeployFromTemplateViewHost;", mainWindowSource);
        Assert.Contains("var referenceDataService = new DeployReferenceDataService(", mainWindowSource);
        Assert.Contains("var resolveSuggestionsService = new DeployResolveSuggestionsService();", mainWindowSource);
        Assert.Contains("var templateEditorLauncher = new DeployTemplateEditorLauncher(templatesShellAdapter);", mainWindowSource);
        Assert.Contains("DeployWorkspaceComposition? workspaceComposition = null;", mainWindowSource);
        Assert.Contains("Action refreshSharedUiState = () => workspaceComposition?.RefreshSharedUiState();", mainWindowSource);
        Assert.Contains("new DeployFromTemplateWorkspaceHost(", mainWindowSource);
        Assert.Contains("shellBridge.AttachProgressCallbacks,", mainWindowSource);
        Assert.Contains("shellBridge.RequestResultsPanelToggle));", mainWindowSource);
        Assert.Contains("() => _deployCapabilityRuntime?.RefreshTemplatesLoadingState());", mainWindowSource);
        Assert.Contains("items => _deployCapabilityRuntime?.ReconcileTemplateSelection(items));", mainWindowSource);
    }

    [Fact]
    public void DeployCapabilityRuntime_StaysAtCapabilityBoundary()
    {
        var runtimeSource = LoadDeployCapabilityRuntimeSource();

        Assert.Contains("internal sealed class DeployCapabilityRuntime", runtimeSource);
        Assert.Contains("private readonly IDeployFromTemplateLane _fromTemplateLane;", runtimeSource);
        Assert.Contains("public void ApplyShellState()", runtimeSource);
        Assert.Contains("_workspaceComposition.ApplyShellState();", runtimeSource);
        Assert.Contains("EnsureFromTemplateLaneLoadedForActiveRoute();", runtimeSource);
        Assert.Contains("_ = _fromTemplateLane.EnsureTemplatesLoadedAsync(forceRefresh: false);", runtimeSource);
        Assert.Contains("public void ResetRightPanelBehavior()", runtimeSource);
        Assert.Contains("public bool ShouldAutoOpenRightPanel()", runtimeSource);
        Assert.Contains("public void RefreshTemplatesLoadingState()", runtimeSource);
        Assert.Contains("_fromTemplateLane.RefreshUi();", runtimeSource);
        Assert.Contains("public void ReconcileTemplateSelection(IReadOnlyList<TemplateLibraryItem> items)", runtimeSource);
        Assert.Contains("_fromTemplateLane.ReconcileSelection(items);", runtimeSource);
        Assert.Contains("_workspaceComposition.RefreshSharedUiState();", runtimeSource);
        Assert.DoesNotContain("DeployOnTheFlyWorkspaceOwner", runtimeSource);
        Assert.DoesNotContain("ShellRightPanelColumn", runtimeSource);
        Assert.DoesNotContain("InsightsPanel.Visibility", runtimeSource);
    }

    [Fact]
    public void DeployResultsPanelCoordinator_RemainsSharedAndLaneInterfaceBased()
    {
        var coordinatorSource = LoadDeployResultsPanelCoordinatorSource();

        Assert.Contains("internal sealed class DeployResultsPanelCoordinator", coordinatorSource);
        Assert.Contains("private readonly IDeployQuickDeployLane _quickDeployLane;", coordinatorSource);
        Assert.Contains("private readonly IDeployFromTemplateLane _fromTemplateLane;", coordinatorSource);
        Assert.Contains("_fromTemplateLane.ApplyResultsPanelState(_isDeployFromTemplateActive(), showPanel, panelUnavailable);", coordinatorSource);
        Assert.Contains("_quickDeployLane.ApplyResultsPanelState(_isDeployOnTheFlyActive(), showPanel, panelUnavailable);", coordinatorSource);
        Assert.DoesNotContain("private readonly DeployOnTheFlyWorkspaceOwner _onTheFlyWorkspaceOwner;", coordinatorSource);
        Assert.DoesNotContain("private readonly DeployFromTemplateWorkspaceComposition _fromTemplateWorkspaceComposition;", coordinatorSource);
    }

    [Fact]
    public void MainWindow_RespectsExplicitDeployRightPanelToggleAgainstAutoOpen()
    {
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains("private bool _isDeployRightPanelAutoOpenSuppressed;", mainWindowSource);
        Assert.Contains("var deployCapabilityRuntime = _deployCapabilityRuntime;", mainWindowSource);
        Assert.Contains("if (deployCapabilityRuntime is null)", mainWindowSource);
        Assert.Contains("var shouldAutoOpenDeployRightPanel = CanActiveCapabilityOwnRightPanel() && deployCapabilityRuntime.ShouldAutoOpenRightPanel();", mainWindowSource);
        Assert.Contains("if (!shouldAutoOpenDeployRightPanel)", mainWindowSource);
        Assert.Contains("_isDeployRightPanelAutoOpenSuppressed = false;", mainWindowSource);
        Assert.Contains("if (shouldAutoOpenDeployRightPanel && !_isDeployRightPanelAutoOpenSuppressed)", mainWindowSource);
        Assert.Contains("private void SetRightPanelOpenFromUserToggle(bool isOpen)", mainWindowSource);
        Assert.Contains("_isDeployRightPanelAutoOpenSuppressed = !isOpen &&", mainWindowSource);
        Assert.Contains("deployCapabilityRuntime.ShouldAutoOpenRightPanel();", mainWindowSource);
        Assert.Contains("SetRightPanelOpenFromUserToggle(!_isShellRightPanelOpen);", mainWindowSource);
        Assert.Contains("SetRightPanelOpenFromUserToggle(false);", mainWindowSource);
        Assert.Contains("_shellRightPanelOwnerCapabilityKey = ResolveRightPanelOwnerCapabilityKey(_activeCapability.Key);", mainWindowSource);
    }

    [Fact]
    public void DeployLaneResultsPanelLaunchers_HaveUnobstructedGridRows()
    {
        var quickDeployXaml = NormalizeLineEndings(LoadDeployOnTheFlyViewXamlSource());
        var fromTemplateXaml = NormalizeLineEndings(LoadDeployFromTemplateViewXamlSource());

        Assert.Contains("<Grid.RowDefinitions>", quickDeployXaml);
        Assert.Contains("x:Name=\"DeployOnTheFlyOpenResultsPanelButton\"", quickDeployXaml);
        Assert.Contains("x:Name=\"DeployOnTheFlyReadinessSummaryTextBlock\"\n                        Grid.Row=\"1\"", quickDeployXaml);
        Assert.Contains("x:Name=\"DeployOnTheFlyGlobalIssuesBadgeTextBlock\"\n                        Grid.Row=\"2\"", quickDeployXaml);

        Assert.Contains("<Grid.RowDefinitions>", fromTemplateXaml);
        Assert.Contains("x:Name=\"DeployOpenResultsPanelButton\"", fromTemplateXaml);
        Assert.Contains("x:Name=\"DeployReadinessSummaryTextBlock\"\n                                Grid.Row=\"1\"", fromTemplateXaml);
        Assert.Contains("x:Name=\"DeployGlobalIssuesBadgeTextBlock\"\n                                Grid.Row=\"2\"", fromTemplateXaml);
    }

    private static string LoadMainWindowSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml.cs");
        return File.ReadAllText(path);
    }

    private static string LoadDeployCapabilityRuntimeSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployCapabilityRuntime.cs");
        return File.ReadAllText(path);
    }

    private static string LoadDeployResultsPanelCoordinatorSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "ResultsPanel", "DeployResultsPanelCoordinator.cs");
        return File.ReadAllText(path);
    }

    private static string LoadDeployOnTheFlyViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployOnTheFlyView.xaml");
        return File.ReadAllText(path);
    }

    private static string LoadDeployFromTemplateViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployFromTemplateView.xaml");
        return File.ReadAllText(path);
    }

    private static string NormalizeLineEndings(string source) => source.Replace("\r\n", "\n", StringComparison.Ordinal);
}
