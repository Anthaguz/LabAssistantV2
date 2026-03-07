using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneAKScenarioMatrixTests
{
    [Fact]
    public void ShellViewModel_DefinesAssetsSwitchesRouteAndSubview()
    {
        var source = LoadShellViewModelSource();

        Assert.Contains("public const string AssetsSwitches = \"assets.switches\";", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.AssetsSwitches, \"Virtual Switches\"", source);
    }

    [Fact]
    public void MainWindow_HostsAssetsSwitchesViewInShell()
    {
        var xaml = LoadMainWindowXaml();
        var source = LoadMainWindowSource();

        Assert.NotNull(FindByName(xaml, "AssetsSwitchesViewHost"));
        Assert.Contains("private AssetsSwitchesView AssetsSwitchesView => AssetsSwitchesViewHost;", source);
        Assert.Contains("private FrameworkElement AssetsSwitchesPanel => AssetsSwitchesViewHost;", source);
        Assert.Contains("AssetsSwitchesPanel.Visibility = IsAssetsSwitchesActive ? Visibility.Visible : Visibility.Collapsed;", source);
        Assert.Contains("private bool IsAssetsSwitchesActive =>", source);
    }

    [Fact]
    public void AssetsSwitchesView_DefinesScaffoldRegionsAndExplicitStateContainers()
    {
        var xaml = LoadAssetsSwitchesViewXaml();

        Assert.NotNull(FindByName(xaml, "AssetsSwitchesActionsRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesStatusRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesListRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesDetailsRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesEditRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesLoadingStatePanel"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesEmptyStatePanel"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesErrorStatePanel"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesRefreshButton"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesCreateButton"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesApplyButton"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesValidateButton"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesDeleteButton"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesTypeInfoButton"));
    }

    [Fact]
    public void Ak3_WiresSwitchOperations_WithExplicitDeleteGuardrails()
    {
        var source = LoadMainWindowSource();
        var viewSource = LoadAssetsSwitchesViewXamlSource();
        var capabilitySource = LoadAssetsSwitchesCapabilityServiceSource();

        Assert.Contains("EnsureAssetsSwitchesAsync", source);
        Assert.Contains("UpdateAssetsSwitchesUi", source);
        Assert.Contains("AssetsSwitchesRefreshButton_Click", source);
        Assert.Contains("AssetsSwitchesCreateButton_Click", source);
        Assert.Contains("AssetsSwitchesApplyButton_Click", source);
        Assert.Contains("AssetsSwitchesValidateButton_Click", source);
        Assert.Contains("AssetsSwitchesDeleteButton_Click", source);
        Assert.Contains("ShowAssetsSwitchDeleteConfirmationDialogAsync", source);
        Assert.Contains("IAssetsSwitchesCapabilityService", source);
        Assert.Contains("_hasAssetsSwitchesErrorState", source);
        Assert.Contains("AssetsSwitchesErrorStatePanel.Visibility = _hasAssetsSwitchesErrorState ? Visibility.Visible : Visibility.Collapsed;", source);
        Assert.Contains("Delete is allowed only when no Hyper-V VM is attached to the switch.", source);
        Assert.Contains("Delete is blocked because at least one VM is attached to this switch.", capabilitySource);
        Assert.Contains("Switch type changes are not supported. Create a new switch instead.", capabilitySource);
        Assert.Contains("External adapter rebinding is not supported here. Create a new switch instead.", capabilitySource);
        Assert.Contains("External: binds the switch to a host network adapter for outside connectivity.", viewSource);
    }

    private static string LoadShellViewModelSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "ShellViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XDocument LoadMainWindowXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static string LoadMainWindowSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XDocument LoadAssetsSwitchesViewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsSwitchesView.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static string LoadAssetsSwitchesViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsSwitchesView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsSwitchesCapabilityServiceSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.Business", "Assets", "AssetsSwitchesCapabilityService.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
