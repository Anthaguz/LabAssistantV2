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
        Assert.Contains("SyncDeploySubviewSelection();", source);
        Assert.Contains("SyncAssetsSubviewSelection();", source);
        Assert.Contains("SyncDiagnosticsSubviewSelection();", source);
    }

    [Fact]
    public void Templates_UsesLibraryAsPrimarySurface_AndKeepsEditorAsWorkflowStateEntry()
    {
        var xaml = LoadMainWindowXaml();
        var mainWindowSource = LoadMainWindowSource();
        var shellSource = LoadShellViewModelSource();

        Assert.NotNull(FindByName(xaml, "TemplatesWorkspacePanel"));
        Assert.NotNull(FindByName(xaml, "TemplatesLibraryViewHost"));
        Assert.NotNull(FindByName(xaml, "TemplatesEditorViewHost"));
        Assert.DoesNotContain("TemplatesSubviewTabView", xaml.ToString());

        Assert.Contains("showChildRoutesInShell: false", shellSource);
        Assert.Contains("new ShellSubview(ShellRouteKeys.TemplatesLibrary, \"Library\"", shellSource);
        Assert.Contains("new ShellSubview(ShellRouteKeys.TemplatesEditor, \"Editor\"", shellSource);

        Assert.Contains("if (!capability.ShowChildRoutesInShell)", mainWindowSource);
        Assert.Contains("TemplatesWorkspaceHost.Visibility = IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.Contains("TemplatesLibraryViewHost.Visibility = IsTemplatesLibraryActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.Contains("TemplatesEditorViewHost.Visibility = IsTemplatesEditorActive ? Visibility.Visible : Visibility.Collapsed;", mainWindowSource);
        Assert.Contains("NavigateToRoute(capability.DefaultSubview.RouteKey);", mainWindowSource);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.TemplatesEditor);", mainWindowSource);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.TemplatesLibrary);", mainWindowSource);
    }

    [Fact]
    public void OverviewViews_ProvideLocalRouteEntryPoints_WithoutInventingDomainSemantics()
    {
        var assetsOverviewSource = LoadAssetsOverviewViewXamlSource();
        var deployOverviewSource = LoadDeployOverviewViewXamlSource();
        var diagnosticsOverviewSource = LoadDiagnosticsOverviewViewXamlSource();
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains("x:Name=\"AssetsOverviewOpenBaseDisksButton\"", assetsOverviewSource);
        Assert.Contains("x:Name=\"AssetsOverviewOpenSwitchesButton\"", assetsOverviewSource);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.AssetsBaseDisks);", mainWindowSource);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.AssetsSwitches);", mainWindowSource);

        Assert.Contains("x:Name=\"DeployOverviewOpenQuickDeployButton\"", deployOverviewSource);
        Assert.Contains("x:Name=\"DeployOverviewOpenFromTemplateButton\"", deployOverviewSource);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.DeployOnTheFly);", mainWindowSource);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.DeployFromTemplate);", mainWindowSource);

        Assert.Contains("x:Name=\"DiagnosticsOverviewOpenLogsButton\"", diagnosticsOverviewSource);
        Assert.Contains("x:Name=\"DiagnosticsOverviewOpenSupportExportButton\"", diagnosticsOverviewSource);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.DiagnosticsLogs);", mainWindowSource);
        Assert.Contains("OpenStructuredLogLocation();", mainWindowSource);
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
        var fromTemplateRightPanelSource = LoadDeployFromTemplateRightPanelViewXamlSource();

        Assert.Contains("x:Name=\"DeployOnTheFlyOpenResultsPanelButton\"", quickDeploySource);
        Assert.Contains("x:Name=\"DeployOpenResultsPanelButton\"", fromTemplateSource);
        Assert.Contains("DeployOpenResultsPanelButton.Click += DeployOpenResultsPanelButton_Click;", mainWindowSource);
        Assert.Contains("DeployOnTheFlyOpenResultsPanelButton.Click += DeployOnTheFlyOpenResultsPanelButton_Click;", mainWindowSource);
        Assert.Contains("private void ToggleDeployRightPanelFromWorkflow()", mainWindowSource);
        Assert.Contains("IssueBadge.Visibility = Visibility.Collapsed;", mainWindowSource);
        Assert.Contains("\"From Template Progress / Results\"", mainWindowSource);
        Assert.Contains("\"Quick Deploy Progress / Results\"", mainWindowSource);
        Assert.Contains("Text=\"Run warnings / errors\"", fromTemplateRightPanelSource);
    }

    [Fact]
    public void QuickDeploy_MovesIssueSignalingIntoWorkflowRowsAndEditorSurface()
    {
        var quickDeploySource = LoadDeployOnTheFlyViewXamlSource();
        var quickDeployCodeBehindSource = LoadDeployOnTheFlyViewCodeBehindSource();
        var mainWindowSource = LoadMainWindowSource();

        Assert.Contains("x:Name=\"DeployOnTheFlyEditorIssueSummaryTextBlock\"", quickDeploySource);
        Assert.Contains("Text=\"{Binding IssueBadgeText}\"", quickDeploySource);
        Assert.Contains("Text=\"{Binding IssueSummary}\"", quickDeploySource);
        Assert.Contains("Click=\"DeployOnTheFlyRowRemoveButton_Click\"", quickDeploySource);
        Assert.Contains("Visibility=\"Collapsed\"", quickDeploySource);

        Assert.Contains("public event Action<VmTemplate>? VmRemoveRequested;", quickDeployCodeBehindSource);
        Assert.Contains("VmRemoveRequested?.Invoke(vmEntry);", quickDeployCodeBehindSource);

        Assert.Contains("_deployOnTheFlyVmEntryRows", mainWindowSource);
        Assert.Contains("UpdateDeployOnTheFlyVmEntryRows();", mainWindowSource);
        Assert.Contains("UpdateDeployOnTheFlyEditorIssueSummary();", mainWindowSource);
        Assert.Contains("GetDeployOnTheFlyDraftIssues()", mainWindowSource);
        Assert.Contains("Review VM row badges and the selected VM details to fix blockers here before deploy.", mainWindowSource);
    }

    private static string LoadMainWindowSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XDocument LoadMainWindowXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static string LoadShellViewModelSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "ShellViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsBaseDisksViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsBaseDisksView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsSwitchesViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsSwitchesView.xaml");
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
