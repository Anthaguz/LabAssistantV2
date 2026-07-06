using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Runtime-independent coverage for the migrated <see cref="TemplatesBuilderViewModel"/>'s interaction
/// contract: linear + free step navigation and its gating, the review projection deriving from current
/// validation state, draft &lt;-&gt; <see cref="LabTemplate"/> mapping round-trips, and the live-refresh
/// + caret-preservation invariant (editing a per-field view model refreshes the resource lists / VM
/// overview / review without rebuilding the focused detail panel). All exercised with no dispatcher and
/// no Hyper-V, mirroring how the declarative x:Bind view drives the view model.
/// </summary>
public sealed class TemplatesBuilderViewModelInteractionTests
{
    [Fact]
    public void BuilderStepNavigation_LinearNextTraversesTopStepsThenDrillsIntoVmSections()
    {
        var viewModel = CreateViewModel();
        var draft = NamedDraft();
        Assert.NotEmpty(draft.Vms);
        viewModel.LoadNewDraft(draft);

        // Linear Next walks the four leading steps in order (a render has to run first; LoadNewDraft
        // does not render, so the first Next renders and advances).
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsNetworksVisible);

        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsForestsDomainsVisible);

        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsCredentialsVisible);

        // Fifth Next lands on the VM step at its overview.
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsVmsVisible);
        Assert.True(viewModel.IsVmOverviewVisible);
        Assert.False(viewModel.IsVmDetailVisible);

        // Continuing Next drills into the first VM's sections (still the VM step, now a detail route) -
        // linear navigation traverses the flattened route list, not just the six top-level steps.
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsVmsVisible);
        Assert.False(viewModel.IsVmOverviewVisible);
        Assert.True(viewModel.IsVmDetailVisible);

        // Previous walks back out to the VM overview.
        viewModel.PreviousStepCommand.Execute(null);
        Assert.True(viewModel.IsVmsVisible);
        Assert.True(viewModel.IsVmOverviewVisible);

        // And back up through the leading steps.
        viewModel.PreviousStepCommand.Execute(null);
        Assert.True(viewModel.IsCredentialsVisible);
    }

    [Fact]
    public void BuilderStepNavigation_FreeJumpViaNavigatorRowSelectsAnyStep()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());

        // Render the navigator (root depth exposes all six steps in order).
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsNetworksVisible);
        Assert.Equal(6, viewModel.NavigatorRows.Count);

        // Jump straight to Review (index 5), skipping the intermediate steps.
        viewModel.NavigatorRows[5].Command!.Execute(null);
        Assert.True(viewModel.IsReviewVisible);
        // Review is the terminal step: Next hides, Save surfaces.
        Assert.False(viewModel.NextStepVisible);
        Assert.True(viewModel.SaveVisible);

        // Jump straight back to General (index 0).
        viewModel.NavigatorRows[0].Command!.Execute(null);
        Assert.True(viewModel.IsGeneralVisible);
    }

    [Fact]
    public void BuilderStepNavigation_WithoutActiveDraft_IsGatedOff()
    {
        var viewModel = CreateViewModel();

        // No draft loaded: the footer navigation is gated off.
        Assert.False(viewModel.HasActiveDraft);
        Assert.False(viewModel.NextStepEnabled);
        Assert.False(viewModel.PreviousStepEnabled);

        // Loading a draft and rendering enables forward navigation.
        viewModel.LoadNewDraft(NamedDraft());
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.HasActiveDraft);
        Assert.True(viewModel.NextStepEnabled);
    }

    [Fact]
    public void BuilderReview_CleanSuggestedDraft_ShowsNoBlockerAndSummarizesState()
    {
        var viewModel = CreateViewModel();
        var draft = NamedDraft();

        // A clean suggested draft validates without blockers.
        viewModel.LoadNewDraft(draft);
        Assert.False(viewModel.HasValidationBlockers, string.Join(" | ", viewModel.ValidationState.Blockers.Select(issue => issue.Message)));

        // Rendering derives the review projection from validation state - no manual Validate action.
        viewModel.SetDeploymentProfileCommand.Execute("Balanced");
        Assert.Equal(
            viewModel.ValidationState.HasBlockers || viewModel.ValidationState.Warnings.Count > 0,
            viewModel.IsReviewBlockerVisible);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ReviewSummaryText));
    }

    [Fact]
    public void BuilderDraftMapping_RoundTripsVmsDomainsAndNetworksThroughLabTemplate()
    {
        var draft = NamedDraft();
        Assert.NotEmpty(draft.Vms);

        var build = TemplatesBuilderDraftMapper.BuildDocument(draft, "template-round-trip", 1, "1.0.0", sourceFilePath: null);
        Assert.NotNull(build.Document);

        var template = build.Document!.Template;
        var mappedBack = TemplatesBuilderDraftMapper.FromTemplate(template);

        Assert.Equal(draft.TemplateName, mappedBack.TemplateName);
        Assert.Equal(draft.Vms.Count, mappedBack.Vms.Count);
        Assert.Equal(
            draft.Vms.Select(vm => vm.Name),
            mappedBack.Vms.Select(vm => vm.Name));
        Assert.Equal(draft.Domains.Count, mappedBack.Domains.Count);
        Assert.Equal(
            draft.Domains.Select(domain => domain.DnsName),
            mappedBack.Domains.Select(domain => domain.DnsName));
        Assert.Equal(draft.LabNetworks.Count, mappedBack.LabNetworks.Count);
    }

    [Fact]
    public async Task BuilderShowDocument_LoadsMappedVmsIntoDraftAtViewModelLevel()
    {
        var draft = NamedDraft();
        var build = TemplatesBuilderDraftMapper.BuildDocument(draft, "template-show", 1, "1.0.0", sourceFilePath: null);
        Assert.NotNull(build.Document);

        var viewModel = CreateViewModel();
        await viewModel.ShowDocumentAsync(build.Document!);

        var loaded = viewModel.CaptureDraft();
        Assert.True(viewModel.HasActiveDraft);
        Assert.Equal(draft.Vms.Select(vm => vm.Name), loaded.Vms.Select(vm => vm.Name));
        Assert.Equal(draft.TemplateName, loaded.TemplateName);
    }

    [Fact]
    public void BuilderLiveRefresh_FieldEditRefreshesResourceListsWithoutRebuildingFocusedPanel()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());

        // Enter the Networks step so the network detail panel and its per-field view models are live.
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsNetworksVisible);
        Assert.NotEmpty(viewModel.NetworkDetailRows);

        var focusedPanelBefore = viewModel.NetworkDetailRows;
        var nameField = viewModel.NetworkDetailRows[0].Left;
        var resourceListBefore = viewModel.NetworkRows;

        // Simulate a keystroke: the two-way bound field updates its source.
        nameField.Value = "Renamed Segment";

        // Live refresh: the resource list and the working draft reflect the edit immediately.
        Assert.False(ReferenceEquals(resourceListBefore, viewModel.NetworkRows));
        Assert.Equal("Renamed Segment", viewModel.CaptureDraft().LabNetworks[0].Name);

        // Caret preservation: the focused detail panel is NOT rebuilt - same collection and same field
        // view model instance survive the edit, so the caret stays put.
        Assert.True(ReferenceEquals(focusedPanelBefore, viewModel.NetworkDetailRows));
        Assert.True(ReferenceEquals(nameField, viewModel.NetworkDetailRows[0].Left));
    }

    [Fact]
    public void BuilderDetailTitles_PopulateWhenDrillingIntoResourceAndVmDetails()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());

        // Networks step: the detail panel exposes its constant category title alongside the field rows.
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsNetworksVisible);
        Assert.True(viewModel.HasNetworkDetail);
        Assert.Equal("Selected Network Detail", viewModel.NetworkDetailTitle);

        // Drill through the leading steps into the first VM's detail route (6 Next steps total).
        viewModel.NextStepCommand.Execute(null); // ForestsDomains
        viewModel.NextStepCommand.Execute(null); // Credentials
        viewModel.NextStepCommand.Execute(null); // VM overview
        viewModel.NextStepCommand.Execute(null); // first VM detail
        Assert.True(viewModel.IsVmDetailVisible);

        // The VM detail primary title carries the projection's per-VM title (regression: it was dropped).
        Assert.True(viewModel.HasVmDetailTitle);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.VmDetailTitle));
    }

    private static TemplatesBuilderViewModel CreateViewModel()
    {
        var viewModel = new TemplatesBuilderViewModel(new StubTemplatesCapabilityService());
        viewModel.Attach(new StubBuilderHost());
        return viewModel;
    }

    private static TemplatesBuilderDraftSnapshot NamedDraft()
    {
        var referenceData = new TemplatesBuilderReferenceData(
            ["vSwitch-Core"],
            [
                new TemplateVhdxCatalogOption("disk-dc", @"C:\base\disk-dc.vhdx", "Windows Server", "2022", 2, "sig-dc"),
                new TemplateVhdxCatalogOption("disk-member", @"C:\base\disk-member.vhdx", "Windows Server", "2022", 2, "sig-member")
            ]);

        return TemplatesBuilderDraftMapper.CreateSuggestedDraft(referenceData) with { TemplateName = "Interaction Lab" };
    }

    private sealed class StubTemplatesCapabilityService : ITemplatesCapabilityService
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
            => Task.FromResult(new TemplateOperationResult { Success = true, UserMessage = "Saved." });

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

    private sealed class StubBuilderHost : ITemplatesBuilderHost
    {
        public Task<TemplatesBuilderReferenceData> LoadBuilderReferenceDataAsync(bool forceRefresh)
            => Task.FromResult(new TemplatesBuilderReferenceData(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>()));

        public Task ReloadLibraryAsync(bool forceRefresh) => Task.CompletedTask;

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
