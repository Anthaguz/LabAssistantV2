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
    public void MainWindow_DefinesTemplatesScaffoldHosts_AndLocalSubNavigation()
    {
        var xaml = LoadMainWindowXaml();

        Assert.NotNull(FindByName(xaml, "TemplatesLocalNavigationPanel"));
        Assert.NotNull(FindByName(xaml, "TemplatesSubviewTabView"));
        Assert.NotNull(FindByName(xaml, "TemplatesLibraryTabViewItem"));
        Assert.NotNull(FindByName(xaml, "TemplatesEditorTabViewItem"));
        Assert.NotNull(FindByName(xaml, "TemplatesLibraryViewHost"));
        Assert.NotNull(FindByName(xaml, "TemplatesEditorViewHost"));
    }

    [Fact]
    public void MainWindow_WiresTemplatesLocalNavigation_WithoutChangingGlobalFooterContract()
    {
        var source = LoadMainWindowSource();

        Assert.Contains("TemplatesSubviewTabView_SelectionChanged", source);
        Assert.Contains("SyncTemplatesSubviewSelection()", source);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.TemplatesLibrary);", source);
        Assert.Contains("NavigateToRoute(ShellRouteKeys.TemplatesEditor);", source);
        Assert.Contains("GlobalNavigationView.FooterMenuItems.Add(parentItem);", source);
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
        Assert.Contains("_templatesCapabilityService.SaveAsync", source);
        Assert.Contains("_templatesCapabilityService.ImportAsync", source);
        Assert.Contains("_templatesCapabilityService.ExportAsync", source);
        Assert.Contains("_templatesCapabilityService.DeleteAsync", source);
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
        Assert.NotNull(FindByName(editorXaml, "SaveTemplateButton"));
        Assert.NotNull(FindByName(editorXaml, "SaveTemplateAsButton"));
        Assert.NotNull(FindByName(editorXaml, "ValidateTemplateButton"));
        Assert.NotNull(FindByName(editorXaml, "TemplateEditorStatusTextBlock"));
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
