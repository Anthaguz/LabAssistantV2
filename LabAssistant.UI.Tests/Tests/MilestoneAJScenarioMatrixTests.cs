using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneAJScenarioMatrixTests
{
    [Fact]
    public void ShellViewModel_DefinesAssetsBaseDisksRouteAndSubview()
    {
        var source = LoadShellViewModelSource();

        Assert.Contains("public const string AssetsBaseDisks = \"assets.base_disks\";", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.AssetsBaseDisks, \"Base Disks\"", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.AssetsSwitches, \"Virtual Switches\"", source);
    }

    [Fact]
    public void MainWindow_HostsAssetsBaseDisksViewInShell()
    {
        var xaml = LoadMainWindowXaml();
        var xamlSource = LoadMainWindowXamlSource();
        var source = LoadMainWindowSource();

        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksViewHost"));
        Assert.Contains("xmlns:assetsViews=\"using:LabAssistant.WinUI.Views.Assets\"", xamlSource);
        Assert.Contains("private AssetsBaseDisksView AssetsBaseDisksView => AssetsBaseDisksViewHost;", source);
        Assert.Contains("private FrameworkElement AssetsBaseDisksPanel => AssetsBaseDisksViewHost;", source);
        Assert.Contains("AssetsBaseDisksPanel.Visibility = IsAssetsBaseDisksActive ? Visibility.Visible : Visibility.Collapsed;", source);
    }

    [Fact]
    public void AssetsBaseDisksView_DefinesScaffoldRegionsAndExplicitStateContainers()
    {
        var xaml = LoadAssetsBaseDisksViewXaml();

        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksActionsRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksStatusRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksListRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksDetailsRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksMetadataEditRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksLoadingStatePanel"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksEmptyStatePanel"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksErrorStatePanel"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksRefreshButton"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksImportButton"));
        Assert.NotNull(FindByName(xaml, "AssetsBaseDisksRemoveButton"));
    }

    [Fact]
    public void Aj4_HardensAssetsBaseDisksStateContinuity_AndActionableMessaging()
    {
        var source = LoadMainWindowSource();
        var nativeFileDialogsSource = LoadNativeFileDialogsSource();
        var capabilitySource = LoadAssetsBaseDisksCapabilityServiceSource();

        Assert.Contains("private bool IsAssetsBaseDisksActive =>", source);
        Assert.Contains("EnsureAssetsBaseDisksAsync", source);
        Assert.Contains("_pendingAssetsBaseDiskDraft", source);
        Assert.Contains("AssetsBaseDisksRefreshButton_Click", source);
        Assert.Contains("AssetsBaseDisksImportButton_Click", source);
        Assert.Contains("AssetsBaseDisksValidateButton_Click", source);
        Assert.Contains("AssetsBaseDisksSaveMetadataButton_Click", source);
        Assert.Contains("AssetsBaseDisksRemoveButton_Click", source);
        Assert.Contains("ShowAssetsBaseDiskRemoveConfirmationDialogAsync", source);
        Assert.Contains("FormatAssetsBaseDiskValidationText", source);
        Assert.Contains("IAssetsBaseDisksCapabilityService", source);
        Assert.Contains("ShowOpenVhdxDialog", nativeFileDialogsSource);
        Assert.Contains("Active runtime consumer detection is not currently implemented.", capabilitySource);
        Assert.Contains("Base disk removed from the registry.", capabilitySource);
        Assert.Contains("Validate and Save Metadata", source);
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

    private static string LoadMainWindowXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadNativeFileDialogsSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Interop", "NativeFileDialogs.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsBaseDisksCapabilityServiceSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.Business", "Assets", "AssetsBaseDisksCapabilityService.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XDocument LoadAssetsBaseDisksViewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsBaseDisksView.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
