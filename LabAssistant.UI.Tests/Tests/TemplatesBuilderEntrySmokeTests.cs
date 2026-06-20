using LabAssistant.Business.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class TemplatesBuilderEntrySmokeTests
{
    [Fact]
    public async Task BuilderCreateDraft_WithSparseReferenceData_DoesNotThrowAndNavigatesToBuilder()
    {
        var workspace = new TemplatesBuilderWorkspaceViewModel();
        var host = new RecordingBuilderHost
        {
            ReferenceData = new TemplatesBuilderReferenceData(null!, null!)
        };
        var controller = new TemplatesBuilderWorkspaceController(new RecordingTemplatesCapabilityService(), workspace, host);

        var exception = await Record.ExceptionAsync(() => controller.CreateDraftAsync());

        Assert.Null(exception);
        Assert.True(workspace.HasActiveDraft);
        Assert.True(host.NavigatedToBuilder);
        Assert.Contains("switches: none loaded", workspace.ReferenceText, StringComparison.Ordinal);
        Assert.Contains("catalog disks: none loaded", workspace.ReferenceText, StringComparison.Ordinal);
    }

    [Fact]
    public void BuilderLoadNewDraft_WithMissingNestedNicCollection_ReportsValidationBlockerInsteadOfThrowing()
    {
        var workspace = new TemplatesBuilderWorkspaceViewModel();
        var draft = TemplatesBuilderDraftMapper.CreateSuggestedDraft(new TemplatesBuilderReferenceData(
            ["vSwitch-Core"],
            [
                new TemplateVhdxCatalogOption("disk-dc", @"C:\base\disk-dc.vhdx", "Windows Server", "2022", 2, "sig-dc"),
                new TemplateVhdxCatalogOption("disk-member", @"C:\base\disk-member.vhdx", "Windows Server", "2022", 2, "sig-member")
            ]));
        var malformedVm = draft.Vms[0] with { Nics = null! };

        var exception = Record.Exception(() => workspace.LoadNewDraft(draft with { Vms = [malformedVm, draft.Vms[1]] }));

        Assert.Null(exception);
        Assert.True(workspace.HasValidationBlockers);
        Assert.Contains(workspace.ValidationState.Blockers, issue =>
            issue.Category == TemplatesBuilderValidationCategory.Network &&
            issue.ScopeKey == malformedVm.VmId &&
            issue.Message.Contains("networking data could not be loaded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuilderSmoke_CreateDraftNavigatesDrillInAndPreservesInvalidDraftWithoutHyperV()
    {
        var workspace = new TemplatesBuilderWorkspaceViewModel();
        var host = new RecordingBuilderHost
        {
            ReferenceData = CreateReferenceData()
        };
        var controller = new TemplatesBuilderWorkspaceController(new RecordingTemplatesCapabilityService(), workspace, host);

        await controller.CreateDraftAsync();

        Assert.True(workspace.HasActiveDraft);
        Assert.True(host.NavigatedToBuilder);
        Assert.False(host.NavigatedToLibrary);
        Assert.Contains("vSwitch-Core", workspace.ReferenceText, StringComparison.Ordinal);
        Assert.Contains("disk-dc", workspace.ReferenceText, StringComparison.Ordinal);

        var draft = workspace.CaptureDraft();
        var navigation = new TemplatesBuilderWorkflowNavigation();
        Assert.True(navigation.SelectStep(BuilderWorkflowStep.Vms, draft));

        var vmList = navigation.Project(draft, canNavigate: true);
        Assert.Equal(BuilderNavigatorDepth.VmList, vmList.NavigatorDepth);
        Assert.Equal("Back to Builder", vmList.NavigatorBackTargetLabel);
        Assert.True(Assert.Single(vmList.RootRows, row => row.Label == "VMs").IsSelected);

        Assert.True(navigation.SelectVmChild(0, draft));
        var vmSections = navigation.Project(draft, canNavigate: true);
        Assert.Equal(BuilderNavigatorDepth.VmSections, vmSections.NavigatorDepth);
        Assert.Equal("Back to VMs", vmSections.NavigatorBackTargetLabel);
        Assert.Equal(BuilderVmDetailCategory.Basics, vmSections.SelectedVmDetailCategory);
        Assert.Contains(vmSections.SelectedVmSectionRows, row => row.Label == "Resources");
        Assert.Contains(vmSections.SelectedVmSectionRows, row => row.Label == "Networking");

        Assert.True(navigation.MoveNavigatorBack());
        Assert.Equal(BuilderNavigatorDepth.VmList, navigation.Project(draft, canNavigate: true).NavigatorDepth);
        Assert.True(navigation.MoveNavigatorBack());
        var backAtRoot = navigation.Project(draft, canNavigate: true);
        Assert.Equal(BuilderNavigatorDepth.Root, backAtRoot.NavigatorDepth);
        Assert.Equal(BuilderWorkflowStep.Vms, backAtRoot.ActiveStep);
        Assert.False(navigation.MoveNavigatorBack());
        Assert.False(host.NavigatedToLibrary);

        var invalidVm = draft.Vms[0] with { Name = "bad/name" };
        workspace.ApplyDraft(draft with { Vms = [invalidVm, draft.Vms[1]] });

        var retainedDraft = workspace.CaptureDraft();
        Assert.Equal("bad/name", retainedDraft.Vms[0].Name);
        Assert.True(workspace.HasValidationBlockers);
        Assert.Contains(workspace.ValidationState.Blockers, issue =>
            issue.Message.Contains("unsupported characters", StringComparison.Ordinal));

        navigation.SelectStep(BuilderWorkflowStep.Review, retainedDraft);
        var reviewFooter = navigation.ProjectFooter(
            retainedDraft,
            canNavigate: true,
            canSave: !workspace.HasValidationBlockers,
            canSaveAs: !workspace.HasValidationBlockers);
        Assert.True(reviewFooter.IsReview);
        Assert.False(reviewFooter.CanSave);
        Assert.False(reviewFooter.CanSaveAs);

        await controller.SaveAsync();
        await controller.SaveAsAsync();

        Assert.Contains("blocked", workspace.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unsupported characters", workspace.StatusText, StringComparison.Ordinal);
        Assert.False(host.NavigatedToLibrary);
    }

    private static TemplatesBuilderReferenceData CreateReferenceData()
        =>
        new(
            ["vSwitch-Core"],
            [
                new TemplateVhdxCatalogOption("disk-dc", @"C:\base\disk-dc.vhdx", "Windows Server", "2022", 2, "sig-dc"),
                new TemplateVhdxCatalogOption("disk-member", @"C:\base\disk-member.vhdx", "Windows Server", "2022", 2, "sig-member")
            ]);

    private sealed class RecordingTemplatesCapabilityService : ITemplatesCapabilityService
    {
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
            => Task.FromResult(new TemplateOperationResult());

        public Task<TemplateValidationSummaryResult> ValidateAsync(TemplateEditorDocument document, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateValidationSummaryResult());

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
        public TemplatesBuilderReferenceData ReferenceData { get; init; } = new(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>());

        public bool IsTemplatesLoading { get; private set; }

        public bool NavigatedToBuilder { get; private set; }

        public bool NavigatedToLibrary { get; private set; }

        public void SetTemplatesLoading(bool isLoading) => IsTemplatesLoading = isLoading;

        public void ApplyWorkspaceState()
        {
        }

        public Task EnsureTemplatesLibraryAsync(bool forceRefresh) => Task.CompletedTask;

        public Task<TemplatesBuilderReferenceData> LoadReferenceDataAsync(bool forceRefresh) => Task.FromResult(ReferenceData);

        public Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName)
            => Task.FromResult<string?>(@"C:\templates\v2-lab-template.json");

        public void NavigateToBuilder() => NavigatedToBuilder = true;

        public void NavigateToLibrary() => NavigatedToLibrary = true;
    }
}
