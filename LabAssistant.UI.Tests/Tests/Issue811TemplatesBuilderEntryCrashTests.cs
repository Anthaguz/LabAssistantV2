using LabAssistant.Business.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class Issue811TemplatesBuilderEntryCrashTests
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

        public void SetTemplatesLoading(bool isLoading) => IsTemplatesLoading = isLoading;

        public void ApplyWorkspaceState()
        {
        }

        public Task EnsureTemplatesLibraryAsync(bool forceRefresh) => Task.CompletedTask;

        public Task<TemplatesBuilderReferenceData> LoadReferenceDataAsync(bool forceRefresh) => Task.FromResult(ReferenceData);

        public Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName)
            => Task.FromResult<string?>(@"C:\templates\v2-lab-template.json");

        public void NavigateToBuilder() => NavigatedToBuilder = true;

        public void NavigateToLibrary()
        {
        }
    }
}
