using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneALScenarioMatrixTests
{
    [Fact]
    public void ShellHeader_RemainsCapabilityLevelOwner_ForAssetsDeployAndDiagnostics()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("ContentTitleTextBlock.Text = _activeCapability.DisplayName;", source);
        Assert.Contains("Configure and run deployment workflows from one capability surface with readiness, remediation, and results context.", source);
        Assert.Contains("Manage shared Hyper-V assets, inventory, and compatibility state from one capability surface.", source);
        Assert.Contains("Inspect support-oriented diagnostics and structured log context from one capability surface.", source);
        Assert.DoesNotContain("ContentTitleTextBlock.Text = IsAssetsSwitchesActive", source);
    }

    [Fact]
    public void ShellViewModel_DefinesApprovedOverviewRoutes_AndSubviewOrdering()
    {
        var source = LoadShellViewModelSource();

        Assert.Contains("public const string AssetsOverview = \"assets.overview\";", source);
        Assert.Contains("public const string DeployOverview = \"deploy.overview\";", source);
        Assert.Contains("public const string DiagnosticsOverview = \"diagnostics.overview\";", source);

        var assetsOverviewIndex = source.IndexOf("new ShellSubview(ShellRouteKeys.AssetsOverview", StringComparison.Ordinal);
        var assetsBaseDisksIndex = source.IndexOf("new ShellSubview(ShellRouteKeys.AssetsBaseDisks", StringComparison.Ordinal);
        var assetsSwitchesIndex = source.IndexOf("new ShellSubview(ShellRouteKeys.AssetsSwitches", StringComparison.Ordinal);
        Assert.True(assetsOverviewIndex >= 0 && assetsBaseDisksIndex >= 0 && assetsSwitchesIndex >= 0, "Assets overview/base disks/switches routes must exist.");
        Assert.True(assetsOverviewIndex < assetsBaseDisksIndex && assetsBaseDisksIndex < assetsSwitchesIndex, "Assets local ordering must be Overview, Base Disks, Switches.");

        var deployOverviewIndex = source.IndexOf("new ShellSubview(ShellRouteKeys.DeployOverview", StringComparison.Ordinal);
        var deployQuickDeployIndex = source.IndexOf("new ShellSubview(ShellRouteKeys.DeployOnTheFly", StringComparison.Ordinal);
        var deployFromTemplateIndex = source.IndexOf("new ShellSubview(ShellRouteKeys.DeployFromTemplate", StringComparison.Ordinal);
        Assert.True(deployOverviewIndex >= 0 && deployQuickDeployIndex >= 0 && deployFromTemplateIndex >= 0, "Deploy overview/quick deploy/from template routes must exist.");
        Assert.True(deployOverviewIndex < deployQuickDeployIndex && deployQuickDeployIndex < deployFromTemplateIndex, "Deploy local ordering must be Overview, Quick Deploy, From Template.");

        var diagnosticsOverviewIndex = source.IndexOf("new ShellSubview(ShellRouteKeys.DiagnosticsOverview", StringComparison.Ordinal);
        var diagnosticsLogsIndex = source.IndexOf("new ShellSubview(ShellRouteKeys.DiagnosticsLogs", StringComparison.Ordinal);
        Assert.True(diagnosticsOverviewIndex >= 0 && diagnosticsLogsIndex >= 0, "Diagnostics overview/logs routes must exist.");
        Assert.True(diagnosticsOverviewIndex < diagnosticsLogsIndex, "Diagnostics local ordering must be Overview, Logs.");
        Assert.Contains("public bool HasOverview =>", source);
    }

    [Fact]
    public void MainWindow_DefinesCapabilityLocalNavigationHosts_AndParentOverviewRouting()
    {
        var xaml = LoadMainWindowXaml();
        var source = LoadMainWindowSource();
        var assetsCompositionSource = LoadAssetsWorkspaceCompositionSource();
        var deployCompositionSource = LoadDeployWorkspaceCompositionSource();
        var diagnosticsCompositionSource = LoadDiagnosticsWorkspaceCompositionSource();

        Assert.NotNull(FindByName(xaml, "DeployLocalNavigationPanel"));
        Assert.NotNull(FindByName(xaml, "DeploySubviewTabView"));
        Assert.NotNull(FindByName(xaml, "DeployOverviewViewHost"));
        Assert.NotNull(FindByName(xaml, "DeployQuickDeployTabViewItem"));
        Assert.NotNull(FindByName(xaml, "DeployFromTemplateTabViewItem"));

        Assert.NotNull(FindByName(xaml, "AssetsLocalNavigationPanel"));
        Assert.NotNull(FindByName(xaml, "AssetsSubviewTabView"));
        Assert.NotNull(FindByName(xaml, "AssetsOverviewViewHost"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksTabViewItem"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesTabViewItem"));

        Assert.NotNull(FindByName(xaml, "DiagnosticsLocalNavigationPanel"));
        Assert.NotNull(FindByName(xaml, "DiagnosticsSubviewTabView"));
        Assert.NotNull(FindByName(xaml, "DiagnosticsOverviewViewHost"));
        Assert.NotNull(FindByName(xaml, "DiagnosticsLogsTabViewItem"));

        Assert.Contains("if (_shellViewModel.TryResolveCapability(key, out var capability))", source);
        Assert.Contains("NavigateToRoute(capability.DefaultSubview.RouteKey);", source);
        Assert.Contains("if (capability.HasOverview && string.Equals(subview.RouteKey, capability.DefaultSubview.RouteKey, StringComparison.Ordinal))", source);
        Assert.Contains("_deployWorkspaceComposition.ApplyShellState();", source);
        Assert.Contains("_subviewTabView.SelectionChanged += DeploySubviewTabView_SelectionChanged;", deployCompositionSource);
        Assert.Contains("SyncDeploySubviewSelection()", deployCompositionSource);
        Assert.Contains("_assetsWorkspaceComposition.ApplyShellState();", source);
        Assert.Contains("SyncAssetsSubviewSelection();", assetsCompositionSource);
        Assert.Contains("_diagnosticsWorkspaceComposition.ApplyShellState();", source);
        Assert.Contains("SyncDiagnosticsSubviewSelection()", diagnosticsCompositionSource);
    }

    [Fact]
    public void Templates_UsesLibraryAsPrimarySurface_AndKeepsEditorAsWorkflowStateEntry()
    {
        var xaml = LoadMainWindowXaml();
        var mainWindowSource = LoadMainWindowSource();
        var shellSource = LoadShellViewModelSource();
        var compositionSource = LoadTemplatesWorkspaceCompositionSource();
        var libraryCompositionSource = LoadTemplatesLibraryWorkspaceCompositionSource();

        Assert.NotNull(FindByName(xaml, "TemplatesWorkspacePanel"));
        Assert.NotNull(FindByName(xaml, "TemplatesLibraryViewHost"));
        Assert.NotNull(FindByName(xaml, "TemplatesEditorViewHost"));
        Assert.DoesNotContain("TemplatesSubviewTabView", xaml.ToString());

        Assert.Contains("showChildRoutesInShell: false", shellSource);
        Assert.Contains("new ShellSubview(ShellRouteKeys.TemplatesLibrary, \"Library\"", shellSource);
        Assert.Contains("new ShellSubview(ShellRouteKeys.TemplatesEditor, \"Editor\"", shellSource);

        Assert.Contains("if (!capability.ShowChildRoutesInShell)", mainWindowSource);
        Assert.Contains("private readonly TemplatesWorkspaceComposition _templatesWorkspaceComposition;", mainWindowSource);
        Assert.Contains("_templatesWorkspaceComposition.ApplyShellState();", mainWindowSource);
        Assert.Contains("_workspaceHost.Visibility = _shellBridge.IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;", compositionSource);
        Assert.Contains("_libraryComposition.ApplyShellState(_shellBridge.IsTemplatesLibraryActive);", compositionSource);
        Assert.Contains("_view.Visibility = isLibraryActive ? Visibility.Visible : Visibility.Collapsed;", libraryCompositionSource);
        Assert.Contains("_editorComposition.ApplyShellState(_shellBridge.IsTemplatesEditorActive);", compositionSource);
        Assert.Contains("NavigateToRoute(capability.DefaultSubview.RouteKey);", mainWindowSource);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.TemplatesEditor);", mainWindowSource);
        Assert.Contains("() => NavigateToRoute(ShellRouteKeys.TemplatesLibrary)", mainWindowSource);
    }

    [Fact]
    public void OverviewViews_ProvideLocalRouteEntryPoints_WithoutInventingDomainSemantics()
    {
        var assetsOverviewSource = LoadAssetsOverviewViewXamlSource();
        var deployOverviewSource = LoadDeployOverviewViewXamlSource();
        var deployOverviewCodeBehindSource = LoadDeployOverviewViewCodeBehindSource();
        var diagnosticsOverviewSource = LoadDiagnosticsOverviewViewXamlSource();
        var assetsCompositionSource = LoadAssetsWorkspaceCompositionSource();
        var diagnosticsCompositionSource = LoadDiagnosticsWorkspaceCompositionSource();
        var diagnosticsOverviewCompositionSource = LoadDiagnosticsOverviewWorkspaceCompositionSource();
        var diagnosticsOverviewCodeBehindSource = LoadDiagnosticsOverviewCodeBehindSource();

        Assert.Contains("x:Name=\"AssetsOverviewOpenBaseDisksButton\"", assetsOverviewSource);
        Assert.Contains("x:Name=\"AssetsOverviewOpenSwitchesButton\"", assetsOverviewSource);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.AssetsBaseDisks);", assetsCompositionSource);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.AssetsSwitches);", assetsCompositionSource);

        Assert.Contains("x:Name=\"DeployOverviewOpenQuickDeployButton\"", deployOverviewSource);
        Assert.Contains("x:Name=\"DeployOverviewOpenFromTemplateButton\"", deployOverviewSource);
        Assert.Contains("Click=\"DeployOverviewOpenQuickDeployButton_Click\"", deployOverviewSource);
        Assert.Contains("Click=\"DeployOverviewOpenFromTemplateButton_Click\"", deployOverviewSource);
        Assert.Contains("public event EventHandler? OpenQuickDeployRequested;", deployOverviewCodeBehindSource);
        Assert.Contains("public event EventHandler? OpenFromTemplateRequested;", deployOverviewCodeBehindSource);
        Assert.Contains("public void UpdateSummary(string quickDeploySummaryText, string fromTemplateSummaryText)", deployOverviewCodeBehindSource);
        Assert.DoesNotContain("DeployOverviewOpenQuickDeployButtonControl", deployOverviewCodeBehindSource);
        Assert.DoesNotContain("DeployOverviewOpenFromTemplateButtonControl", deployOverviewCodeBehindSource);
        Assert.DoesNotContain("DeployOverviewQuickDeploySummaryTextBlockControl", deployOverviewCodeBehindSource);
        Assert.DoesNotContain("DeployOverviewFromTemplateSummaryTextBlockControl", deployOverviewCodeBehindSource);

        Assert.Contains("x:Name=\"DiagnosticsOverviewOpenLogsButton\"", diagnosticsOverviewSource);
        Assert.Contains("x:Name=\"DiagnosticsOverviewOpenSupportExportButton\"", diagnosticsOverviewSource);
        Assert.Contains("Click=\"DiagnosticsOverviewOpenLogsButton_Click\"", diagnosticsOverviewSource);
        Assert.Contains("Click=\"DiagnosticsOverviewOpenSupportExportButton_Click\"", diagnosticsOverviewSource);
        Assert.Contains("public event EventHandler? OpenLogsRequested;", diagnosticsOverviewCodeBehindSource);
        Assert.Contains("public event EventHandler? OpenSupportExportRequested;", diagnosticsOverviewCodeBehindSource);
        Assert.Contains("public void UpdateSummary(string logsSummaryText, string supportSummaryText)", diagnosticsOverviewCodeBehindSource);
        Assert.Contains("_overviewWorkspaceComposition = new DiagnosticsOverviewWorkspaceComposition(", diagnosticsCompositionSource);
        Assert.Contains("_view.OpenLogsRequested += OpenLogsRequested;", diagnosticsOverviewCompositionSource);
        Assert.Contains("_view.OpenSupportExportRequested += OpenSupportExportRequested;", diagnosticsOverviewCompositionSource);
    }

    [Fact]
    public void AssetsBaseDisksView_RemovesRedundantPageLevelHeaderBand()
    {
        var source = LoadAssetsBaseDisksViewXamlSource();

        Assert.DoesNotContain("Text=\"Assets / Base Disks\"", source);
        Assert.DoesNotContain("Manage registered base disks with in-context metadata editing, validation visibility, and registry-only removal.", source);
        Assert.Contains("Text=\"Registered Base Disks\"", source);
        Assert.Contains("Text=\"Selected Disk Details\"", source);
    }

    [Fact]
    public void AssetsSwitchesView_KeepsLocalSectionHeadersWithoutPageLevelBand()
    {
        var source = LoadAssetsSwitchesViewXamlSource();

        Assert.DoesNotContain("Text=\"Assets / Virtual Switches\"", source);
        Assert.DoesNotContain("Manage Hyper-V virtual switches", source);
        Assert.Contains("Text=\"Virtual Switch Inventory\"", source);
        Assert.Contains("Text=\"Selected Switch Details\"", source);
    }

    [Fact]
    public void DeployViews_RemoveRedundantPageLevelTitleBands_AndKeepWorkflowSections()
    {
        var quickDeploySource = LoadDeployOnTheFlyViewXamlSource();
        var fromTemplateSource = LoadDeployFromTemplateViewXamlSource();

        Assert.DoesNotContain("Text=\"Quick Deploy\"", quickDeploySource);
        Assert.DoesNotContain("Configure VM entries, evaluate readiness, resolve blockers, and start deploy when ready.", quickDeploySource);
        Assert.Contains("Text=\"VM Entries\"", quickDeploySource);
        Assert.Contains("Text=\"VM Properties\"", quickDeploySource);

        Assert.DoesNotContain("Text=\"Deploy From Template\"", fromTemplateSource);
        Assert.DoesNotContain("Deploy from-template with compact status, expandable per-VM details, and collapsed global issue drawer.", fromTemplateSource);
        Assert.Contains("Text=\"Template Review\"", fromTemplateSource);
        Assert.Contains("Text=\"Remediation and Deploy\"", fromTemplateSource);
        Assert.DoesNotContain("Text=\"VM Properties\"", fromTemplateSource);
    }

    [Fact]
    public void DeployRightPanel_RemainsShellOwnedButUsesWorkflowLocalTriggers()
    {
        var quickDeploySource = LoadDeployOnTheFlyViewXamlSource();
        var fromTemplateSource = LoadDeployFromTemplateViewXamlSource();
        var mainWindowSource = LoadMainWindowSource();
        var quickDeployCompositionSource = LoadDeployOnTheFlyWorkspaceCompositionSource();
        var fromTemplateRightPanelSource = LoadDeployFromTemplateRightPanelViewXamlSource();

        Assert.Contains("x:Name=\"DeployOnTheFlyOpenResultsPanelButton\"", quickDeploySource);
        Assert.Contains("x:Name=\"DeployOpenResultsPanelButton\"", fromTemplateSource);
        Assert.Contains("DeployFromTemplateView.OpenResultsPanelRequested += DeployOpenResultsPanelButton_Click;", mainWindowSource);
        Assert.Contains("_view.OpenResultsPanelRequested += (_, _) => _host.OnOpenResultsPanelRequested();", quickDeployCompositionSource);
        Assert.Contains("private void ToggleDeployRightPanelFromWorkflow()", mainWindowSource);
        Assert.Contains("IssueBadge.Visibility = Visibility.Collapsed;", mainWindowSource);
        Assert.Contains("\"From Template Progress / Results\"", mainWindowSource);
        Assert.Contains("\"Quick Deploy Progress / Results\"", mainWindowSource);
        Assert.Contains("Text=\"Run warnings / errors\"", fromTemplateRightPanelSource);
    }

    [Fact]
    public void FromTemplate_MainWorkspaceStaysReviewAndRemediationOriented()
    {
        var fromTemplateSource = LoadDeployFromTemplateViewXamlSource();
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains("x:Name=\"DeployTemplateSummaryTextBlock\"", fromTemplateSource);
        Assert.Contains("x:Name=\"DeployTemplateRemediationTextBlock\"", fromTemplateSource);
        Assert.Contains("x:Name=\"DeploySharedIssuesSummaryTextBlock\"", fromTemplateSource);
        Assert.Contains("x:Name=\"DeploySharedIssuesListView\"", fromTemplateSource);
        Assert.Contains("Content=\"Review Readiness\"", fromTemplateSource);
        Assert.Contains("Content=\"Fix in Templates Editor\"", fromTemplateSource);

        Assert.Contains("_deployFromTemplateWorkspaceComposition.ReplaceIssueRows(issueRows);", mainWindowSource);
        Assert.Contains("Template Review", fromTemplateSource);
        Assert.Contains("_deployFromTemplateWorkspaceComposition.RefreshReviewState(hasBlockingFailures);", mainWindowSource);
    }

    [Fact]
    public void QuickDeploy_MovesIssueSignalingIntoWorkflowRowsAndEditorSurface()
    {
        var quickDeploySource = LoadDeployOnTheFlyViewXamlSource();
        var quickDeployCodeBehindSource = LoadDeployOnTheFlyViewCodeBehindSource();
        var quickDeployCompositionSource = LoadDeployOnTheFlyWorkspaceCompositionSource();
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains("x:Name=\"DeployOnTheFlyEditorIssueSummaryTextBlock\"", quickDeploySource);
        Assert.Contains("Text=\"{Binding IssueBadgeText}\"", quickDeploySource);
        Assert.Contains("Text=\"{Binding IssueSummary}\"", quickDeploySource);
        Assert.Contains("Click=\"DeployOnTheFlyRowRemoveButton_Click\"", quickDeploySource);
        Assert.Contains("Visibility=\"Collapsed\"", quickDeploySource);

        Assert.Contains("public event Action<VmTemplate>? VmRemoveRequested;", quickDeployCodeBehindSource);
        Assert.Contains("VmRemoveRequested?.Invoke(vmEntry);", quickDeployCodeBehindSource);
        Assert.Contains("public void ApplyWorkspaceState(DeployOnTheFlyWorkspaceViewState state)", quickDeployCodeBehindSource);
        Assert.DoesNotContain("public TextBlock DeployOnTheFlyEditorIssueSummaryTextBlockControl =>", quickDeployCodeBehindSource);

        Assert.Contains("_deployOnTheFlyWorkspace.VmEntryRows", mainWindowSource);
        Assert.Contains("UpdateDeployOnTheFlyVmEntryRows();", mainWindowSource);
        Assert.Contains("BuildDeployOnTheFlyEditorIssueSummaryText()", mainWindowSource);
        Assert.Contains("GetDeployOnTheFlyDraftIssues()", mainWindowSource);
        Assert.Contains("Review VM row badges and the selected VM details to fix blockers here before deploy.", mainWindowSource);
        Assert.Contains("_view.SetResultsPanelLauncherState(", quickDeployCompositionSource);
    }

    [Fact]
    public void ShellAndMigratedViews_ConvergeOnCompactDrawerAndBoundedScrollOwnership()
    {
        var mainWindowSource = LoadMainWindowSource();
        var machinesSource = LoadMachinesOverviewViewXamlSource();
        var machinesCodeBehindSource = LoadMachinesOverviewViewCodeBehindSource();
        var baseDisksSource = LoadAssetsBaseDisksViewXamlSource();
        var baseDisksCodeBehindSource = LoadAssetsBaseDisksViewCodeBehindSource();
        var switchesSource = LoadAssetsSwitchesViewXamlSource();
        var switchesCodeBehindSource = LoadAssetsSwitchesViewCodeBehindSource();
        var quickDeploySource = LoadDeployOnTheFlyViewXamlSource();
        var quickDeployCodeBehindSource = LoadDeployOnTheFlyViewCodeBehindSource();
        var fromTemplateSource = LoadDeployFromTemplateViewXamlSource();

        Assert.Contains("private const double ShellNavigationDrawerThreshold = 1100;", mainWindowSource);
        Assert.Contains("NavigationViewPaneDisplayMode.LeftMinimal", mainWindowSource);
        Assert.Contains("GlobalNavigationView.CompactPaneLength = useDrawerMode ? 0 : 56;", mainWindowSource);
        Assert.Contains("GlobalNavigationView.IsPaneOpen = false;", mainWindowSource);
        Assert.Contains("<ScrollViewer", LoadMainWindowXamlSource());
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", LoadMainWindowXamlSource());

        Assert.Contains("x:Name=\"MachinesListRowDefinition\"", machinesSource);
        Assert.Contains("x:Name=\"MachinesDetailsRowDefinition\"", machinesSource);
        Assert.Contains("x:Name=\"MachinesInventoryRegion\"", machinesSource);
        Assert.Contains("x:Name=\"MachinesDetailsRegion\"", machinesSource);
        Assert.Contains("UpdateLayoutMode(", machinesCodeBehindSource);
        Assert.Contains("CompactLayoutThreshold = 1024", machinesCodeBehindSource);

        Assert.Contains("x:Name=\"AssetsBaseDisksPrimaryRowDefinition\"", baseDisksSource);
        Assert.Contains("x:Name=\"AssetsBaseDisksStateRowDefinition\"", baseDisksSource);
        Assert.Contains("x:Name=\"AssetsBaseDisksDetailsRowDefinition\"", baseDisksSource);
        Assert.Contains("<ScrollViewer VerticalScrollBarVisibility=\"Auto\">", baseDisksSource);
        Assert.Contains("CompactLayoutThreshold = 1040", baseDisksCodeBehindSource);

        Assert.Contains("x:Name=\"AssetsSwitchesPrimaryRowDefinition\"", switchesSource);
        Assert.Contains("x:Name=\"AssetsSwitchesStateRowDefinition\"", switchesSource);
        Assert.Contains("x:Name=\"AssetsSwitchesDetailsRowDefinition\"", switchesSource);
        Assert.Contains("AssetsSwitchesAttachedVmsListView", switchesSource);
        Assert.Contains("CompactLayoutThreshold = 1040", switchesCodeBehindSource);

        Assert.Contains("x:Name=\"DeployOnTheFlyPrimaryRowDefinition\"", quickDeploySource);
        Assert.Contains("x:Name=\"DeployOnTheFlyEditorRowDefinition\"", quickDeploySource);
        Assert.Contains("DeployOnTheFlyVmEntriesPanel", quickDeploySource);
        Assert.Contains("DeployOnTheFlyVmEditorPanel", quickDeploySource);
        Assert.Contains("CompactLayoutThreshold = 1120", quickDeployCodeBehindSource);

        Assert.Contains("x:Name=\"DeployFromTemplateWorkspaceScrollViewer\"", fromTemplateSource);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", fromTemplateSource);
        Assert.DoesNotContain("Text=\"Deploy From Template\"", fromTemplateSource);
    }

    [Fact]
    public void MigratedViews_UseIconFirstChromeForRoutineLocalActions_AndKeepMajorWorkflowActionsTextual()
    {
        var machinesSource = LoadMachinesOverviewViewXamlSource();
        var baseDisksSource = LoadAssetsBaseDisksViewXamlSource();
        var switchesSource = LoadAssetsSwitchesViewXamlSource();
        var quickDeploySource = LoadDeployOnTheFlyViewXamlSource();
        var fromTemplateSource = LoadDeployFromTemplateViewXamlSource();

        Assert.Contains("ToolTipService.ToolTip=\"Refresh machine inventory\"", machinesSource);
        Assert.Contains("ToolTipService.ToolTip=\"Apply machine changes\"", machinesSource);
        Assert.Contains("ToolTipService.ToolTip=\"Delete virtual machine\"", machinesSource);
        Assert.Contains("Text=\"Power\"", machinesSource);
        Assert.Contains("Text=\"Remote access\"", machinesSource);
        Assert.Contains("Text=\"Delete\"", machinesSource);
        Assert.Contains("OpenConsoleButton", machinesSource);
        Assert.Contains("OpenRdpButton", machinesSource);
        Assert.Contains("DeleteVmButton", machinesSource);

        Assert.Contains("ToolTipService.ToolTip=\"Refresh base disk inventory\"", baseDisksSource);
        Assert.Contains("ToolTipService.ToolTip=\"Import or register base disk\"", baseDisksSource);
        Assert.Contains("ToolTipService.ToolTip=\"Save base disk metadata\"", baseDisksSource);
        Assert.Contains("ToolTipService.ToolTip=\"Remove base disk from catalog\"", baseDisksSource);

        Assert.Contains("ToolTipService.ToolTip=\"Refresh switch inventory\"", switchesSource);
        Assert.Contains("ToolTipService.ToolTip=\"Create a new virtual switch\"", switchesSource);
        Assert.Contains("ToolTipService.ToolTip=\"Apply switch changes\"", switchesSource);
        Assert.Contains("ToolTipService.ToolTip=\"Delete virtual switch\"", switchesSource);

        Assert.Contains("ToolTipService.ToolTip=\"Add VM\"", quickDeploySource);
        Assert.Contains("ToolTipService.ToolTip=\"Remove VM\"", quickDeploySource);
        Assert.Contains("Content=\"Resolve Suggestions\"", quickDeploySource);
        Assert.Contains("Content=\"Open in Templates Editor\"", quickDeploySource);
        Assert.Contains("Content=\"Start Deploy\"", quickDeploySource);

        Assert.Contains("ToolTipService.ToolTip=\"Reload templates\"", fromTemplateSource);
        Assert.Contains("Content=\"Review Readiness\"", fromTemplateSource);
        Assert.Contains("Content=\"Resolve Suggestions\"", fromTemplateSource);
        Assert.Contains("Content=\"Fix in Templates Editor\"", fromTemplateSource);
        Assert.Contains("Content=\"Start Deploy\"", fromTemplateSource);
    }

    [Fact]
    public void ALClosureAnchors_PreserveTheFullShellViewConsistencyChain()
    {
        var shellSource = LoadShellViewModelSource();
        var mainWindowSource = LoadMainWindowSource();
        var quickDeploySource = LoadDeployOnTheFlyViewXamlSource();
        var fromTemplateSource = LoadDeployFromTemplateViewXamlSource();

        Assert.Contains("ContentTitleTextBlock.Text = _activeCapability.DisplayName;", mainWindowSource);
        Assert.Contains("new ShellSubview(ShellRouteKeys.AssetsOverview", shellSource);
        Assert.Contains("new ShellSubview(ShellRouteKeys.DeployOverview", shellSource);
        Assert.Contains("new ShellSubview(ShellRouteKeys.DiagnosticsOverview", shellSource);
        Assert.Contains("showChildRoutesInShell: false", shellSource);
        Assert.Contains("private readonly TemplatesWorkspaceComposition _templatesWorkspaceComposition;", mainWindowSource);
        Assert.Contains("_templatesWorkspaceComposition.ApplyShellState();", mainWindowSource);
        Assert.Contains("private void ToggleDeployRightPanelFromWorkflow()", mainWindowSource);
        Assert.Contains("UpdateDeployOnTheFlyVmEntryRows();", mainWindowSource);
        Assert.Contains("_deployFromTemplateWorkspaceComposition.ReplaceIssueRows(issueRows);", mainWindowSource);
        Assert.Contains("private const double ShellNavigationDrawerThreshold = 1100;", mainWindowSource);
        Assert.Contains("x:Name=\"DeployOnTheFlyEditorIssueSummaryTextBlock\"", quickDeploySource);
        Assert.Contains("x:Name=\"DeploySharedIssuesListView\"", fromTemplateSource);
    }

    private static string LoadMainWindowSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployOnTheFlyWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployOnTheFlyWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XDocument LoadMainWindowXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static string LoadMainWindowXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadShellViewModelSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "ShellViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "AssetsWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "DeployWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTemplatesWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Templates", "TemplatesWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTemplatesLibraryWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Templates", "TemplatesLibraryWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsBaseDisksViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsBaseDisksView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadMachinesOverviewViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Machines", "MachinesOverviewView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadMachinesOverviewViewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Machines", "MachinesOverviewView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsSwitchesViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsSwitchesView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsBaseDisksViewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsBaseDisksView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsSwitchesViewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsSwitchesView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsOverviewViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsOverviewView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployOnTheFlyViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployOnTheFlyView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployOverviewViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployOverviewView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployOverviewViewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployOverviewView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployFromTemplateViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployFromTemplateView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployFromTemplateRightPanelViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployFromTemplateRightPanelView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDiagnosticsOverviewViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Diagnostics", "DiagnosticsOverviewView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDiagnosticsOverviewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Diagnostics", "DiagnosticsOverviewView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDiagnosticsWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Diagnostics", "DiagnosticsWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDiagnosticsOverviewWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Diagnostics", "DiagnosticsOverviewWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployOnTheFlyViewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployOnTheFlyView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
