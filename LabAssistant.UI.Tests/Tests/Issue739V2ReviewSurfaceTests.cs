using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class Issue739V2ReviewSurfaceTests
{
    [Fact]
    public void DeployFromTemplateView_DefinesV2ReviewControls()
    {
        var xaml = LoadXaml("DeployFromTemplateView.xaml");

        Assert.NotNull(FindByName(xaml, "DeployV2ReviewPanel"));
        Assert.NotNull(FindByName(xaml, "DeployV2BlockersListView"));
        Assert.NotNull(FindByName(xaml, "DeployV2CredentialSlotsListView"));
        Assert.NotNull(FindByName(xaml, "DeployV2SaveCredentialSlotButton"));
        Assert.NotNull(FindByName(xaml, "DeployV2WavesListView"));
        Assert.NotNull(FindByName(xaml, "DeployV2DiagnosticsListView"));
    }

    [Fact]
    public void DeployFromTemplateComposition_WiresV2ReviewControllerAndSlotSaveFlow()
    {
        var source = File.ReadAllText(GetCompositionPath());

        Assert.Contains("DeployV2ReviewWorkspaceViewModel", source);
        Assert.Contains("DeployV2ReviewWorkspaceController", source);
        Assert.Contains("BuildV2PlanAsync", source);
        Assert.Contains("SaveSelectedCredentialSlotAsync", source);
        Assert.Contains("StartV2DeployAsync", source);
    }

    private static XDocument LoadXaml(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "Views", "Deploy", fileName);
        return XDocument.Load(path);
    }

    private static string GetCompositionPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "ViewModels", "Deploy", "FromTemplate", "DeployFromTemplateWorkspaceComposition.cs");
    }

    private static XElement? FindByName(XDocument xaml, string name)
    {
        return xaml.Descendants().FirstOrDefault(element =>
            string.Equals(element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value, name, StringComparison.Ordinal));
    }
}
