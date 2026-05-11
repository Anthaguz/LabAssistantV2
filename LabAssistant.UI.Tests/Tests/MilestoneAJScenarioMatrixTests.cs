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
        var controllerSource = LoadAssetsBaseDisksWorkspaceControllerSource();
        var editorWorkflowSource = LoadAssetsBaseDisksEditorWorkflowSource();
        var compositionSource = LoadAssetsBaseDisksWorkspaceCompositionSource();
        var viewSource = LoadAssetsBaseDisksViewCodeBehindSource();
        var nativeFileDialogsSource = LoadNativeFileDialogsSource();
        var capabilitySource = LoadAssetsBaseDisksCapabilityServiceSource();
        var xamlSource = LoadAssetsBaseDisksViewXamlSource();

        Assert.Contains("private bool IsAssetsBaseDisksActive =>", source);
        Assert.Contains("private readonly AssetsBaseDisksWorkspaceComposition _assetsBaseDisksWorkspaceComposition;", source);
        Assert.Contains("_assetsBaseDisksWorkspaceComposition = new AssetsBaseDisksWorkspaceComposition(", source);
        Assert.Contains("new AssetsBaseDisksCompositionHost(", source);
        Assert.DoesNotContain("AssetsBaseDisksRefreshButton_Click", source);
        Assert.DoesNotContain("AssetsBaseDisksImportButton_Click", source);
        Assert.DoesNotContain("AssetsBaseDisksValidateButton_Click", source);
        Assert.DoesNotContain("AssetsBaseDisksSaveMetadataButton_Click", source);
        Assert.DoesNotContain("AssetsBaseDisksRemoveButton_Click", source);
        Assert.DoesNotContain("private async Task EnsureAssetsBaseDisksAsync(bool forceRefresh)", source);
        Assert.DoesNotContain("private void UpdateAssetsBaseDisksUi()", source);
        Assert.Contains("public event EventHandler? RefreshRequested;", viewSource);
        Assert.Contains("public event EventHandler? ImportRequested;", viewSource);
        Assert.Contains("public event EventHandler? ValidateRequested;", viewSource);
        Assert.Contains("public event EventHandler? SaveMetadataRequested;", viewSource);
        Assert.Contains("public event EventHandler? RemoveRequested;", viewSource);
        Assert.Contains("public event EventHandler? BrowsePathRequested;", viewSource);
        Assert.Contains("public event EventHandler? SelectedBaseDiskChanged;", viewSource);
        Assert.Contains("public event EventHandler? MetadataChanged;", viewSource);
        Assert.Contains("_view.RefreshRequested += AssetsBaseDisksRefreshRequested;", compositionSource);
        Assert.Contains("_view.ImportRequested += AssetsBaseDisksImportRequested;", compositionSource);
        Assert.Contains("_view.ValidateRequested += AssetsBaseDisksValidateRequested;", compositionSource);
        Assert.Contains("_view.SaveMetadataRequested += AssetsBaseDisksSaveMetadataRequested;", compositionSource);
        Assert.Contains("_view.RemoveRequested += AssetsBaseDisksRemoveRequested;", compositionSource);
        Assert.Contains("ShowRemoveConfirmationDialogAsync", controllerSource);
        Assert.Contains("private readonly AssetsBaseDisksEditorWorkflow _editorWorkflow;", controllerSource);
        Assert.Contains("_editorWorkflow = new AssetsBaseDisksEditorWorkflow(capabilityService, workspace, host);", controllerSource);
        Assert.Contains("internal sealed class AssetsBaseDisksEditorWorkflow", editorWorkflowSource);
        Assert.Contains("FormatValidationText", editorWorkflowSource);
        Assert.Contains("_workspace.PendingDraft", controllerSource);
        Assert.Contains("_workspace.HasErrorState", controllerSource);
        Assert.Contains("IAssetsBaseDisksCapabilityService", source);
        Assert.Contains("ShowOpenVhdxDialog", nativeFileDialogsSource);
        Assert.Contains("Active runtime consumer detection is not currently implemented.", capabilitySource);
        Assert.Contains("Base disk removed from the registry.", capabilitySource);
        Assert.Contains("Registry-only removal.", xamlSource);
        Assert.Contains("Validate and Save Metadata", editorWorkflowSource);
        Assert.DoesNotContain("public Button", viewSource);
        Assert.DoesNotContain("public TextBox", viewSource);
        Assert.DoesNotContain("public ListView", viewSource);
        Assert.DoesNotContain("public Border", viewSource);
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

    private static string LoadAssetsBaseDisksWorkspaceControllerSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "AssetsBaseDisksWorkspaceController.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsBaseDisksEditorWorkflowSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "AssetsBaseDisksEditorWorkflow.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsBaseDisksWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "AssetsBaseDisksWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XDocument LoadAssetsBaseDisksViewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsBaseDisksView.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static string LoadAssetsBaseDisksViewXamlSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsBaseDisksView.xaml");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsBaseDisksViewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsBaseDisksView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
