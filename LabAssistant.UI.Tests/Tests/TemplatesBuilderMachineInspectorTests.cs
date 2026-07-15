using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Headless coverage for the Level 2 selected-machine inspector projection and role authoring path.
/// </summary>
public sealed class TemplatesBuilderMachineInspectorTests
{
    [Fact]
    public void DomainControllerInspector_LocksDnsAndShowsStructuralAddsAsDisplayOnly()
    {
        var viewModel = CreateDomainMachineLevelViewModel();

        var inspector = AssertInspector(viewModel);
        var dns = inspector.RoleRows.Single(row => row.RoleKey == TemplatesBuilderRoleProjectionCatalog.DnsServerRoleKey);
        Assert.True(dns.IsAssigned);
        Assert.True(dns.IsLocked);
        Assert.False(dns.IsToggleEnabled);
        Assert.Contains("linked automatically", dns.StatusNote, StringComparison.OrdinalIgnoreCase);

        var adds = inspector.RoleRows.Single(row => row.RoleKey == TemplatesBuilderRoleProjectionCatalog.ActiveDirectoryDomainControllerRoleKey);
        Assert.True(adds.IsAssigned);
        Assert.False(adds.IsToggleEnabled);
        Assert.Contains("managed from the machine list", adds.StatusNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MemberServerInspector_TogglingDhcpAssignsAdditionalRole()
    {
        var viewModel = CreateDomainMachineLevelViewModel();
        SelectMemberMachine(viewModel);

        var inspector = AssertInspector(viewModel);
        var dhcp = inspector.RoleRows.Single(row => row.RoleKey == TemplatesBuilderRoleProjectionCatalog.DhcpServerRoleKey);
        Assert.False(dhcp.IsAssigned);
        Assert.True(dhcp.IsToggleEnabled);

        dhcp.ToggleCommand!.Execute(null);

        var updatedInspector = AssertInspector(viewModel);
        var updatedDhcp = updatedInspector.RoleRows.Single(row => row.RoleKey == TemplatesBuilderRoleProjectionCatalog.DhcpServerRoleKey);
        Assert.True(updatedDhcp.IsAssigned);

        var memberIndex = viewModel.MachineCards.Single(card => !card.IsDomainController).VmIndex;
        var member = viewModel.CaptureDraft().Vms[memberIndex];
        Assert.Contains(TemplatesBuilderRoleProjectionCatalog.DhcpServerRoleKey, member.AdditionalRoles ?? []);
    }

    [Fact]
    public void InspectorSearch_FiltersRolesByNameAndDescription()
    {
        var viewModel = CreateDomainMachineLevelViewModel();
        SelectMemberMachine(viewModel);

        viewModel.RoleSearchText = "leases";

        var inspector = AssertInspector(viewModel);
        var row = Assert.Single(inspector.RoleRows);
        Assert.Equal(TemplatesBuilderRoleProjectionCatalog.DhcpServerRoleKey, row.RoleKey);
    }

    [Fact]
    public void Inspector_AdcsRowShowsInstallOnlyBadge()
    {
        var viewModel = CreateDomainMachineLevelViewModel();
        SelectMemberMachine(viewModel);

        var inspector = AssertInspector(viewModel);
        var adcs = inspector.RoleRows.Single(row => row.RoleKey == TemplatesBuilderRoleProjectionCatalog.CertificateServicesRoleKey);
        Assert.True(adcs.IsInstallOnly);
        Assert.True(adcs.ShowInstallOnlyBadge);
        // The long "guided configuration is added later" note was removed; the compact badge is the only signal.
        Assert.True(string.IsNullOrWhiteSpace(adcs.StatusNote));
    }

    [Fact]
    public void CommitMachineBaseDisk_IgnoresEchoAndNullSelections_ButCommitsRealChange()
    {
        // Locks the fix for the base-disk ComboBox re-entrancy that overflowed the stack on zoom-in: rebuilding the
        // inspector re-applies SelectedValue, which WinUI reports back through SelectionChanged as a "selection". A
        // render always mints a fresh inspector instance, so an untouched instance proves the commit was a no-op.
        var viewModel = CreateDomainMachineLevelViewModel();
        var inspector = AssertInspector(viewModel);

        var currentDisk = inspector.SelectedBaseDiskId;
        Assert.False(string.IsNullOrEmpty(currentDisk));

        var dcIndex = viewModel.MachineCards.Single(card => card.IsDomainController).VmIndex;

        // Echo of the current selection - the guard must skip it, leaving the same inspector instance in place.
        inspector.CommitMachineBaseDiskCommand!.Execute(currentDisk);
        Assert.Same(inspector, viewModel.SelectedMachineInspector);

        // Transient null / whitespace while the ItemsSource is swapped - also no-ops.
        inspector.CommitMachineBaseDiskCommand!.Execute(null);
        Assert.Same(inspector, viewModel.SelectedMachineInspector);
        inspector.CommitMachineBaseDiskCommand!.Execute("   ");
        Assert.Same(inspector, viewModel.SelectedMachineInspector);

        Assert.Equal(currentDisk, viewModel.CaptureDraft().Vms[dcIndex].VhdxId);

        // A genuinely different disk commits once and re-renders (new inspector instance).
        var differentDisk = currentDisk == "disk-dc" ? "disk-member" : "disk-dc";
        inspector.CommitMachineBaseDiskCommand!.Execute(differentDisk);
        Assert.NotSame(inspector, viewModel.SelectedMachineInspector);
        Assert.Equal(differentDisk, viewModel.CaptureDraft().Vms[dcIndex].VhdxId);
    }

    [Fact]
    public void SelectingMachineCards_MovesSelectionAndInspectorBetweenMachines()
    {
        // Locks the Level 2 card-tap selection contract the view relies on: tapping a card runs its SelectCommand,
        // which must move both the selected flag and the inspector to that machine. The live regression was purely
        // in the view (ItemsRepeater does not set a realized child's DataContext, so the tap handler read the wrong
        // object) - this asserts the view-model side stays sound so a fresh render always re-targets the inspector.
        var viewModel = CreateDomainMachineLevelViewModel();

        var dcIndex = viewModel.MachineCards.Single(card => card.IsDomainController).VmIndex;
        var memberIndex = viewModel.MachineCards.Single(card => !card.IsDomainController).VmIndex;
        var dcName = viewModel.CaptureDraft().Vms[dcIndex].Name;
        var memberName = viewModel.CaptureDraft().Vms[memberIndex].Name;

        viewModel.MachineCards.Single(card => card.VmIndex == dcIndex).SelectCommand!.Execute(null);
        Assert.Equal(dcIndex, viewModel.MachineCards.Single(card => card.IsSelected).VmIndex);
        var dcInspector = AssertInspector(viewModel);
        Assert.Equal(dcName, dcInspector.MachineNameText);

        viewModel.MachineCards.Single(card => card.VmIndex == memberIndex).SelectCommand!.Execute(null);
        Assert.Equal(memberIndex, viewModel.MachineCards.Single(card => card.IsSelected).VmIndex);
        var memberInspector = AssertInspector(viewModel);
        Assert.NotSame(dcInspector, memberInspector);
        Assert.Equal(memberName, memberInspector.MachineNameText);

        // Tapping back to the first card must re-target again - the selection is not one-way.
        viewModel.MachineCards.Single(card => card.VmIndex == dcIndex).SelectCommand!.Execute(null);
        Assert.Equal(dcIndex, viewModel.MachineCards.Single(card => card.IsSelected).VmIndex);
        Assert.Equal(dcName, AssertInspector(viewModel).MachineNameText);
    }

    [Fact]
    public void SingleDomain_ShowsSubnetSubtext_AndSurvivesWorkingDraftCapture()
    {
        // Regression: the domain switch's in-memory DomainId (the reconciler's domain<->switch link that feeds the
        // node subnet subtext) was dropped whenever CaptureWorkingDraft rebuilt the selected network, so a lone
        // domain rendered "Root domain" with no subnet. Capture runs on ordinary navigation (NextStep), so this
        // reproduces without any explicit network edit.
        var viewModel = new TemplatesBuilderViewModel(new RecordingTemplatesCapabilityService());
        viewModel.Attach(new StubBuilderHost());
        viewModel.LoadNewDraft(NamedDraft());
        viewModel.NextStepCommand.Execute(null);
        viewModel.NextStepCommand.Execute(null);

        var draft = viewModel.CaptureDraft();
        Assert.Single(draft.Domains);
        var domainNetwork = draft.LabNetworks.Single(network => !string.IsNullOrWhiteSpace(network.DomainId));
        Assert.Equal("domain-contoso", domainNetwork.DomainId);

        var domainNode = viewModel.TopologyCanvas!.Nodes.First(node => !node.IsForest && !node.IsStandalone);
        Assert.Contains(domainNetwork.Subnet, domainNode.Subtext);
    }

    [Fact]
    public async Task MachineInspector_SurfacesReadOnlyBootstrapAccountFromBaseDisk()
    {
        // The Level 2 machine inspector shows the base disk's baked-in local admin (bootstrap account) as a read-only
        // field, so the user sees which identity the deploy reuses without authoring any credentials.
        var viewModel = new TemplatesBuilderViewModel(new RecordingTemplatesCapabilityService());
        viewModel.Attach(new BootstrapAwareBuilderHost());
        await viewModel.CreateDraftAsync();

        var domainNode = viewModel.TopologyCanvas!.Nodes.First(node => !node.IsForest && !node.IsStandalone);
        domainNode.ManageMachinesCommand!.Execute(null);
        Assert.True(viewModel.IsMachineLevelVisible);

        var inspector = AssertInspector(viewModel);
        Assert.True(inspector.HasBootstrapAccount);
        Assert.Equal("Administrator", inspector.BootstrapAccountText);
    }

    [Theory]
    [InlineData(MachineHostAddressStatus.Duplicate)]
    [InlineData(MachineHostAddressStatus.ReservedRouter)]
    [InlineData(MachineHostAddressStatus.ReservedHost)]
    [InlineData(MachineHostAddressStatus.OutOfSubnet)]
    [InlineData(MachineHostAddressStatus.NetworkOrBroadcast)]
    [InlineData(MachineHostAddressStatus.Invalid)]
    public void HostAddressStatus_ErrorStates_ProduceInlineMessageAndOutline(MachineHostAddressStatus status)
    {
        // The inspector now receives the status as the MachineHostAddressStatus enum by value, so a future rename
        // of any member is compiler-enforced through the projection. This asserts every error status still maps to
        // a non-empty inline message and the red outline, so a broken mapping fails here instead of silently
        // making an invalid address look valid.
        var inspector = CreateInspectorWithHostStatus(status);

        Assert.Equal(status, inspector.HostAddressStatus);
        Assert.False(string.IsNullOrWhiteSpace(inspector.HostAddressValidationMessage));
        Assert.True(inspector.HasHostAddressValidationMessage);
        Assert.True(inspector.ShowHostAddressErrorOutline);
    }

    [Theory]
    [InlineData(MachineHostAddressStatus.Ok)]
    [InlineData(MachineHostAddressStatus.Empty)]
    public void HostAddressStatus_ValidStates_ProduceNoMessageOrOutline(MachineHostAddressStatus status)
    {
        var inspector = CreateInspectorWithHostStatus(status);

        Assert.Equal(status, inspector.HostAddressStatus);
        Assert.True(string.IsNullOrWhiteSpace(inspector.HostAddressValidationMessage));
        Assert.False(inspector.HasHostAddressValidationMessage);
        Assert.False(inspector.ShowHostAddressErrorOutline);
    }

    private static BuilderMachineInspectorViewModel CreateInspectorWithHostStatus(MachineHostAddressStatus status)
        => new(
            machineTitle: "srv-01",
            roleLabel: "Member server",
            subtext: "Member server",
            searchText: string.Empty,
            isRolesPanelExpanded: false,
            isFeaturesPanelExpanded: false,
            machineNameText: "srv-01",
            machineCpuCountText: "2",
            machineMemoryMbText: "2048",
            baseDiskOptions: [],
            selectedBaseDiskId: "disk-member",
            bootstrapAccountText: string.Empty,
            isHostAddressEditable: true,
            hostAddressFixedOctetPrefix: "10.0.0.",
            hasSecondHostOctet: false,
            hostAddressOctetOneText: "5",
            hostAddressOctetTwoText: string.Empty,
            hostAddressSubnetCidr: "10.0.0.0/24",
            hostAddressStatus: status,
            roleRows: [],
            featureRows: [],
            commitMachineNameCommand: null,
            commitMachineCpuCountCommand: null,
            commitMachineMemoryMbCommand: null,
            commitMachineBaseDiskCommand: null,
            commitMachineHostOctetsCommand: null,
            toggleRolesPanelCommand: null,
            toggleFeaturesPanelCommand: null);

    private static BuilderMachineInspectorViewModel AssertInspector(TemplatesBuilderViewModel viewModel)
    {
        Assert.True(viewModel.HasSelectedMachineInspector);
        Assert.NotNull(viewModel.SelectedMachineInspector);
        return viewModel.SelectedMachineInspector!;
    }

    private static void SelectMemberMachine(TemplatesBuilderViewModel viewModel)
    {
        var memberCard = viewModel.MachineCards.Single(card => !card.IsDomainController);
        memberCard.SelectCommand!.Execute(null);
    }

    private static TemplatesBuilderViewModel CreateDomainMachineLevelViewModel()
    {
        var viewModel = new TemplatesBuilderViewModel(new RecordingTemplatesCapabilityService());
        viewModel.Attach(new StubBuilderHost());
        viewModel.LoadNewDraft(NamedDraft());

        viewModel.NextStepCommand.Execute(null);
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsForestsDomainsVisible);

        var domainNode = viewModel.TopologyCanvas!.Nodes.First(node => !node.IsForest && !node.IsStandalone);
        domainNode.ManageMachinesCommand!.Execute(null);
        Assert.True(viewModel.IsMachineLevelVisible);
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

        return TemplatesBuilderDraftMapper.CreateSuggestedDraft(referenceData) with { TemplateName = "Inspector Lab" };
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

    private sealed class BootstrapAwareBuilderHost : ITemplatesBuilderHost
    {
        public Task<TemplatesBuilderReferenceData> LoadBuilderReferenceDataAsync(bool forceRefresh)
            => Task.FromResult(new TemplatesBuilderReferenceData(
                ["vSwitch-Core"],
                [
                    new TemplateVhdxCatalogOption("disk-dc", @"C:\base\disk-dc.vhdx", "Windows Server", "2022", 2, "sig-dc", "Administrator"),
                    new TemplateVhdxCatalogOption("disk-member", @"C:\base\disk-member.vhdx", "Windows Server", "2022", 2, "sig-member", "Administrator")
                ]));

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
