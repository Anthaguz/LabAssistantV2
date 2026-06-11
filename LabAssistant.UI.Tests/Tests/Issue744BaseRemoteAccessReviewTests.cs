using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class Issue744BaseRemoteAccessReviewTests
{
    [Fact]
    public void DeployFromTemplateView_DefinesBaseRemoteAccessReviewControls()
    {
        var xaml = LoadXaml("DeployFromTemplateView.xaml");

        Assert.NotNull(FindByName(xaml, "DeployV2EnableRemoteDesktopCheckBox"));
        Assert.NotNull(FindByName(xaml, "DeployV2SetPrivateNetworkProfileCheckBox"));
        Assert.NotNull(FindByName(xaml, "DeployV2DisableFirewallCheckBox"));
        Assert.NotNull(FindByName(xaml, "DeployV2DisableRdpNlaCheckBox"));
    }

    [Fact]
    public void DeployFromTemplateComposition_WiresBaseRemoteAccessIntoV2Deploy()
    {
        var source = File.ReadAllText(GetCompositionPath());

        Assert.Contains("V2BaseRemoteAccessOptionsChanged", source);
        Assert.Contains("UpdateBaseRemoteAccessOptions", source);
        Assert.Contains("ApplyV2BaseRemoteAccessState", source);
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
