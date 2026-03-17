using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneAEScenarioMatrixTests
{
    [Fact]
    public void ShellViewModel_PreservesTemplatesRoutes_AndGlobalNavigationContract()
    {
        var source = LoadShellViewModelSource();

        Assert.Contains("public const string TemplatesLibrary = \"templates.library\";", source);
        Assert.Contains("public const string TemplatesEditor = \"templates.editor\";", source);
        Assert.Contains("public const string SettingsGeneral = \"settings.general\";", source);
        Assert.Contains("key: \"templates\"", source);
    }

    [Fact]
    public void TemplatesEditorView_DefinesSwitchAndVhdxSelectorControls()
    {
        var xaml = LoadTemplatesEditorViewXaml();

        Assert.NotNull(FindByName(xaml, "TemplateVmSwitchRowsPanel"));
        Assert.NotNull(FindByName(xaml, "AddTemplateVmSwitchRowButton"));
        Assert.NotNull(FindByName(xaml, "TemplateVmSwitchGuidanceTextBlock"));
        Assert.NotNull(FindByName(xaml, "TemplateVmVhdxCatalogComboBox"));
        Assert.NotNull(FindByName(xaml, "TemplateVmVhdxGuidanceTextBlock"));
        Assert.NotNull(FindByName(xaml, "TemplateVmVhdxIdTextBox"));
        Assert.NotNull(FindByName(xaml, "TemplateVmVhdPathTextBox"));
        Assert.NotNull(FindByName(xaml, "TemplateVmVhdxSignatureTextBox"));
    }

    [Fact]
    public void MainWindow_DefinesSwitchSelectorRows_ValidationAndGuidancePaths()
    {
        var source = LoadMainWindowSource();
        var editorCompositionSource = LoadTemplatesEditorWorkspaceCompositionSource();
        var editorControllerSource = LoadTemplatesEditorWorkspaceControllerSource();
        var editorViewSource = LoadTemplatesEditorViewCodeBehindSource();

        Assert.Contains("TryValidateSelectedSwitches", editorControllerSource);
        Assert.Contains("Duplicate switch", editorControllerSource);
        Assert.Contains("No host switches available", editorCompositionSource);
        Assert.Contains("private string BuildSwitchGuidanceText(IReadOnlyList<string> selectedSwitches)", editorCompositionSource);
        Assert.Contains("AddTemplateVmSwitchRowButton.Click += AddTemplateVmSwitchRowButton_Click;", editorViewSource);
        Assert.Contains("private void RemoveTemplateVmSwitchRowButton_Click(object sender, RoutedEventArgs e)", editorViewSource);
        Assert.Contains("selectedVmEntry.SwitchNames = selectedSwitches.Count > 0 ? selectedSwitches : null;", editorControllerSource);
    }

    [Fact]
    public void TemplateSwitchPersistence_PreservesCanonicalAndLegacyCompatibilityRules()
    {
        var vmTemplateSource = LoadVmTemplateSource();
        var storeSource = LoadTemplateStoreSource();
        var validatorSource = LoadTemplateValidatorSource();

        Assert.Contains("public string? SwitchName { get; set; }", vmTemplateSource);
        Assert.Contains("public List<string>? SwitchNames { get; set; }", vmTemplateSource);
        Assert.Contains("if (canonical.Count == 0 && !string.IsNullOrWhiteSpace(vm.SwitchName))", storeSource);
        Assert.Contains("vm.SwitchNames = canonical.Count > 0 ? canonical : null;", storeSource);
        Assert.Contains("vm.SwitchName = canonical.Count > 0 ? canonical[0] : null;", storeSource);
        Assert.Contains("switchNames must not contain duplicates.", validatorSource);
    }

    [Fact]
    public void MainWindow_DefinesCatalogFirstVhdxSelector_WithLegacyGuidance()
    {
        var source = LoadMainWindowSource();
        var editorCompositionSource = LoadTemplatesEditorWorkspaceCompositionSource();
        var editorViewSource = LoadTemplatesEditorViewCodeBehindSource();

        Assert.Contains("EnsureTemplateVhdxCatalogOptionsAsync", source);
        Assert.Contains("SetEditorVmReferenceData(_templateAvailableSwitches, _templateVhdxCatalogOptions);", source);
        Assert.Contains("TemplateVmVhdxCatalogComboBox.SelectionChanged += TemplateVmVhdxCatalogComboBox_SelectionChanged;", editorViewSource);
        Assert.Contains("Legacy path-based reference loaded. Select a catalog entry to normalize.", editorCompositionSource);
        Assert.Contains("No catalog entries available. Import base disks in Assets > Base Disks.", editorCompositionSource);
    }

    [Fact]
    public void MainWindow_DefinesDeterministicNormalizationAndConflictBlockingPaths()
    {
        var source = LoadTemplatesEditorWorkspaceCompositionSource();
        var editorControllerSource = LoadTemplatesEditorWorkspaceControllerSource();

        Assert.Contains("EvaluateTemplateVhdxNormalization", source);
        Assert.Contains("if (idMatch is not null)", source);
        Assert.Contains("if (!string.IsNullOrWhiteSpace(vmTemplate.VhdxId))", source);
        Assert.Contains("if (signatureMatches.Count == 1)", source);
        Assert.Contains("if (pathMatch is not null)", source);
        Assert.Contains("Resolved from vhdxId.", source);
        Assert.Contains("Resolved from vhdxSignature.", source);
        Assert.Contains("Catalog entry '", source);
        Assert.Contains("VHD identity conflict detected. Select a catalog entry to resolve before saving.", source);
        Assert.Contains("Multiple catalog entries match vhdxSignature. Select one entry before saving.", source);
        Assert.Contains("_workspace.SetStatusText(_workspace.VmVhdxGuidanceText);", editorControllerSource);
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

    private static string LoadTemplatesEditorWorkspaceCompositionSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Templates", "TemplatesEditorWorkspaceComposition.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTemplatesEditorWorkspaceControllerSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Templates", "TemplatesEditorWorkspaceController.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTemplatesEditorViewCodeBehindSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Templates", "TemplatesEditorView.xaml.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadVmTemplateSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.Models", "Templates", "VmTemplate.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTemplateStoreSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.Data", "Templates", "LabTemplateStore.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static string LoadTemplateValidatorSource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.Models", "Validation", "LabTemplateValidator.cs");
        return File.ReadAllText(Path.GetFullPath(path));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
