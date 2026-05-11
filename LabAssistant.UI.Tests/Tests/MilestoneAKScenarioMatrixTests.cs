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
        Assert.Contains("private FrameworkElement AssetsSwitchesPanel => AssetsSwitchesViewHost;", source);
        Assert.Contains("AssetsSwitchesPanel.Visibility = IsAssetsSwitchesActive ? Visibility.Visible : Visibility.Collapsed;", source);
        Assert.Contains("private bool IsAssetsSwitchesActive =>", source);
    }

    [Fact]
    public void AssetsSwitchesView_DefinesOperationalRegionsAndExplicitStateContainers()
    {
        var xaml = LoadAssetsSwitchesViewXaml();

        Assert.NotNull(FindByName(xaml, "AssetsSwitchesListRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesDetailsRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesEditRegion"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesStatusTextBlock"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesLoadingStatePanel"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesEmptyStatePanel"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesErrorStatePanel"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesRefreshButton"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesCreateButton"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesApplyButton"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesDeleteButton"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesTypeInfoButton"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesAttachedVmsHintTextBlock"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesAttachedVmsListView"));
        Assert.NotNull(FindByName(xaml, "AssetsSwitchesErrorStateTextBox"));
    }

    [Fact]
    public void Ak3_WiresSwitchOperations_WithExplicitDeleteGuardrails()
    {
        var source = LoadMainWindowSource();
        var viewSource = LoadAssetsSwitchesViewXamlSource();
        var capabilitySource = LoadAssetsSwitchesCapabilityServiceSource();
        var workspaceSource = LoadAssetsSwitchesWorkspaceSource();
        var controllerSource = LoadAssetsSwitchesControllerSource();
        var editorWorkflowSource = LoadAssetsSwitchesEditorWorkflowSource();
        var compositionSource = LoadAssetsSwitchesCompositionSource();

        Assert.Contains("ShowAssetsSwitchDeleteConfirmationDialogAsync", source);
        Assert.Contains("IAssetsSwitchesCapabilityService", source);
        Assert.Contains("_assetsSwitchesWorkspaceComposition = new AssetsSwitchesWorkspaceComposition(", source);
        Assert.Contains("RefreshRequested += AssetsSwitchesRefreshRequested;", compositionSource);
        Assert.Contains("CreateRequested += AssetsSwitchesCreateRequested;", compositionSource);
        Assert.Contains("ApplyRequested += AssetsSwitchesApplyRequested;", compositionSource);
        Assert.Contains("DeleteRequested += AssetsSwitchesDeleteRequested;", compositionSource);
        Assert.Contains("SelectedSwitchChanged += AssetsSwitchesListView_SelectionChanged;", compositionSource);
        Assert.Contains("EditorChanged += AssetsSwitchesEditorChanged;", compositionSource);
        Assert.Contains("HandleSelectionChangedAsync", controllerSource);
        Assert.Contains("SaveDraftAsync", controllerSource);
        Assert.Contains("DeleteSelectedAsync", controllerSource);
        Assert.Contains("private readonly AssetsSwitchesEditorWorkflow _editorWorkflow;", controllerSource);
        Assert.Contains("_editorWorkflow = new AssetsSwitchesEditorWorkflow(capabilityService, workspace, host);", controllerSource);
        Assert.Contains("internal sealed class AssetsSwitchesEditorWorkflow", editorWorkflowSource);
        Assert.Contains("ApplyDeleteAssessment", editorWorkflowSource);
        Assert.Contains("public bool HasErrorState { get; set; }", workspaceSource);
        Assert.Contains("Delete is allowed only when no Hyper-V VM is attached to the switch.", source);
        Assert.Contains("Delete is blocked because at least one VM is attached to this switch.", capabilitySource);
        Assert.Contains("Switch type changes are not supported. Create a new switch instead.", capabilitySource);
        Assert.Contains("External adapter rebinding is not supported here. Create a new switch instead.", capabilitySource);
        Assert.Contains("External: binds the switch to a host network adapter for outside connectivity.", viewSource);
    }

    [Fact]
    public void Ak4_HardensSwitchDraftAndStateVisibilityPaths()
    {
        var source = LoadMainWindowSource();
        var viewSource = LoadAssetsSwitchesViewXamlSource();
        var workspaceSource = LoadAssetsSwitchesWorkspaceSource();
        var controllerSource = LoadAssetsSwitchesControllerSource();
        var editorWorkflowSource = LoadAssetsSwitchesEditorWorkflowSource();
        var compositionSource = LoadAssetsSwitchesCompositionSource();

        Assert.Contains("private readonly AssetsSwitchesWorkspaceComposition _assetsSwitchesWorkspaceComposition;", source);
        Assert.Contains("internal sealed class AssetsSwitchesWorkspaceComposition : IAssetsSwitchesWorkspaceHost", compositionSource);
        Assert.Contains("private readonly AssetsSwitchesView _view;", compositionSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceViewModel _workspace = new();", compositionSource);
        Assert.Contains("private readonly AssetsSwitchesWorkspaceController _controller;", compositionSource);
        Assert.Contains("CaptureDraft(bool isNewOverride)", compositionSource);
        Assert.Contains("_view.UpdateWorkspaceState(BuildViewState(workspace, canValidateOrApply, isExternalSwitchTypeSelected));", compositionSource);
        Assert.Contains("internal sealed class AssetsSwitchesWorkspaceController", controllerSource);
        Assert.Contains("private readonly AssetsSwitchesEditorWorkflow _editorWorkflow;", controllerSource);
        Assert.Contains("RefreshValidationAsync", editorWorkflowSource);
        Assert.Contains("LoadAttachedVmNamesAsync", editorWorkflowSource);
        Assert.Contains("SetAttachedVmState", editorWorkflowSource);
        Assert.Contains("ClearErrorState", editorWorkflowSource);
        Assert.Contains("DataContext = _presentation;", LoadAssetsSwitchesViewCodeBehindSource());
        Assert.Contains("_presentation.Apply(state);", LoadAssetsSwitchesViewCodeBehindSource());
        Assert.DoesNotContain("AssetsSwitchesRefreshButton.IsEnabled =", LoadAssetsSwitchesViewCodeBehindSource());
        Assert.DoesNotContain("AssetsSwitchesStatusTextBlock.Text =", LoadAssetsSwitchesViewCodeBehindSource());
        Assert.DoesNotContain("AssetsSwitchesErrorStatePanel.Visibility =", LoadAssetsSwitchesViewCodeBehindSource());
        Assert.Contains("public AssetsSwitchDraft? PendingDraft { get; set; }", workspaceSource);
        Assert.Contains("public int ValidationRequestVersion { get; set; }", workspaceSource);
        Assert.Contains("public int AssessmentRequestVersion { get; set; }", workspaceSource);
        Assert.Contains("The current new-switch draft was preserved.", editorWorkflowSource);
        Assert.Contains("Delete blocked. Disconnect the attached VMs from this switch and refresh before trying again.", controllerSource);
        Assert.Contains("Attached VMs currently using this switch.", editorWorkflowSource);
        Assert.Contains("Select a switch or click New to begin.", LoadAssetsSwitchesViewCodeBehindSource());
        Assert.Contains("IsEnabled=\"{Binding CanRefresh, Mode=OneWay}\"", viewSource);
        Assert.Contains("Visibility=\"{Binding ErrorStateVisibility, Mode=OneWay}\"", viewSource);
        Assert.Contains("Text=\"{Binding ErrorStateText, Mode=OneWay}\"", viewSource);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility=\"Auto\"", viewSource);
        Assert.DoesNotContain("AssetsSwitchesActionsRegion", viewSource);
        Assert.DoesNotContain("AssetsSwitchesStatusRegion", viewSource);
        Assert.Contains("ToolTipService.ToolTip=\"Create a new virtual switch\"", viewSource);
    }

    [Fact]
    public void Ak5_ClosureEvidenceProtectsFullMilestoneChain()
    {
        var source = LoadMainWindowSource();
        var viewSource = LoadAssetsSwitchesViewXamlSource();
        var capabilitySource = LoadAssetsSwitchesCapabilityServiceSource();
        var workspaceSource = LoadAssetsSwitchesWorkspaceSource();
        var controllerSource = LoadAssetsSwitchesControllerSource();
        var editorWorkflowSource = LoadAssetsSwitchesEditorWorkflowSource();
        var compositionSource = LoadAssetsSwitchesCompositionSource();

        Assert.Contains("public const string AssetsSwitches = \"assets.switches\";", LoadShellViewModelSource());
        Assert.Contains("AssetsSwitchesPanel.Visibility = IsAssetsSwitchesActive ? Visibility.Visible : Visibility.Collapsed;", source);

        Assert.NotNull(FindByName(LoadAssetsSwitchesViewXaml(), "AssetsSwitchesListRegion"));
        Assert.NotNull(FindByName(LoadAssetsSwitchesViewXaml(), "AssetsSwitchesDetailsRegion"));
        Assert.NotNull(FindByName(LoadAssetsSwitchesViewXaml(), "AssetsSwitchesEditRegion"));
        Assert.NotNull(FindByName(LoadAssetsSwitchesViewXaml(), "AssetsSwitchesLoadingStatePanel"));
        Assert.NotNull(FindByName(LoadAssetsSwitchesViewXaml(), "AssetsSwitchesEmptyStatePanel"));
        Assert.NotNull(FindByName(LoadAssetsSwitchesViewXaml(), "AssetsSwitchesErrorStatePanel"));

        Assert.Contains("IAssetsSwitchesCapabilityService", source);
        Assert.Contains("public void ApplyShellState()", compositionSource);
        Assert.Contains("public Task EnsureInventoryAsync(bool forceRefresh)", compositionSource);
        Assert.Contains("public async Task EnsureInventoryAsync(bool forceRefresh)", controllerSource);
        Assert.Contains("public async Task BeginCreateAsync()", controllerSource);
        Assert.Contains("public async Task SaveDraftAsync()", controllerSource);
        Assert.Contains("public async Task DeleteSelectedAsync()", controllerSource);
        Assert.Contains("Delete is blocked because at least one VM is attached to this switch.", capabilitySource);
        Assert.Contains("Switch type changes are not supported. Create a new switch instead.", capabilitySource);
        Assert.Contains("External adapter rebinding is not supported here. Create a new switch instead.", capabilitySource);

        Assert.Contains("public AssetsSwitchDraft? PendingDraft { get; set; }", workspaceSource);
        Assert.Contains("RefreshValidationAsync", editorWorkflowSource);
        Assert.Contains("LoadAttachedVmNamesAsync", editorWorkflowSource);
        Assert.Contains("public bool HasErrorState { get; set; }", workspaceSource);
        Assert.Contains("Attached VMs currently using this switch.", editorWorkflowSource);
        Assert.Contains("internal sealed class AssetsSwitchesViewPresentationModel : INotifyPropertyChanged", LoadAssetsSwitchesViewCodeBehindSource());
        Assert.DoesNotContain("Delete eligibility is checked when you click Delete.", source);
        Assert.Contains("ContentTitleTextBlock.Text = _activeCapability.DisplayName;", source);
        Assert.Contains("Manage shared Hyper-V assets, inventory, and compatibility state from one capability surface.", source);
        Assert.DoesNotContain("Assets / Virtual Switches", viewSource);
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

    private static string LoadAssetsSwitchesWorkspaceSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "Switches", "AssetsSwitchesWorkspaceViewModel.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsSwitchesControllerSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "Switches", "AssetsSwitchesWorkspaceController.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsSwitchesEditorWorkflowSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "Switches", "AssetsSwitchesEditorWorkflow.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsSwitchesCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Assets", "Switches", "AssetsSwitchesWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadAssetsSwitchesViewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Assets", "AssetsSwitchesView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name));
    }
}
