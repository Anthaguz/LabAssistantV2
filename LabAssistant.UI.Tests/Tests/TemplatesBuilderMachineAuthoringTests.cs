using System.Linq;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Runtime-independent coverage for the Phase 2 Level 2 machine authoring rules
/// (<see cref="TemplatesBuilderMachineAuthoring"/>) and the machine/standalone projections
/// (<see cref="TemplatesBuilderMachineProjector"/>, and the Level 1 standalone container on
/// <see cref="TemplatesBuilderDirectoryTopologyProjector"/>). Every "create" path is asserted to leave the
/// draft valid through the real <see cref="TemplatesBuilderDraftValidator"/>, and deleting a domain's only
/// domain controller is asserted to be refused so a domain can never be stripped of its directory anchor.
/// </summary>
public sealed class TemplatesBuilderMachineAuthoringTests
{
    [Fact]
    public void AddDomainComputer_AddsMemberServerBornValidAndSelected()
    {
        var start = StartDraft();
        var domainId = start.Domains[0].DomainId;

        var result = TemplatesBuilderMachineAuthoring.AddDomainComputer(start, domainId);
        var draft = result.Draft;

        Assert.Equal(start.Vms.Count + 1, draft.Vms.Count);
        var added = draft.Vms[result.SelectedVmIndex];
        Assert.Equal("contososrv01", added.Name);
        Assert.False(added.IsActiveDirectoryDomainController);
        Assert.Equal(V2MembershipModeCatalog.DomainMember, added.MembershipMode);
        Assert.Equal(domainId, added.DomainId);
        Assert.Single(added.Nics);
        AssertValid(draft);
    }

    [Fact]
    public void AddDomainComputer_UnknownDomain_IsNoOp()
    {
        var start = StartDraft();

        var result = TemplatesBuilderMachineAuthoring.AddDomainComputer(start, "does-not-exist");

        Assert.Equal(-1, result.SelectedVmIndex);
        Assert.Equal(start.Vms.Count, result.Draft.Vms.Count);
    }

    [Fact]
    public void AddStandaloneComputer_AddsWorkgroupMachineBornValidAndSelected()
    {
        var start = StartDraft();

        var result = TemplatesBuilderMachineAuthoring.AddStandaloneComputer(start);
        var draft = result.Draft;

        var added = draft.Vms[result.SelectedVmIndex];
        Assert.Equal("standalone01", added.Name);
        Assert.False(added.IsActiveDirectoryDomainController);
        Assert.Equal(V2MembershipModeCatalog.Standalone, added.MembershipMode);
        Assert.Equal(string.Empty, added.DomainId);
        Assert.True(TemplatesBuilderMachineProjector.IsStandalone(added));
        AssertValid(draft);
    }

    [Fact]
    public void DeleteComputer_RemovesMemberAndSelectsSurvivorInSameDomain()
    {
        var start = StartDraft();
        var domainId = start.Domains[0].DomainId;
        var withMember = TemplatesBuilderMachineAuthoring.AddDomainComputer(start, domainId).Draft;
        var memberIndex = withMember.Vms.Count - 1;

        var result = TemplatesBuilderMachineAuthoring.DeleteComputer(withMember, memberIndex);

        Assert.Equal(start.Vms.Count, result.Draft.Vms.Count);
        Assert.DoesNotContain(result.Draft.Vms, vm => vm.Name == "contososrv01");
        // The surviving domain controller in the same domain is selected.
        Assert.True(result.SelectedVmIndex >= 0);
        Assert.True(TemplatesBuilderMachineProjector.BelongsToDomain(result.Draft.Vms[result.SelectedVmIndex], domainId));
        AssertValid(result.Draft);
    }

    [Fact]
    public void DeleteComputer_RefusesToDeleteOnlyDomainController()
    {
        var start = StartDraft();
        var dcIndex = start.Vms.ToList().FindIndex(vm => vm.IsActiveDirectoryDomainController);

        var result = TemplatesBuilderMachineAuthoring.DeleteComputer(start, dcIndex);

        Assert.Equal(start.Vms.Count, result.Draft.Vms.Count);
        Assert.Equal(dcIndex, result.SelectedVmIndex);
        Assert.Contains(result.Draft.Vms, vm => vm.IsActiveDirectoryDomainController);
    }

    [Fact]
    public void DeleteComputer_AllowsDomainControllerWhenAnotherRemains()
    {
        var start = StartDraft();
        var dc = start.Vms.First(vm => vm.IsActiveDirectoryDomainController);
        var secondDc = dc with { VmId = dc.VmId + "-2", Name = dc.Name + "b" };
        var twoDcs = start with { Vms = start.Vms.Append(secondDc).ToList() };
        var firstDcIndex = twoDcs.Vms.ToList().FindIndex(vm => vm.IsActiveDirectoryDomainController);

        var result = TemplatesBuilderMachineAuthoring.DeleteComputer(twoDcs, firstDcIndex);

        Assert.Equal(twoDcs.Vms.Count - 1, result.Draft.Vms.Count);
        Assert.Contains(result.Draft.Vms, vm => vm.IsActiveDirectoryDomainController);
    }

    [Fact]
    public void ProjectDomainMachines_OrdersDomainControllerFirst()
    {
        var start = StartDraft();
        var domainId = start.Domains[0].DomainId;
        var withMember = TemplatesBuilderMachineAuthoring.AddDomainComputer(start, domainId).Draft;

        var container = TemplatesBuilderMachineProjector.ProjectDomainMachines(withMember, domainId, selectedVmIndex: -1);

        Assert.False(container.IsStandalone);
        Assert.Equal("contoso.lab", container.Title);
        Assert.Equal(2, container.Machines.Count);
        Assert.True(container.Machines[0].IsDomainController);
        Assert.Equal("Domain controller", container.Machines[0].RoleLabel);
        Assert.Equal("Member server", container.Machines[1].RoleLabel);
    }

    [Fact]
    public void ProjectStandaloneMachines_ReturnsOnlyStandaloneCards()
    {
        var start = StartDraft();
        var withStandalone = TemplatesBuilderMachineAuthoring.AddStandaloneComputer(start).Draft;

        var container = TemplatesBuilderMachineProjector.ProjectStandaloneMachines(withStandalone, selectedVmIndex: 0);

        Assert.True(container.IsStandalone);
        Assert.Equal(TemplatesBuilderMachineProjector.StandaloneContainerId, container.ContainerId);
        var card = Assert.Single(container.Machines);
        Assert.Equal("Standalone machine", card.RoleLabel);
    }

    [Fact]
    public void DirectoryTopologyProjection_EmitsStandaloneContainerOnlyWhenStandaloneMachinesExist()
    {
        var start = StartDraft();
        var withoutStandalone = TemplatesBuilderDirectoryTopologyProjector.Project(start, BuilderForestDomainResourceKind.Forest, 0);
        Assert.Null(withoutStandalone.Standalone);

        var withStandalone = TemplatesBuilderMachineAuthoring.AddStandaloneComputer(start).Draft;
        var projection = TemplatesBuilderDirectoryTopologyProjector.Project(withStandalone, BuilderForestDomainResourceKind.Standalone, 0);
        Assert.NotNull(projection.Standalone);
        Assert.Equal(1, projection.Standalone!.Value.MachineCount);
        Assert.True(projection.Standalone.Value.IsSelected);
    }

    private static void AssertValid(TemplatesBuilderDraftSnapshot draft)
    {
        var state = TemplatesBuilderDraftValidator.Validate(draft, TemplatesBuilderValidationRequest.All());
        Assert.True(
            state.Blockers.Count == 0,
            "Unexpected validation blockers: " + string.Join(" | ", state.Blockers.Select(issue => issue.Message)));
    }

    private static TemplatesBuilderDraftSnapshot StartDraft()
        => TemplatesBuilderTopologyAuthoring.AddForest(EmptyDraft()).Draft;

    private static TemplatesBuilderDraftSnapshot EmptyDraft()
        => new(
            TemplateName: "Empty Lab",
            TemplateDescription: string.Empty,
            DeploymentProfile: "Balanced",
            LabNetworks:
            [
                new TemplatesBuilderLabNetworkDraft("lab-core", "Core", "vSwitch-Core", string.Empty, "10.0.0.0/24", string.Empty)
            ],
            CredentialSlots:
            [
                new TemplatesBuilderCredentialSlotDraft("slot-local", "Local bootstrap", "local bootstrap"),
                new TemplatesBuilderCredentialSlotDraft("slot-admin", "Domain admin", "domain administration"),
                new TemplatesBuilderCredentialSlotDraft("slot-dsrm", "DSRM", "domain controller recovery")
            ],
            Forests: [],
            Domains: [],
            Vms: [],
            IsSaveConfirmed: false);
}
