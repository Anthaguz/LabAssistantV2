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

    [Fact]
    public async Task BuilderSave_EditsAfterConfirmation_BlockSaveUntilReconfirmed()
    {
        var workspace = new TemplatesBuilderWorkspaceViewModel();
        var service = new RecordingTemplatesCapabilityService();
        var host = new RecordingBuilderHost();
        var controller = new TemplatesBuilderWorkspaceController(service, workspace, host);
        var draft = CreateConfirmedBuilderDraft();

        workspace.LoadNewDraft(draft with { IsSaveConfirmed = false });
        workspace.ApplyDraft(draft);
        workspace.ApplyDraft(workspace.CaptureDraft() with
        {
            TemplateDescription = "Changed after confirmation.",
            IsSaveConfirmed = true
        });

        await controller.SaveAsync();

        Assert.Equal(0, service.SaveCalls);
        Assert.False(workspace.IsSaveConfirmed);
        Assert.Contains("Confirm the visible Builder draft before saving.", workspace.StatusText);

        workspace.ApplyDraft(workspace.CaptureDraft() with { IsSaveConfirmed = true });

        await controller.SaveAsync();

        Assert.Equal(1, service.SaveCalls);
    }

    [Fact]
    public async Task BuilderSave_PreservesExistingTrustsDuringRoundTrip()
    {
        var workspace = new TemplatesBuilderWorkspaceViewModel();
        var service = new RecordingTemplatesCapabilityService();
        var host = new RecordingBuilderHost();
        var controller = new TemplatesBuilderWorkspaceController(service, workspace, host);
        var sourceTemplate = CreateTemplateWithTrust();

        workspace.LoadDocument(new TemplateEditorDocument
        {
            Template = sourceTemplate,
            SourceFilePath = @"C:\templates\v2-with-trust.json"
        });
        workspace.ApplyDraft(workspace.CaptureDraft() with { TemplateDescription = "Updated visible fields." });
        workspace.ApplyDraft(workspace.CaptureDraft() with { IsSaveConfirmed = true });

        await controller.SaveAsync();

        Assert.Equal(1, service.SaveCalls);
        var savedTrust = Assert.Single(service.LastSavedDocument!.Template.DirectoryTopology!.Trusts!);
        var sourceTrust = Assert.Single(sourceTemplate.DirectoryTopology!.Trusts!);
        Assert.NotSame(sourceTrust, savedTrust);
        Assert.Equal("trust-contoso-fabrikam", savedTrust.TrustId);
        Assert.Equal("domain-contoso", savedTrust.SourceDomainId);
        Assert.Equal("domain-fabrikam", savedTrust.TargetDomainId);
        Assert.Equal(V2TrustType.Forest, savedTrust.TrustType);
        Assert.Equal(V2TrustDirection.Bidirectional, savedTrust.Direction);
    }

    private static TemplatesBuilderDraftSnapshot CreateConfirmedBuilderDraft()
    {
        var referenceData = new TemplatesBuilderReferenceData(
            ["vSwitch-Core"],
            [
                new TemplateVhdxCatalogOption("disk-dc", @"C:\base\disk-dc.vhdx", "Windows Server", "2022", 2, "sig-dc"),
                new TemplateVhdxCatalogOption("disk-member", @"C:\base\disk-member.vhdx", "Windows Server", "2022", 2, "sig-member")
            ]);

        return TemplatesBuilderDraftMapper.CreateSuggestedDraft(referenceData) with { IsSaveConfirmed = true };
    }

    private static LabTemplate CreateTemplateWithTrust()
    {
        return new LabTemplate
        {
            Id = "template-with-trust",
            Name = "Template With Trust",
            Description = "Existing V2 template with trust intent.",
            SchemaVersion = TemplateSchemaVersionCatalog.V2SchemaVersion,
            TemplateRevision = 1,
            CreatedWithAppVersion = "1.0.0",
            TemplateType = LabTemplate.SupportedTemplateType,
            DeploymentProfile = "Balanced",
            ExecutionEngine = TemplateExecutionEngine.V2UnifiedPlanning,
            LabNetworks =
            [
                new LabNetworkTemplate
                {
                    NetworkId = "lab-core",
                    Name = "Core",
                    SwitchName = "vSwitch-Core",
                    Subnet = "10.0.0.0/24"
                }
            ],
            DirectoryTopology = new V2DirectoryTopologyTemplate
            {
                Forests =
                [
                    new V2ForestTemplate
                    {
                        ForestId = "forest-contoso",
                        RootDomainId = "domain-contoso"
                    },
                    new V2ForestTemplate
                    {
                        ForestId = "forest-fabrikam",
                        RootDomainId = "domain-fabrikam"
                    }
                ],
                Domains =
                [
                    new V2DomainTemplate
                    {
                        DomainId = "domain-contoso",
                        DnsName = "contoso.com",
                        NetBiosName = "CONTOSO",
                        ForestId = "forest-contoso",
                        RelationKind = V2DomainRelationKind.Root,
                        FirstDomainControllerVmId = "vm-contoso-dc"
                    },
                    new V2DomainTemplate
                    {
                        DomainId = "domain-fabrikam",
                        DnsName = "fabrikam.com",
                        NetBiosName = "FABRIKAM",
                        ForestId = "forest-fabrikam",
                        RelationKind = V2DomainRelationKind.Root,
                        FirstDomainControllerVmId = "vm-fabrikam-dc"
                    }
                ],
                Trusts =
                [
                    new V2TrustTemplate
                    {
                        TrustId = "trust-contoso-fabrikam",
                        SourceDomainId = "domain-contoso",
                        TargetDomainId = "domain-fabrikam",
                        TrustType = V2TrustType.Forest,
                        Direction = V2TrustDirection.Bidirectional
                    }
                ]
            },
            VmTemplates =
            {
                new VmTemplate
                {
                    VmId = "vm-contoso-dc",
                    Name = "contoso-dc",
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = "disk-dc",
                    TopologyRole = "FirstDomainController",
                    DomainId = "domain-contoso"
                },
                new VmTemplate
                {
                    VmId = "vm-fabrikam-dc",
                    Name = "fabrikam-dc",
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = "disk-member",
                    TopologyRole = "FirstDomainController",
                    DomainId = "domain-fabrikam"
                }
            }
        };
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

    private sealed class RecordingTemplatesCapabilityService : ITemplatesCapabilityService
    {
        public int SaveCalls { get; private set; }

        public TemplateEditorDocument? LastSavedDocument { get; private set; }

        public Task<TemplateLibraryLoadResult> LoadLibraryAsync(string? searchText = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateLibraryLoadResult());

        public Task<TemplateEditorDocument> CreateDraftAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateEditorDocument());

        public Task<TemplateEditorDocument> LoadForEditorAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateEditorDocument());

        public Task<TemplatesVhdxCatalogLoadResult> LoadVhdxCatalogOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplatesVhdxCatalogLoadResult());

        public Task<TemplateOperationResult> SaveAsync(
            TemplateEditorDocument document,
            string? targetFilePath = null,
            bool saveAs = false,
            CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            LastSavedDocument = document;

            return Task.FromResult(new TemplateOperationResult
            {
                Success = true,
                OperationId = "operation-save",
                UserMessage = "Saved.",
                FilePath = targetFilePath ?? document.SourceFilePath ?? @"C:\templates\saved.json"
            });
        }

        public Task<TemplateValidationSummaryResult> ValidateAsync(TemplateEditorDocument document, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateValidationSummaryResult { IsValid = true });

        public Task<TemplateOperationResult> DeleteAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateOperationResult());

        public Task<TemplateOperationResult> ImportAsync(string sourceFilePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateOperationResult());

        public Task<TemplateOperationResult> ExportAsync(
            string sourceFilePath,
            string destinationFilePath,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateOperationResult());
    }

    private sealed class RecordingBuilderHost : ITemplatesBuilderWorkspaceControllerHost
    {
        public bool IsTemplatesLoading { get; private set; }

        public void SetTemplatesLoading(bool isLoading) => IsTemplatesLoading = isLoading;

        public void ApplyWorkspaceState()
        {
        }

        public Task EnsureTemplatesLibraryAsync(bool forceRefresh) => Task.CompletedTask;

        public Task<TemplatesBuilderReferenceData> LoadReferenceDataAsync(bool forceRefresh)
            => Task.FromResult(new TemplatesBuilderReferenceData(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>()));

        public Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName)
            => Task.FromResult<string?>(@"C:\templates\save-as.json");

        public void NavigateToBuilder()
        {
        }

        public void NavigateToLibrary()
        {
        }
    }
}
