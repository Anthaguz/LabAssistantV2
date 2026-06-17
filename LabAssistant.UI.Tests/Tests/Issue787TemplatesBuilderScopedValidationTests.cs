using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class Issue787TemplatesBuilderScopedValidationTests
{
    [Fact]
    public void BuilderValidation_DomainChange_ValidatesDomainAndDependentMembershipReferencesOnly()
    {
        var draft = CreateBuilderDraft();
        var changedDomain = draft.Domains[0] with
        {
            DomainId = "domain-renamed",
            DnsName = "not a domain"
        };
        var changed = draft with { Domains = [changedDomain] };

        var request = TemplatesBuilderValidationRequest.DetectChangedScopes(draft, changed);
        var result = TemplatesBuilderDraftValidator.Validate(changed, request);

        Assert.Contains(TemplatesBuilderValidationCategory.Domain, result.EvaluatedCategories);
        Assert.Contains(TemplatesBuilderValidationCategory.VmMembership, result.EvaluatedCategories);
        Assert.DoesNotContain(TemplatesBuilderValidationCategory.Network, result.EvaluatedCategories);
        Assert.DoesNotContain(TemplatesBuilderValidationCategory.VmIdentity, result.EvaluatedCategories);
        Assert.Contains(result.Blockers, issue => issue.Category == TemplatesBuilderValidationCategory.Domain && issue.Message.Contains("DNS name", StringComparison.Ordinal));
        Assert.Contains(result.Blockers, issue => issue.Category == TemplatesBuilderValidationCategory.VmMembership && issue.Message.Contains("unknown domain", StringComparison.Ordinal));
    }

    [Fact]
    public void BuilderValidation_IpEdits_CheckFormatSubnetFitAndDuplicatesInNetworkScope()
    {
        var draft = CreateBuilderDraft();
        var invalidIpVm = draft.Vms[0] with
        {
            Nics = [draft.Vms[0].Nics[0] with { IpAddress = "999.1.1.1" }]
        };
        var outsideSubnetVm = draft.Vms[0] with
        {
            Nics = [draft.Vms[0].Nics[0] with { IpAddress = "10.0.1.10" }]
        };
        var duplicateIpVm = draft.Vms[1] with
        {
            Nics = [draft.Vms[1].Nics[0] with { IpAddress = "10.0.0.10" }]
        };

        var invalidIp = TemplatesBuilderDraftValidator.Validate(
            draft with { Vms = [invalidIpVm, draft.Vms[1]] },
            TemplatesBuilderValidationRequest.ForCategories(TemplatesBuilderValidationCategory.Network));
        var outsideSubnet = TemplatesBuilderDraftValidator.Validate(
            draft with { Vms = [outsideSubnetVm, draft.Vms[1]] },
            TemplatesBuilderValidationRequest.ForCategories(TemplatesBuilderValidationCategory.Network));
        var duplicateIp = TemplatesBuilderDraftValidator.Validate(
            draft with { Vms = [draft.Vms[0], duplicateIpVm] },
            TemplatesBuilderValidationRequest.ForCategories(TemplatesBuilderValidationCategory.Network));

        Assert.Contains(invalidIp.Blockers, issue => issue.Message.Contains("valid IPv4 address", StringComparison.Ordinal));
        Assert.Contains(outsideSubnet.Blockers, issue => issue.Message.Contains("must fit network", StringComparison.Ordinal));
        Assert.Contains(duplicateIp.Blockers, issue => issue.Message.Contains("duplicated", StringComparison.Ordinal));
        Assert.Equal([TemplatesBuilderValidationCategory.Network], duplicateIp.EvaluatedCategories);
    }

    [Fact]
    public void BuilderValidation_VmIdentityChange_ValidatesVmIdentityOnly()
    {
        var draft = CreateBuilderDraft();
        var invalidVm = draft.Vms[0] with { Name = "bad/name" };
        var duplicateVm = draft.Vms[1] with { VmId = draft.Vms[0].VmId };
        var changed = draft with { Vms = [invalidVm, duplicateVm] };

        var request = TemplatesBuilderValidationRequest.DetectChangedScopes(draft, changed);
        var result = TemplatesBuilderDraftValidator.Validate(changed, request);

        Assert.Equal([TemplatesBuilderValidationCategory.VmIdentity], result.EvaluatedCategories);
        Assert.Contains(result.Blockers, issue => issue.Message.Contains("unsupported characters", StringComparison.Ordinal));
        Assert.Contains(result.Blockers, issue => issue.Message.Contains("VM id", StringComparison.Ordinal) && issue.Message.Contains("unique", StringComparison.Ordinal));
    }

    [Fact]
    public void BuilderValidation_MembershipChange_ValidatesDomainAssignmentAndRoleCompatibilityOnly()
    {
        var draft = CreateBuilderDraft();
        var invalidVm = draft.Vms[0] with
        {
            MembershipMode = V2MembershipModeCatalog.Standalone,
            DomainId = "domain-contoso",
            IsActiveDirectoryDomainController = true
        };
        var changed = draft with { Vms = [invalidVm, draft.Vms[1]] };

        var request = TemplatesBuilderValidationRequest.DetectChangedScopes(draft, changed);
        var result = TemplatesBuilderDraftValidator.Validate(changed, request);

        Assert.Equal([TemplatesBuilderValidationCategory.VmMembership], result.EvaluatedCategories);
        Assert.Contains(result.Blockers, issue => issue.Message.Contains("must not carry a domain assignment", StringComparison.Ordinal));
        Assert.Contains(result.Blockers, issue => issue.Message.Contains("must use DomainMember membership", StringComparison.Ordinal));
    }

    [Fact]
    public void BuilderWorkspace_UnrelatedDraftEdit_RetainsExistingScopedValidationState()
    {
        var workspace = new TemplatesBuilderWorkspaceViewModel();
        var draft = CreateBuilderDraft();

        workspace.LoadNewDraft(draft);
        workspace.ApplyDraft(draft with { Vms = [draft.Vms[0] with { Name = "bad/name" }, draft.Vms[1]] });
        var blockerCount = workspace.ValidationState.Blockers.Count;

        workspace.ApplyDraft(workspace.CaptureDraft() with { TemplateDescription = "Unrelated description edit." });

        Assert.True(blockerCount > 0);
        Assert.Equal(blockerCount, workspace.ValidationState.Blockers.Count);
        Assert.Contains(workspace.ValidationState.Blockers, issue => issue.Category == TemplatesBuilderValidationCategory.VmIdentity);
    }

    [Fact]
    public void TemplatesBuilderView_ReviewAggregatesValidationStateWithoutManualValidateAction()
    {
        var builderSource = File.ReadAllText(WinUIPath(Path.Combine("Views", "Templates", "TemplatesBuilderView.xaml.cs")));
        var compositionSource = File.ReadAllText(WinUIPath(Path.Combine("ViewModels", "Templates", "Builder", "TemplatesBuilderWorkspaceComposition.cs")));

        Assert.Contains("ValidationState: _workspace.ValidationState", compositionSource);
        Assert.Contains("UpdateValidationState(_workspace.ValidationState)", compositionSource);
        Assert.Contains("_validationState.BuildReviewSummary()", builderSource);
        Assert.DoesNotContain("ValidateRequested", builderSource);
        Assert.DoesNotContain("ValidateRequested", compositionSource);
    }

    [Fact]
    public async Task BuilderSaveAndSaveAs_AreBlockedByCurrentValidationBlockers()
    {
        var workspace = new TemplatesBuilderWorkspaceViewModel();
        var service = new RecordingTemplatesCapabilityService();
        var host = new RecordingBuilderHost();
        var controller = new TemplatesBuilderWorkspaceController(service, workspace, host);
        var draft = CreateBuilderDraft();

        workspace.LoadNewDraft(draft);
        workspace.ApplyDraft(draft with { Vms = [draft.Vms[0] with { Name = "bad/name" }, draft.Vms[1]] });

        Assert.True(workspace.HasValidationBlockers, string.Join(" | ", workspace.ValidationState.Blockers.Select(issue => issue.Message)));

        await controller.SaveAsync();
        await controller.SaveAsAsync();

        Assert.Equal(0, service.SaveCalls);
        Assert.Equal(0, host.SavePickerCalls);
        Assert.Contains("blocked", workspace.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unsupported characters", workspace.StatusText, StringComparison.Ordinal);
    }

    private static TemplatesBuilderDraftSnapshot CreateBuilderDraft()
    {
        var referenceData = new TemplatesBuilderReferenceData(
            ["vSwitch-Core"],
            [
                new TemplateVhdxCatalogOption("disk-dc", @"C:\base\disk-dc.vhdx", "Windows Server", "2022", 2, "sig-dc"),
                new TemplateVhdxCatalogOption("disk-member", @"C:\base\disk-member.vhdx", "Windows Server", "2022", 2, "sig-member")
            ]);

        return TemplatesBuilderDraftMapper.CreateSuggestedDraft(referenceData);
    }

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

    private sealed class RecordingTemplatesCapabilityService : ITemplatesCapabilityService
    {
        public int SaveCalls { get; private set; }

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
            return Task.FromResult(new TemplateOperationResult { Success = true, UserMessage = "Saved." });
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
        public int SavePickerCalls { get; private set; }

        public bool IsTemplatesLoading { get; private set; }

        public void SetTemplatesLoading(bool isLoading) => IsTemplatesLoading = isLoading;

        public void ApplyWorkspaceState()
        {
        }

        public Task EnsureTemplatesLibraryAsync(bool forceRefresh) => Task.CompletedTask;

        public Task<TemplatesBuilderReferenceData> LoadReferenceDataAsync(bool forceRefresh)
            => Task.FromResult(new TemplatesBuilderReferenceData(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>()));

        public Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName)
        {
            SavePickerCalls++;
            return Task.FromResult<string?>(@"C:\templates\save-as.json");
        }

        public void NavigateToBuilder()
        {
        }

        public void NavigateToLibrary()
        {
        }
    }
}
