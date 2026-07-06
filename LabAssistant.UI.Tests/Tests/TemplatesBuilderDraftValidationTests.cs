using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Runtime-independent coverage for the Builder's scoped validation contract, exercised both through
/// the pure <see cref="TemplatesBuilderDraftValidator"/> and through the migrated
/// <see cref="TemplatesBuilderViewModel"/>'s scoped-revalidation merge (formerly the workspace view
/// model). Also verifies Review aggregates validation state automatically with no manual Validate
/// action, and that Save / Save As honor current blockers and final-build failures.
/// </summary>
public sealed class TemplatesBuilderDraftValidationTests
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
    public void BuilderViewModel_UnrelatedDraftEdit_RetainsExistingScopedValidationState()
    {
        var viewModel = CreateViewModel();
        var draft = CreateBuilderDraft();

        viewModel.LoadNewDraft(draft);
        viewModel.ApplyDraft(draft with { Vms = [draft.Vms[0] with { Name = "bad/name" }, draft.Vms[1]] });
        var blockerCount = viewModel.ValidationState.Blockers.Count;

        viewModel.ApplyDraft(viewModel.CaptureDraft() with { TemplateDescription = "Unrelated description edit." });

        Assert.True(blockerCount > 0);
        Assert.Equal(blockerCount, viewModel.ValidationState.Blockers.Count);
        Assert.Contains(viewModel.ValidationState.Blockers, issue => issue.Category == TemplatesBuilderValidationCategory.VmIdentity);
    }

    [Fact]
    public void BuilderViewModel_ScopedVmIdentityRefresh_PreservesOtherVmIdentityBlockers()
    {
        var viewModel = CreateViewModel();
        var draft = CreateBuilderDraft();
        var vmAInvalid = draft.Vms[0] with { Name = "bad/name" };
        var vmBInvalid = draft.Vms[1] with { Name = "bad name" };

        viewModel.LoadNewDraft(draft);
        viewModel.ApplyDraft(draft with { Vms = [vmAInvalid, vmBInvalid] });
        viewModel.ApplyDraft(viewModel.CaptureDraft() with { Vms = [draft.Vms[0], vmBInvalid] });

        Assert.True(viewModel.HasValidationBlockers);
        Assert.DoesNotContain(viewModel.ValidationState.Blockers, issue => issue.ScopeKey == draft.Vms[0].VmId);
        Assert.Contains(viewModel.ValidationState.Blockers, issue =>
            issue.Category == TemplatesBuilderValidationCategory.VmIdentity &&
            issue.ScopeKey == draft.Vms[1].VmId &&
            issue.Message.Contains("unsupported characters", StringComparison.Ordinal));
    }

    [Fact]
    public void BuilderViewModel_ScopedVmIdentityRefresh_RemovesResolvedDuplicateIdentityBlockers()
    {
        var viewModel = CreateViewModel();
        var draft = CreateBuilderDraft();
        var duplicateVm = draft.Vms[1] with
        {
            VmId = draft.Vms[0].VmId,
            Name = draft.Vms[0].Name
        };

        viewModel.LoadNewDraft(draft);
        viewModel.ApplyDraft(draft with { Vms = [draft.Vms[0], duplicateVm] });

        Assert.Contains(viewModel.ValidationState.Blockers, issue => issue.Message.Contains("VM id", StringComparison.Ordinal) && issue.Message.Contains("unique", StringComparison.Ordinal));
        Assert.Contains(viewModel.ValidationState.Blockers, issue => issue.Message.Contains("VM name", StringComparison.Ordinal) && issue.Message.Contains("unique", StringComparison.Ordinal));

        viewModel.ApplyDraft(viewModel.CaptureDraft() with { Vms = [draft.Vms[0], draft.Vms[1]] });

        Assert.DoesNotContain(viewModel.ValidationState.Blockers, issue => issue.Message.Contains("VM id", StringComparison.Ordinal) && issue.Message.Contains("unique", StringComparison.Ordinal));
        Assert.DoesNotContain(viewModel.ValidationState.Blockers, issue => issue.Message.Contains("VM name", StringComparison.Ordinal) && issue.Message.Contains("unique", StringComparison.Ordinal));
    }

    [Fact]
    public void BuilderViewModel_ScopedNetworkRefresh_PreservesOtherNetworkBlockers()
    {
        var viewModel = CreateViewModel();
        var draft = CreateBuilderDraft();
        var branchNetwork = new TemplatesBuilderLabNetworkDraft("lab-branch", "Branch", "vSwitch-Branch", string.Empty, "10.1.0.0/24", string.Empty);
        var vmAInvalid = draft.Vms[0] with
        {
            Nics = [draft.Vms[0].Nics[0] with { IpAddress = "not-an-ip" }]
        };
        var vmBInvalid = draft.Vms[1] with
        {
            Nics = [draft.Vms[1].Nics[0] with { NetworkId = "lab-branch", IpAddress = "10.2.0.20" }]
        };

        viewModel.LoadNewDraft(draft);
        viewModel.ApplyDraft(draft with
        {
            LabNetworks = [draft.LabNetworks[0], branchNetwork],
            Vms = [vmAInvalid, vmBInvalid]
        });
        viewModel.ApplyDraft(viewModel.CaptureDraft() with { Vms = [draft.Vms[0], vmBInvalid] });

        Assert.True(viewModel.HasValidationBlockers);
        Assert.DoesNotContain(viewModel.ValidationState.Blockers, issue =>
            issue.Category == TemplatesBuilderValidationCategory.Network &&
            issue.ScopeKey == "lab-core");
        Assert.Contains(viewModel.ValidationState.Blockers, issue =>
            issue.Category == TemplatesBuilderValidationCategory.Network &&
            issue.ScopeKey == "lab-branch" &&
            issue.Message.Contains("must fit network", StringComparison.Ordinal));
    }

    [Fact]
    public void BuilderViewModel_ScopedMembershipRefresh_PreservesOtherMembershipBlockers()
    {
        var viewModel = CreateViewModel();
        var draft = CreateBuilderDraft();
        var vmAInvalid = draft.Vms[0] with
        {
            MembershipMode = V2MembershipModeCatalog.Standalone,
            DomainId = "domain-contoso",
            IsActiveDirectoryDomainController = false
        };
        var vmBInvalid = draft.Vms[1] with { DomainId = "domain-missing" };

        viewModel.LoadNewDraft(draft);
        viewModel.ApplyDraft(draft with { Vms = [vmAInvalid, vmBInvalid] });
        viewModel.ApplyDraft(viewModel.CaptureDraft() with { Vms = [draft.Vms[0], vmBInvalid] });

        Assert.True(viewModel.HasValidationBlockers);
        Assert.DoesNotContain(viewModel.ValidationState.Blockers, issue => issue.ScopeKey == draft.Vms[0].VmId);
        Assert.Contains(viewModel.ValidationState.Blockers, issue =>
            issue.Category == TemplatesBuilderValidationCategory.VmMembership &&
            issue.ScopeKey == draft.Vms[1].VmId &&
            issue.Message.Contains("unknown domain", StringComparison.Ordinal));
    }

    [Fact]
    public void BuilderViewModel_ScopedMembershipRefresh_ClearsSatisfiedDomainDcBlockerAndKeepsUnrelatedVmBlocker()
    {
        var viewModel = CreateViewModel();
        var draft = CreateBuilderDraft();
        var vmWithoutDcRole = draft.Vms[0] with
        {
            IsActiveDirectoryDomainController = false
        };
        var vmWithUnrelatedMembershipBlocker = draft.Vms[1] with
        {
            DomainId = "domain-missing"
        };

        viewModel.LoadNewDraft(draft);
        viewModel.ApplyDraft(draft with { Vms = [vmWithoutDcRole, vmWithUnrelatedMembershipBlocker] });

        Assert.Contains(viewModel.ValidationState.Blockers, issue =>
            issue.Category == TemplatesBuilderValidationCategory.VmMembership &&
            issue.ScopeKey == "domain-contoso" &&
            issue.Message.Contains("requires at least one VM assigned", StringComparison.Ordinal));
        Assert.Contains(viewModel.ValidationState.Blockers, issue =>
            issue.Category == TemplatesBuilderValidationCategory.VmMembership &&
            issue.ScopeKey == draft.Vms[1].VmId &&
            issue.Message.Contains("unknown domain", StringComparison.Ordinal));

        viewModel.ApplyDraft(viewModel.CaptureDraft() with { Vms = [draft.Vms[0], vmWithUnrelatedMembershipBlocker] });

        Assert.DoesNotContain(viewModel.ValidationState.Blockers, issue =>
            issue.Category == TemplatesBuilderValidationCategory.VmMembership &&
            issue.ScopeKey == "domain-contoso" &&
            issue.Message.Contains("requires at least one VM assigned", StringComparison.Ordinal));
        Assert.Contains(viewModel.ValidationState.Blockers, issue =>
            issue.Category == TemplatesBuilderValidationCategory.VmMembership &&
            issue.ScopeKey == draft.Vms[1].VmId &&
            issue.Message.Contains("unknown domain", StringComparison.Ordinal));
    }

    [Fact]
    public void BuilderViewModel_ReviewAggregatesValidationStateWithoutManualValidateAction()
    {
        var viewModel = CreateViewModel();
        var draft = CreateBuilderDraft();
        var invalidDraft = draft with { Vms = [draft.Vms[0] with { Name = "bad/name" }, draft.Vms[1]] };

        // Loading an invalid draft computes validation blockers; the Review projection is
        // seeded from the constructor render.
        viewModel.LoadNewDraft(invalidDraft);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ReviewSummaryText));

        // Any real edit runs the pipeline that re-renders Review from the current validation
        // state - there is no manual Validate action to press.
        viewModel.SetDeploymentProfileCommand.Execute("Balanced");
        Assert.True(viewModel.IsReviewBlockerVisible);
        Assert.Contains("unsupported characters", viewModel.ReviewBlockerText, StringComparison.Ordinal);

        // The Builder exposes no manual Validate command: Review is always derived from current state.
        Assert.Null(typeof(TemplatesBuilderViewModel).GetProperty("ValidateCommand"));
    }

    [Fact]
    public async Task BuilderSaveAndSaveAs_AreBlockedByCurrentValidationBlockers()
    {
        var service = new RecordingTemplatesCapabilityService();
        var host = new RecordingBuilderHost();
        var viewModel = new TemplatesBuilderViewModel(service);
        viewModel.Attach(host);
        var draft = CreateBuilderDraft();

        viewModel.LoadNewDraft(draft);
        viewModel.ApplyDraft(draft with { Vms = [draft.Vms[0] with { Name = "bad/name" }, draft.Vms[1]] });

        Assert.True(viewModel.HasValidationBlockers, string.Join(" | ", viewModel.ValidationState.Blockers.Select(issue => issue.Message)));

        await viewModel.SaveCommand.ExecuteAsync(null);
        await viewModel.SaveAsCommand.ExecuteAsync(null);

        Assert.Equal(0, service.SaveCalls);
        Assert.Equal(0, host.SavePickerCalls);
        Assert.Contains("blocked", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unsupported characters", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuilderSaveAndSaveAs_AreBlockedWhenFinalBuildFails()
    {
        var service = new RecordingTemplatesCapabilityService();
        var host = new RecordingBuilderHost();
        var viewModel = new TemplatesBuilderViewModel(service);
        viewModel.Attach(host);
        var draft = CreateBuilderDraft() with { TemplateName = string.Empty };

        viewModel.LoadNewDraft(draft);

        Assert.False(viewModel.HasValidationBlockers, string.Join(" | ", viewModel.ValidationState.Blockers.Select(issue => issue.Message)));

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, service.SaveCalls);
        Assert.Equal(0, host.SavePickerCalls);
        Assert.Contains("Save blocked", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains("Template name is required.", viewModel.StatusText, StringComparison.Ordinal);

        await viewModel.SaveAsCommand.ExecuteAsync(null);

        Assert.Equal(0, service.SaveCalls);
        Assert.Equal(0, host.SavePickerCalls);
        Assert.Contains("Save As blocked", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains("Template name is required.", viewModel.StatusText, StringComparison.Ordinal);
    }

    private static TemplatesBuilderViewModel CreateViewModel()
    {
        var viewModel = new TemplatesBuilderViewModel(new RecordingTemplatesCapabilityService());
        viewModel.Attach(new RecordingBuilderHost());
        return viewModel;
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

    private sealed class RecordingBuilderHost : ITemplatesBuilderHost
    {
        public int SavePickerCalls { get; private set; }

        public Task<TemplatesBuilderReferenceData> LoadBuilderReferenceDataAsync(bool forceRefresh)
            => Task.FromResult(new TemplatesBuilderReferenceData(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>()));

        public Task ReloadLibraryAsync(bool forceRefresh) => Task.CompletedTask;

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
