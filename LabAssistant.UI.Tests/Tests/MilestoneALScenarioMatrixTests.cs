using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneALScenarioMatrixTests
{
    [Fact]
    public void ShellHeader_RemainsCapabilityLevelOwner_ForAssetsAndDeployViews()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("ContentTitleTextBlock.Text = _activeCapability.DisplayName;", source);
        Assert.Contains("Configure and run deployment workflows from one capability surface with readiness, remediation, and results context.", source);
        Assert.Contains("Manage shared Hyper-V assets, inventory, and compatibility state from one capability surface.", source);
        Assert.DoesNotContain("ContentTitleTextBlock.Text = IsAssetsSwitchesActive", source);
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
        Assert.Contains("Text=\"Correction Actions (Placeholder)\"", fromTemplateSource);
    }

    private static string LoadMainWindowSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml.cs");
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

    private static string LoadDeployOnTheFlyViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployOnTheFlyView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadDeployFromTemplateViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", "DeployFromTemplateView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }
}
