using System.Xml.Linq;
using LabAssistant.Business.Planning;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class Issue755TemplatesV2BuilderTests
{
    [Fact]
    public void ShellViewModel_ResolvesTemplatesBuilder_AsDistinctWorkflowRoute()
    {
        var shell = new ShellViewModel();

        Assert.True(shell.TryResolveRoute(ShellRouteKeys.TemplatesBuilder, out var capability, out var subview));
        Assert.Equal("templates", capability.Key);
        Assert.Equal("Builder", subview.DisplayName);
        Assert.Equal(ShellRouteKeys.TemplatesLibrary, capability.DefaultSubview.RouteKey);
    }

    [Fact]
    public void TemplatesLibrary_ExposesBuilderEntryPoints_WhilePreservingEditorActions()
    {
        var library = LoadXaml(Path.Combine("Views", "Templates", "TemplatesLibraryView.xaml"));

        Assert.Equal("Open in Editor", FindByName(library, "OpenTemplateInEditorButton").Attribute("Content")?.Value);
        Assert.Equal("Create New", FindByName(library, "CreateTemplateButton").Attribute("Content")?.Value);
        Assert.Equal("Open in Builder", FindByName(library, "OpenTemplateInBuilderButton").Attribute("Content")?.Value);
        Assert.Equal("Create V2 Builder", FindByName(library, "CreateV2BuilderTemplateButton").Attribute("Content")?.Value);
    }

    [Fact]
    public void MainWindow_HostsBuilderRoute_WithoutOwningBuilderWorkflowState()
    {
        var mainWindowXaml = LoadXaml("MainWindow.xaml");
        var mainWindowSource = File.ReadAllText(WinUIPath("MainWindow.xaml.cs"));

        Assert.NotNull(FindByName(mainWindowXaml, "TemplatesBuilderViewHost"));
        Assert.Contains("IsTemplatesBuilderActive", mainWindowSource);
        Assert.DoesNotContain("TemplatesBuilderWorkspaceViewModel", mainWindowSource);
        Assert.DoesNotContain("TemplatesBuilderWorkspaceController", mainWindowSource);
    }

    [Fact]
    public async Task BuilderDraftMapper_ProducesPlannerCompatibleV2Template()
    {
        var referenceData = new TemplatesBuilderReferenceData(
            ["vSwitch-Core"],
            [
                new TemplateVhdxCatalogOption("disk-dc", @"C:\base\disk-dc.vhdx", "Windows Server", "2022", 2, "sig-dc"),
                new TemplateVhdxCatalogOption("disk-member", @"C:\base\disk-member.vhdx", "Windows Server", "2022", 2, "sig-member")
            ]);
        var draft = TemplatesBuilderDraftMapper.CreateSuggestedDraft(referenceData) with
        {
            IsSaveConfirmed = true
        };

        var build = TemplatesBuilderDraftMapper.BuildDocument(
            draft,
            templateId: "template-v2-builder",
            templateRevision: 1,
            createdWithAppVersion: "1.0.0",
            sourceFilePath: null);

        Assert.Empty(build.Errors);
        var document = Assert.IsType<TemplateEditorDocument>(build.Document);
        Assert.Equal(TemplateSchemaVersionCatalog.V2SchemaVersion, document.Template.SchemaVersion);
        Assert.Equal(TemplateExecutionEngine.V2UnifiedPlanning, document.Template.ExecutionEngine);
        Assert.NotNull(document.Template.DirectoryTopology);
        Assert.Null(document.Template.DirectoryTopology!.Trusts);

        var planner = new V2PlanningCapabilityService();
        var plan = await planner.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = document.Template,
            CatalogItems =
            [
                CreateCatalogItem("disk-dc", @"C:\base\disk-dc.vhdx", "sig-dc"),
                CreateCatalogItem("disk-member", @"C:\base\disk-member.vhdx", "sig-member")
            ],
            AvailableSwitchNames = ["vSwitch-Core"],
            AvailableSwitches = [new V2AvailableSwitchInfo { Name = "vSwitch-Core", SwitchType = "Internal" }],
            ResolvedCredentialSlotKeys = ["slot-local", "slot-admin", "slot-join", "slot-dsrm"],
            DefaultDeploymentProfile = "Balanced"
        });

        Assert.True(plan.Success);
        Assert.True(plan.Context.DomainSemanticsRequired);
        var nodeKinds = plan.Nodes.Select(node => node.Kind).ToArray();
        Assert.Contains(V2PlanNodeKind.PromoteFirstDomainController, nodeKinds);
        Assert.Contains(V2PlanNodeKind.JoinDomain, nodeKinds);
    }

    private static VhdxCatalogItem CreateCatalogItem(string id, string path, string signature)
    {
        return new VhdxCatalogItem
        {
            Id = id,
            Path = path,
            OsName = "Windows Server",
            OsVersion = "2022",
            Generation = 2,
            Signature = signature,
            BootstrapProfile = new VhdxBootstrapProfile
            {
                ExpectedLocalUser = "Administrator",
                LocalCredentialSlotRef = "slot-local",
                GuestOsFamily = "windows",
                GuestTransport = "powershell-direct"
            }
        };
    }

    private static XDocument LoadXaml(string relativePath)
        => XDocument.Load(WinUIPath(relativePath));

    private static string WinUIPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "LabAssistant.WinUI",
            relativePath));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }
}
