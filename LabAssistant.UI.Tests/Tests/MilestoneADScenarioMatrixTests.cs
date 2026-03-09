using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneADScenarioMatrixTests
{
    [Fact]
    public void ShellViewModel_DefinesTemplatesCanonicalRoutes_AndDefaultChild()
    {
        var source = LoadShellViewModelSource();

        Assert.Contains("public const string TemplatesLibrary = \"templates.library\";", source);
        Assert.Contains("public const string TemplatesEditor = \"templates.editor\";", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.TemplatesLibrary, \"Library\"", source);
        Assert.Contains("new ShellSubview(ShellRouteKeys.TemplatesEditor, \"Editor\"", source);
        Assert.Contains("DefaultSubview = subviews[0];", source);
    }

    [Fact]
    public void MainWindow_DefinesTemplatesLibraryAndEditorHosts_WithoutPeerTabs()
    {
        var xaml = LoadMainWindowXaml();

        Assert.NotNull(FindByName(xaml, "TemplatesWorkspacePanel"));
        Assert.NotNull(FindByName(xaml, "TemplatesLibraryViewHost"));
        Assert.NotNull(FindByName(xaml, "TemplatesEditorViewHost"));
        Assert.DoesNotContain("TemplatesSubviewTabView", xaml.ToString());
    }

    [Fact]
    public void MainWindow_TreatsTemplatesEditorAsWorkflowStateEntry_WithoutChangingGlobalFooterContract()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("TemplatesWorkspaceHost.Visibility = IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;", source);
        Assert.Contains("TemplatesLibraryViewHost.Visibility = IsTemplatesLibraryActive ? Visibility.Visible : Visibility.Collapsed;", source);
        Assert.Contains("TemplatesEditorViewHost.Visibility = IsTemplatesEditorActive ? Visibility.Visible : Visibility.Collapsed;", source);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.TemplatesLibrary);", source);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.TemplatesEditor);", source);
        Assert.Contains("if (!capability.ShowChildRoutesInShell)", source);
    }

    [Fact]
    public void MainWindow_WiresTemplatesOperations_ForLibraryAndEditorFlows()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("WireTemplatesHandlers()", source);
        Assert.Contains("OpenTemplateInEditorButton.Click += OpenTemplateInEditorButton_Click;", source);
        Assert.Contains("CreateTemplateButton.Click += CreateTemplateButton_Click;", source);
        Assert.Contains("DeleteTemplateButton.Click += DeleteTemplateButton_Click;", source);
        Assert.Contains("ImportTemplateButton.Click += ImportTemplateButton_Click;", source);
        Assert.Contains("ExportTemplateButton.Click += ExportTemplateButton_Click;", source);
        Assert.Contains("SaveTemplateButton.Click += SaveTemplateButton_Click;", source);
        Assert.Contains("SaveTemplateAsButton.Click += SaveTemplateAsButton_Click;", source);
        Assert.Contains("ValidateTemplateButton.Click += ValidateTemplateButton_Click;", source);
        Assert.Contains("BackToLibraryButton.Click += BackToLibraryButton_Click;", source);
        Assert.Contains("TemplateVmListView.SelectionChanged += TemplateVmListView_SelectionChanged;", source);
        Assert.Contains("AddTemplateVmButton.Click += AddTemplateVmButton_Click;", source);
        Assert.Contains("RemoveTemplateVmButton.Click += RemoveTemplateVmButton_Click;", source);
        Assert.Contains("ApplyTemplateVmChangesButton.Click += ApplyTemplateVmChangesButton_Click;", source);
        Assert.Contains("_templatesCapabilityService.SaveAsync", source);
        Assert.Contains("_templatesCapabilityService.ImportAsync", source);
        Assert.Contains("_templatesCapabilityService.ExportAsync", source);
        Assert.Contains("_templatesCapabilityService.DeleteAsync", source);
    }

    [Fact]
    public void MainWindow_PreservesLibraryEditorContinuity_WithoutFilesystemFirstFallback()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("OpenSelectedTemplateInEditorAsync()", source);
        Assert.Contains("await OpenTemplateInEditorAsync(_selectedTemplateLibraryItem, fromDeploy: false);", source);
        Assert.Contains("TemplateLibraryListView.SelectedItem = _selectedTemplateLibraryItem;", source);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.TemplatesEditor);", source);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.TemplatesLibrary);", source);
    }

    [Fact]
    public void MainWindow_UsesExistingTemplateVmSchemaFields_WithoutNewUiKeys()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("_selectedTemplateVmEntry.SwitchName", source);
        Assert.Contains("_selectedTemplateVmEntry.SwitchNames", source);
        Assert.Contains("_selectedTemplateVmEntry.VhdxId", source);
        Assert.Contains("_selectedTemplateVmEntry.VhdPath", source);
        Assert.Contains("_selectedTemplateVmEntry.VhdxSignature", source);
        Assert.DoesNotContain("OperatingSystem", source);
        Assert.DoesNotContain("DomainName", source);
    }

    [Fact]
    public void TemplateViews_ExposeOperationalControls_ForAd3()
    {
        var libraryXaml = LoadTemplatesLibraryViewXaml();
        var editorXaml = LoadTemplatesEditorViewXaml();

        Assert.NotNull(FindByName(libraryXaml, "TemplateLibraryListView"));
        Assert.NotNull(FindByName(libraryXaml, "OpenTemplateInEditorButton"));
        Assert.NotNull(FindByName(libraryXaml, "CreateTemplateButton"));
        Assert.NotNull(FindByName(libraryXaml, "DeleteTemplateButton"));
        Assert.NotNull(FindByName(libraryXaml, "ImportTemplateButton"));
        Assert.NotNull(FindByName(libraryXaml, "ExportTemplateButton"));
        Assert.NotNull(FindByName(libraryXaml, "TemplatesLibraryStatusTextBlock"));
        Assert.NotNull(FindByName(editorXaml, "TemplateNameTextBox"));
        Assert.NotNull(FindByName(editorXaml, "TemplateDescriptionTextBox"));
        Assert.NotNull(FindByName(editorXaml, "TemplateVmListView"));
        Assert.NotNull(FindByName(editorXaml, "AddTemplateVmButton"));
        Assert.NotNull(FindByName(editorXaml, "RemoveTemplateVmButton"));
        Assert.NotNull(FindByName(editorXaml, "TemplateVmNameTextBox"));
        Assert.NotNull(FindByName(editorXaml, "TemplateVmMemoryTextBox"));
        Assert.NotNull(FindByName(editorXaml, "TemplateVmCpuTextBox"));
        Assert.NotNull(FindByName(editorXaml, "TemplateVmSwitchRowsPanel"));
        Assert.NotNull(FindByName(editorXaml, "AddTemplateVmSwitchRowButton"));
        Assert.NotNull(FindByName(editorXaml, "TemplateVmSwitchGuidanceTextBlock"));
        Assert.NotNull(FindByName(editorXaml, "TemplateVmVhdxCatalogComboBox"));
        Assert.NotNull(FindByName(editorXaml, "TemplateVmVhdxGuidanceTextBlock"));
        Assert.NotNull(FindByName(editorXaml, "TemplateVmVhdxIdTextBox"));
        Assert.NotNull(FindByName(editorXaml, "TemplateVmVhdPathTextBox"));
        Assert.NotNull(FindByName(editorXaml, "TemplateVmVhdxSignatureTextBox"));
        Assert.NotNull(FindByName(editorXaml, "ApplyTemplateVmChangesButton"));
        Assert.NotNull(FindByName(editorXaml, "SaveTemplateButton"));
        Assert.NotNull(FindByName(editorXaml, "SaveTemplateAsButton"));
        Assert.NotNull(FindByName(editorXaml, "ValidateTemplateButton"));
        Assert.NotNull(FindByName(editorXaml, "TemplateEditorStatusTextBlock"));
        Assert.Contains(
            editorXaml.Descendants(),
            element => element.Name.LocalName == "ScrollViewer");
    }

    [Fact]
    public void MainWindow_ImplementsVmEntryParityFlow_WithSaveReloadGuards()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("RefreshTemplateVmEntriesFromDocument()", source);
        Assert.Contains("SyncTemplateVmEntriesToDocument()", source);
        Assert.Contains("TryApplySelectedTemplateVmFields(showSuccessStatus: false)", source);
        Assert.Contains("Remove VM Entry", source);
        Assert.Contains("Added VM entry", source);
        Assert.Contains("Removed VM entry", source);
    }

    [Fact]
    public void MainWindow_DefinesTemplateSwitchSelectorRows_AndValidationGuards()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("AddTemplateVmSwitchRowButton_Click", source);
        Assert.Contains("RemoveTemplateVmSwitchRowButton_Click", source);
        Assert.Contains("TryGetTemplateSelectedSwitches", source);
        Assert.Contains("Duplicate switch", source);
        Assert.Contains("No host switches available", source);
    }

    [Fact]
    public void MainWindow_DefinesTemplateVhdxCatalogSelector_AndUnresolvedGuidance()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("EnsureTemplateVhdxCatalogOptionsAsync", source);
        Assert.Contains("TemplateVmVhdxCatalogComboBox.SelectionChanged += TemplateVmVhdxCatalogComboBox_SelectionChanged;", source);
        Assert.Contains("Legacy path-based reference loaded. Select a catalog entry to normalize.", source);
        Assert.Contains("Catalog entry", source);
    }

    [Fact]
    public void MainWindow_DefinesDeterministicVhdxNormalizationPrecedence_AndConflictBlocking()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("EvaluateTemplateVhdxNormalization", source);
        Assert.Contains("Resolved from vhdxId.", source);
        Assert.Contains("Resolved from vhdxSignature.", source);
        Assert.Contains("Resolved from vhdPath.", source);
        Assert.Contains("VHD identity conflict detected. Select a catalog entry to resolve before saving.", source);
        Assert.Contains("Multiple catalog entries match vhdxSignature. Select one entry before saving.", source);
        Assert.Contains("Catalog entry '", source);
    }

    private static XDocument LoadMainWindowXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static XDocument LoadTemplatesLibraryViewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Templates", "TemplatesLibraryView.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static XDocument LoadTemplatesEditorViewXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Templates", "TemplatesEditorView.xaml");
        return XDocument.Load(Path.GetFullPath(path));
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

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
