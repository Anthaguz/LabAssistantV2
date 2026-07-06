using LabAssistant.Business.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Runtime-independent smoke coverage for the migrated <see cref="TemplatesBuilderViewModel"/>: entry
/// (create draft) resilience with sparse reference data, malformed nested collections surfacing as
/// validation blockers rather than throwing, and the full create -> navigate drill-in -> invalid edit
/// -> save-blocked flow with no dispatcher or Hyper-V present.
/// </summary>
public sealed class TemplatesBuilderEntrySmokeTests
{
    [Fact]
    public async Task BuilderCreateDraft_WithSparseReferenceData_DoesNotThrowAndNavigatesToBuilder()
    {
        var host = new RecordingBuilderHost
        {
            ReferenceData = new TemplatesBuilderReferenceData(null!, null!)
        };
        var viewModel = new TemplatesBuilderViewModel(new RecordingTemplatesCapabilityService());
        viewModel.Attach(host);

        var exception = await Record.ExceptionAsync(() => viewModel.CreateDraftAsync());

        Assert.Null(exception);
        Assert.True(viewModel.HasActiveDraft);
        Assert.True(host.NavigatedToBuilder);
        Assert.Contains("switches: none loaded", viewModel.ReferenceText, StringComparison.Ordinal);
        Assert.Contains("catalog disks: none loaded", viewModel.ReferenceText, StringComparison.Ordinal);
    }

    [Fact]
    public void BuilderLoadNewDraft_WithMissingNestedNicCollection_ReportsValidationBlockerInsteadOfThrowing()
    {
        var viewModel = new TemplatesBuilderViewModel(new RecordingTemplatesCapabilityService());
        var draft = TemplatesBuilderDraftMapper.CreateSuggestedDraft(new TemplatesBuilderReferenceData(
            ["vSwitch-Core"],
            [
                new TemplateVhdxCatalogOption("disk-dc", @"C:\base\disk-dc.vhdx", "Windows Server", "2022", 2, "sig-dc"),
                new TemplateVhdxCatalogOption("disk-member", @"C:\base\disk-member.vhdx", "Windows Server", "2022", 2, "sig-member")
            ]));
        var malformedVm = draft.Vms[0] with { Nics = null! };

        var exception = Record.Exception(() => viewModel.LoadNewDraft(draft with { Vms = [malformedVm, draft.Vms[1]] }));

        Assert.Null(exception);
        Assert.True(viewModel.HasValidationBlockers);
        Assert.Contains(viewModel.ValidationState.Blockers, issue =>
            issue.Category == TemplatesBuilderValidationCategory.Network &&
            issue.ScopeKey == malformedVm.VmId &&
            issue.Message.Contains("networking data could not be loaded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuilderSmoke_CreateDraftNavigatesDrillInAndPreservesInvalidDraftWithoutHyperV()
    {
        var host = new RecordingBuilderHost
        {
            ReferenceData = CreateReferenceData()
        };
        var viewModel = new TemplatesBuilderViewModel(new RecordingTemplatesCapabilityService());
        viewModel.Attach(host);

        await viewModel.CreateDraftAsync();

        Assert.True(viewModel.HasActiveDraft);
        Assert.True(host.NavigatedToBuilder);
        Assert.False(host.NavigatedToLibrary);
        Assert.Contains("vSwitch-Core", viewModel.ReferenceText, StringComparison.Ordinal);
        Assert.Contains("disk-dc", viewModel.ReferenceText, StringComparison.Ordinal);

        var draft = viewModel.CaptureDraft();
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
        viewModel.ApplyDraft(draft with { Vms = [invalidVm, draft.Vms[1]] });

        var retainedDraft = viewModel.CaptureDraft();
        Assert.Equal("bad/name", retainedDraft.Vms[0].Name);
        Assert.True(viewModel.HasValidationBlockers);
        Assert.Contains(viewModel.ValidationState.Blockers, issue =>
            issue.Message.Contains("unsupported characters", StringComparison.Ordinal));

        navigation.SelectStep(BuilderWorkflowStep.Review, retainedDraft);
        var reviewFooter = navigation.ProjectFooter(
            retainedDraft,
            canNavigate: true,
            canSave: !viewModel.HasValidationBlockers,
            canSaveAs: !viewModel.HasValidationBlockers);
        Assert.True(reviewFooter.IsReview);
        Assert.False(reviewFooter.CanSave);
        Assert.False(reviewFooter.CanSaveAs);

        await viewModel.SaveCommand.ExecuteAsync(null);
        await viewModel.SaveAsCommand.ExecuteAsync(null);

        Assert.Contains("blocked", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unsupported characters", viewModel.StatusText, StringComparison.Ordinal);
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

    private sealed class RecordingBuilderHost : ITemplatesBuilderHost
    {
        public TemplatesBuilderReferenceData ReferenceData { get; init; } = new(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>());

        public bool NavigatedToBuilder { get; private set; }

        public bool NavigatedToLibrary { get; private set; }

        public Task<TemplatesBuilderReferenceData> LoadBuilderReferenceDataAsync(bool forceRefresh) => Task.FromResult(ReferenceData);

        public Task ReloadLibraryAsync(bool forceRefresh) => Task.CompletedTask;

        public Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName)
            => Task.FromResult<string?>(@"C:\templates\v2-lab-template.json");

        public void NavigateToBuilder() => NavigatedToBuilder = true;

        public void NavigateToLibrary() => NavigatedToLibrary = true;
    }
}
