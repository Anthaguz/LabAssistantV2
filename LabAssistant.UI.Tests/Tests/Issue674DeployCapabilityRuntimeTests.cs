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
        Assert.Contains("IServiceProvider services = App.Services;", mainWindowSource);
        Assert.Contains("_deployCapabilityRuntime = DeployCapabilityBootstrap.Bootstrap(services, new DeployCapabilityBootstrapContext", mainWindowSource);
        Assert.Contains("new DeployCapabilityShellBridge(", mainWindowSource);
        Assert.Contains("ShellViewHosts = new DeployCapabilityShellViewHosts", mainWindowSource);
        Assert.Contains("Templates = new DeployTemplatesShellAdapter(", mainWindowSource);
        Assert.Contains("_deployCapabilityRuntime.ApplyShellState();", mainWindowSource);
        Assert.Contains("_deployCapabilityRuntime.ResetRightPanelBehavior();", mainWindowSource);
        Assert.Contains("_deployCapabilityRuntime.GetRightPanelTitleText();", mainWindowSource);
        Assert.Contains("_deployCapabilityRuntime.ApplyRightPanelState(showPanel, _isShellRightPanelInCompactFallback);", mainWindowSource);
        Assert.Contains("_deployCapabilityRuntime.RefreshTemplatesLoadingState();", mainWindowSource);
        Assert.Contains("_deployCapabilityRuntime.ReconcileTemplateSelection(items);", mainWindowSource);

        Assert.DoesNotContain("private readonly DeployWorkspaceComposition _deployWorkspaceComposition;", mainWindowSource);
        Assert.DoesNotContain("private readonly DeployReferenceDataService _deployReferenceDataService;", mainWindowSource);
        Assert.DoesNotContain("private readonly DeployOnTheFlyWorkspaceOwner _deployOnTheFlyWorkspaceOwner;", mainWindowSource);
        Assert.DoesNotContain("private readonly DeployFromTemplateWorkspaceComposition _deployFromTemplateWorkspaceComposition;", mainWindowSource);
        Assert.DoesNotContain("private readonly DeployResultsPanelCoordinator _deployResultsPanelCoordinator;", mainWindowSource);
        Assert.DoesNotContain("_deployWorkspaceComposition = new DeployWorkspaceComposition(", mainWindowSource);
        Assert.DoesNotContain("_deployOnTheFlyWorkspaceOwner = new DeployOnTheFlyWorkspaceOwner(", mainWindowSource);
        Assert.DoesNotContain("_deployResultsPanelCoordinator = new DeployResultsPanelCoordinator(", mainWindowSource);
        Assert.DoesNotContain("if (IsDeployFromTemplateActive)", mainWindowSource);
        Assert.DoesNotContain("_ = _deployFromTemplateWorkspaceComposition.EnsureTemplatesLoadedAsync(forceRefresh: false);", mainWindowSource);
    }

    [Fact]
    public void DeployCapabilityBootstrapContext_GroupsShellOwnedInputs()
    {
        var contextSource = LoadDeployCapabilityBootstrapContextSource();

        Assert.Contains("internal sealed class DeployCapabilityBootstrapContext", contextSource);
        Assert.Contains("public DeployCapabilityShellBridge ShellBridge { get; init; } = null!;", contextSource);
        Assert.Contains("public DeployCapabilityShellViewHosts ShellViewHosts { get; init; } = null!;", contextSource);
        Assert.Contains("public DeployTemplatesShellAdapter Templates { get; init; } = null!;", contextSource);
        Assert.DoesNotContain("public IServiceProvider Services", contextSource);
        Assert.DoesNotContain("public FrameworkElement LocalNavigationHost", contextSource);
        Assert.DoesNotContain("public object? TemplateItemsSource", contextSource);
        Assert.DoesNotContain("public Func<bool> IsDeployFromTemplateActive", contextSource);
    }

    [Fact]
    public void DeployCapabilityBootstrap_BuildsDeployThroughNamedAssemblySteps()
    {
        var bootstrapSource = LoadDeployCapabilityBootstrapSource();

        Assert.Contains("internal static class DeployCapabilityBootstrap", bootstrapSource);
        Assert.Contains("ValidateContext(context);", bootstrapSource);
        Assert.Contains("public static DeployCapabilityRuntime Bootstrap(IServiceProvider services, DeployCapabilityBootstrapContext context)", bootstrapSource);
        Assert.Contains("var serviceBundle = ResolveServices(services);", bootstrapSource);
        Assert.Contains("var sharedSeams = BuildSharedDeploySeams(context, serviceBundle);", bootstrapSource);
        Assert.Contains("var quickDeployLane = BuildQuickDeployLane(context, serviceBundle, sharedSeams);", bootstrapSource);
        Assert.Contains("var fromTemplateLane = BuildFromTemplateLane(context, serviceBundle, sharedSeams);", bootstrapSource);
        Assert.Contains("var workspace = BuildWorkspace(context, quickDeployLane, fromTemplateLane);", bootstrapSource);
        Assert.Contains("var resultsPanelCoordinator = BuildResultsPanelCoordinator(context, quickDeployLane, fromTemplateLane);", bootstrapSource);
        Assert.Contains("sharedSeams.UiHooks.AttachSharedUiRefresh(workspace.RefreshSharedUiState);", bootstrapSource);
        Assert.Contains("sharedSeams.UiHooks.AttachResultsPanelRefresh(context.ShellBridge.RefreshResultsPanelState);", bootstrapSource);
        Assert.Contains("private static DeploySharedSeams BuildSharedDeploySeams(", bootstrapSource);
        Assert.Contains("private static IDeployQuickDeployLane BuildQuickDeployLane(", bootstrapSource);
        Assert.Contains("private static IDeployFromTemplateLane BuildFromTemplateLane(", bootstrapSource);
        Assert.Contains("private static DeployWorkspaceComposition BuildWorkspace(", bootstrapSource);
        Assert.Contains("private static DeployResultsPanelCoordinator BuildResultsPanelCoordinator(", bootstrapSource);
        Assert.Contains("private static DeployCapabilityRuntime BuildRuntime(", bootstrapSource);
        Assert.Contains("new DeployReferenceDataService(", bootstrapSource);
        Assert.Contains("new DeployResolveSuggestionsService()", bootstrapSource);
        Assert.Contains("new DeployTemplateEditorLauncher(context.Templates)", bootstrapSource);
        Assert.Contains("new DeployCapabilityUiHooks()", bootstrapSource);
        Assert.Contains("new DeployOnTheFlyWorkspaceOwner(", bootstrapSource);
        Assert.Contains("new DeployFromTemplateWorkspaceComposition(", bootstrapSource);
        Assert.Contains("new DeployWorkspaceComposition(", bootstrapSource);
        Assert.Contains("new DeployResultsPanelCoordinator(", bootstrapSource);
        Assert.DoesNotContain("new DeployTemplateEditorLauncher(context.TemplatesWorkspaceComposition)", bootstrapSource);
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
        Assert.DoesNotContain("DeployFromTemplateWorkspaceHost", runtimeSource);
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
        Assert.Contains("var shouldAutoOpenDeployRightPanel = CanActiveCapabilityOwnRightPanel() && _deployCapabilityRuntime.ShouldAutoOpenRightPanel();", mainWindowSource);
        Assert.Contains("if (!shouldAutoOpenDeployRightPanel)", mainWindowSource);
        Assert.Contains("_isDeployRightPanelAutoOpenSuppressed = false;", mainWindowSource);
        Assert.Contains("if (shouldAutoOpenDeployRightPanel && !_isDeployRightPanelAutoOpenSuppressed)", mainWindowSource);
        Assert.Contains("private void SetRightPanelOpenFromUserToggle(bool isOpen)", mainWindowSource);
        Assert.Contains("_isDeployRightPanelAutoOpenSuppressed = !isOpen &&", mainWindowSource);
        Assert.Contains("_deployCapabilityRuntime.ShouldAutoOpenRightPanel();", mainWindowSource);
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

    private static string LoadDeployCapabilityBootstrapContextSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployCapabilityBootstrapContext.cs");
        return File.ReadAllText(path);
    }

    private static string LoadDeployCapabilityBootstrapSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployCapabilityBootstrap.cs");
        return File.ReadAllText(path);
    }

    private static string LoadDeployCapabilityRuntimeSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployCapabilityRuntime.cs");
        return File.ReadAllText(path);
    }

    private static string LoadDeployResultsPanelCoordinatorSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployResultsPanelCoordinator.cs");
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
