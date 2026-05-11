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
    public void MainWindow_AndDeployWorkspaceComposition_PreserveQuickDeployRouteHosting()
    {
        var xaml = LoadMainWindowXaml();
        var mainWindowSource = LoadMainWindowSource();
        var compositionSource = LoadDeployWorkspaceCompositionSource();

        Assert.NotNull(FindByName(xaml, "DeployOnTheFlyViewHost"));
        Assert.Contains("private readonly DeployCapabilityRuntime _deployCapabilityRuntime;", mainWindowSource);
        Assert.Contains("_deployCapabilityRuntime = CreateDeployCapabilityRuntime();", mainWindowSource);
        Assert.Contains("private DeployCapabilityRuntime CreateDeployCapabilityRuntime()", mainWindowSource);
        Assert.Contains("new DeployCapabilityShellBridge(", mainWindowSource);
        Assert.DoesNotContain("private readonly DeployOnTheFlyWorkspaceComposition _deployOnTheFlyWorkspaceComposition;", mainWindowSource);
        Assert.DoesNotContain("new DeployOnTheFlyWorkspaceHost(", mainWindowSource);
        Assert.DoesNotContain("AttachComposition(", mainWindowSource);

        Assert.Contains("_quickDeployLane.ApplyShellState(_shellBridge.IsDeployOnTheFlyActive);", compositionSource);
        Assert.Contains("_shellBridge.NavigateToRoute(ShellRouteKeys.DeployOnTheFly);", compositionSource);
        Assert.DoesNotContain("TryToggleRightPanelFromWorkflow", compositionSource);
        Assert.DoesNotContain("ShouldAutoOpenRightPanel", compositionSource);
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
    public void QuickDeploy_Owner_Composition_AndController_ProtectTheNewLocalBoundary()
    {
        var mainWindowSource = LoadMainWindowSource();
        var ownerSource = LoadDeployOnTheFlyWorkspaceOwnerSource();
        var compositionSource = LoadDeployOnTheFlyWorkspaceCompositionSource();
        var controllerSource = LoadDeployOnTheFlyWorkspaceControllerSource();
        var shellBridgeSource = LoadDeployOnTheFlyWorkspaceShellBridgeSource();
        var referenceDataSource = LoadDeployReferenceDataServiceSource();
        var resolveSuggestionsSource = LoadDeployResolveSuggestionsServiceSource();
        var viewSource = LoadDeployOnTheFlyViewSource();

        Assert.Contains("private readonly DeployCapabilityRuntime _deployCapabilityRuntime;", mainWindowSource);
        Assert.DoesNotContain("new DeployTemplateEditorLauncher(_templatesCapabilityRuntime)", mainWindowSource);
        Assert.DoesNotContain("BuildOnTheFlyTemplate()", mainWindowSource);
        Assert.DoesNotContain("EnsureDeployOnTheFlyReferenceDataAsync", mainWindowSource);
        Assert.DoesNotContain("applyResolveSuggestionsAsync: template => _deployWorkspaceComposition.ApplyResolveSuggestionsAsync(template),", mainWindowSource);

        Assert.Contains("internal sealed class DeployOnTheFlyWorkspaceOwner : IDeployOnTheFlyWorkspaceControllerHost", ownerSource);
        Assert.Contains("private readonly DeployOnTheFlyWorkspaceViewModel _workspace = new();", ownerSource);
        Assert.Contains("private readonly DeployOnTheFlyWorkspaceComposition _composition;", ownerSource);
        Assert.Contains("private readonly DeployOnTheFlyWorkspaceController _controller;", ownerSource);
        Assert.Contains("_composition = new DeployOnTheFlyWorkspaceComposition(view, rightPanelView, _workspace);", ownerSource);
        Assert.Contains("_controller = new DeployOnTheFlyWorkspaceController(_workspace, this);", ownerSource);
        Assert.Contains("public event EventHandler? SharedUiStateChanged;", ownerSource);
        Assert.Contains("public void ApplyShellState(bool isActive)", ownerSource);
        Assert.Contains("public void ApplyResultsPanelState(bool isActive, bool showPanel, bool panelUnavailable)", ownerSource);
        Assert.Contains("private void WireHandlers()", ownerSource);
        Assert.Contains("_view.VmEntrySelectionChanged += VmEntrySelectionChanged;", ownerSource);
        Assert.Contains("_view.ResolveSuggestionsRequested += ResolveSuggestionsRequested;", ownerSource);
        Assert.Contains("_view.OpenTemplateEditorRequested += OpenTemplateEditorRequested;", ownerSource);
        Assert.Contains("_view.StartDeployRequested += StartDeployRequested;", ownerSource);
        Assert.Contains("_view.OpenResultsPanelRequested += OpenResultsPanelRequested;", ownerSource);
        Assert.Contains("private async Task EnsureReferenceDataAsync(bool forceRefresh)", ownerSource);
        Assert.Contains("private LabTemplate BuildTemplate()", ownerSource);
        Assert.Contains("private void ReplaceVmEntriesFromTemplate(LabTemplate template)", ownerSource);
        Assert.Contains("private async Task<int> ApplyResolveSuggestionsAsync(LabTemplate template)", ownerSource);
        Assert.Contains("private async Task OpenTemplateEditorAsync()", ownerSource);
        Assert.Contains("_shellBridge.RequestResultsPanelToggle();", ownerSource);
        Assert.Contains("void IDeployOnTheFlyWorkspaceControllerHost.EnqueueUiUpdate(Action updateAction)", ownerSource);

        Assert.Contains("internal sealed class DeployOnTheFlyWorkspaceComposition", compositionSource);
        Assert.Contains("_view.SetVmEntriesSource(_workspace.VmEntryRows);", compositionSource);
        Assert.Contains("_rightPanelView.SetResultRowsItemsSource(_workspace.ResultRows);", compositionSource);
        Assert.Contains("public void SetVisibility(bool isActive)", compositionSource);
        Assert.Contains("public void ApplyResultsPanelState(bool isActive, bool showPanel, bool panelUnavailable)", compositionSource);
        Assert.Contains("public bool TryApplyVmFields(bool showSuccessStatus, bool showValidationErrors = true)", compositionSource);
        Assert.DoesNotContain("IDeployOnTheFlyCompositionHost", compositionSource);
        Assert.DoesNotContain("public async Task ResolveSuggestionsAsync()", compositionSource);
        Assert.DoesNotContain("public async Task OpenTemplateEditorAsync()", compositionSource);
        Assert.DoesNotContain("public void AddVmEntry()", compositionSource);
        Assert.DoesNotContain("public async Task RemoveVmEntryAsync(VmTemplate? vmEntry)", compositionSource);
        Assert.DoesNotContain("public void ApplyShellState(bool isActive)", compositionSource);
        Assert.DoesNotContain("WireHandlers()", compositionSource);

        Assert.Contains("internal sealed class DeployOnTheFlyWorkspaceController", controllerSource);
        Assert.Contains("await _host.EnsureReferenceDataAsync(forceRefresh: false);", controllerSource);
        Assert.Contains("PrepareDeployExecution(deployContext.MultiVmContext);", controllerSource);
        Assert.Contains("var summary = await _host.DeployAllAsync(deployContext.MultiVmContext);", controllerSource);
        Assert.Contains("private void AttachProgressCallbacks(MultiVmDeploymentContext context)", controllerSource);

        Assert.Contains("internal sealed class DeployOnTheFlyWorkspaceShellBridge", shellBridgeSource);
        Assert.Contains("public async Task<bool> ShowRemoveVmEntryConfirmationDialogAsync(string vmName)", shellBridgeSource);
        Assert.Contains("public void RequestResultsPanelToggle() => _requestResultsPanelToggle();", shellBridgeSource);
        Assert.Contains("public void RefreshResultsPanelState() => _refreshResultsPanelState();", shellBridgeSource);

        Assert.Contains("internal sealed class DeployReferenceDataService", referenceDataSource);
        Assert.Contains("public async Task EnsureAsync(bool forceRefresh)", referenceDataSource);
        Assert.Contains("public IReadOnlyList<TemplateVhdxCatalogOption> VhdxCatalogOptions => _vhdxCatalogOptions;", referenceDataSource);

        Assert.Contains("internal sealed class DeployResolveSuggestionsService", resolveSuggestionsSource);
        Assert.Contains("public int Apply(", resolveSuggestionsSource);

        Assert.False(File.Exists(GetQuickDeployHostPath()));
        Assert.False(File.Exists(GetQuickDeployCompositionHostPath()));

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
        Assert.Contains("_fromTemplateLane.ApplyShellState(_shellBridge.IsDeployFromTemplateActive);", source);
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

    private static string LoadDeployOnTheFlyWorkspaceOwnerSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "QuickDeploy", "DeployOnTheFlyWorkspaceOwner.cs");
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

    private static string LoadDeployOnTheFlyWorkspaceShellBridgeSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "QuickDeploy", "DeployOnTheFlyWorkspaceShellBridge.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployReferenceDataServiceSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployReferenceDataService.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployResolveSuggestionsServiceSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployResolveSuggestionsService.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string GetQuickDeployHostPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "LabAssistant.WinUI", "ViewModels", "Deploy", "QuickDeploy", "DeployOnTheFlyWorkspaceHost.cs"));
    }

    private static string GetQuickDeployCompositionHostPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "LabAssistant.WinUI", "ViewModels", "Deploy", "QuickDeploy", "IDeployOnTheFlyCompositionHost.cs"));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
