using System.Linq;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Runtime-independent coverage for <see cref="TemplatesBuilderMachineBasicsAuthoring"/>: the Level 2 inspector's
/// editable basics (name, vCPU, memory, base disk) and the last-octet host-address editor, including the
/// reserved-address policy (.1 router, .254 host) and duplicate detection that the inline IP feedback relies on.
/// </summary>
public sealed class TemplatesBuilderMachineBasicsAuthoringTests
{
    [Fact]
    public void SetMachineName_UpdatesNameAndPreservesSelection()
    {
        var draft = SuggestedDraft();
        var index = MemberIndex(draft);

        var result = TemplatesBuilderMachineBasicsAuthoring.SetMachineName(draft, index, "app01");

        Assert.Equal(index, result.SelectedVmIndex);
        Assert.Equal("app01", result.Draft.Vms[index].Name);
        Assert.False(result.Draft.IsSaveConfirmed);
    }

    [Fact]
    public void SetMachineCpuAndMemory_StoreRawTextForValidatorToRangeCheck()
    {
        var draft = SuggestedDraft();
        var index = MemberIndex(draft);

        var withCpu = TemplatesBuilderMachineBasicsAuthoring.SetMachineCpuCount(draft, index, "4").Draft;
        var withMemory = TemplatesBuilderMachineBasicsAuthoring.SetMachineMemoryMb(withCpu, index, "8192").Draft;

        Assert.Equal("4", withMemory.Vms[index].CpuCount);
        Assert.Equal("8192", withMemory.Vms[index].MemoryMb);
    }

    [Fact]
    public void SetMachineBaseDisk_TrimsSelection()
    {
        var draft = SuggestedDraft();
        var index = MemberIndex(draft);

        var result = TemplatesBuilderMachineBasicsAuthoring.SetMachineBaseDisk(draft, index, "  disk-dc  ");

        Assert.Equal("disk-dc", result.Draft.Vms[index].VhdxId);
    }

    [Fact]
    public void ProjectHostAddress_ForDomainMember_ExposesFixedPrefixAndSingleEditableOctet()
    {
        var draft = SuggestedDraft();
        var index = MemberIndex(draft);

        var view = TemplatesBuilderMachineBasicsAuthoring.ProjectHostAddress(draft, index);

        Assert.True(view.IsEditable);
        Assert.Equal(1, view.EditableOctetCount);
        Assert.EndsWith(".", view.FixedOctetPrefix);
        Assert.Single(view.Octets);
        Assert.Equal(MachineHostAddressStatus.Ok, view.Status);
    }

    [Fact]
    public void SetMachineHostOctets_WritesComposedAddressAndReadsBack()
    {
        var draft = SuggestedDraft();
        var index = MemberIndex(draft);

        var result = TemplatesBuilderMachineBasicsAuthoring.SetMachineHostOctets(draft, index, new[] { 55 });
        var view = TemplatesBuilderMachineBasicsAuthoring.ProjectHostAddress(result.Draft, index);

        Assert.Equal(55, view.Octets[0]);
        Assert.Equal(MachineHostAddressStatus.Ok, view.Status);
    }

    [Fact]
    public void SetMachineHostOctets_OutOfRangeOctet_NoOps()
    {
        var draft = SuggestedDraft();
        var index = MemberIndex(draft);
        var before = draft.Vms[index].Nics[0].IpAddress;

        var result = TemplatesBuilderMachineBasicsAuthoring.SetMachineHostOctets(draft, index, new[] { 999 });

        Assert.Equal(before, result.Draft.Vms[index].Nics[0].IpAddress);
    }

    [Fact]
    public void HostAddress_RouterReservedFirstAddress_FlagsReservedRouter()
    {
        var draft = SuggestedDraft();
        var index = MemberIndex(draft);

        // .1 is the router reservation on a /24.
        var result = TemplatesBuilderMachineBasicsAuthoring.SetMachineHostOctets(draft, index, new[] { 1 });
        var view = TemplatesBuilderMachineBasicsAuthoring.ProjectHostAddress(result.Draft, index);

        Assert.Equal(MachineHostAddressStatus.ReservedRouter, view.Status);
    }

    [Fact]
    public void HostAddress_MatchingAnotherMachineOnSameSwitch_FlagsDuplicate()
    {
        var draft = SuggestedDraft();
        var memberIndex = MemberIndex(draft);
        var dcIndex = draft.Vms.ToList().FindIndex(vm => vm.IsActiveDirectoryDomainController);

        // Read the DC's octet, then point the member at the same address on the shared switch.
        var dcOctet = TemplatesBuilderMachineBasicsAuthoring.ProjectHostAddress(draft, dcIndex).Octets[0];
        var result = TemplatesBuilderMachineBasicsAuthoring.SetMachineHostOctets(draft, memberIndex, new[] { dcOctet });
        var view = TemplatesBuilderMachineBasicsAuthoring.ProjectHostAddress(result.Draft, memberIndex);

        Assert.Equal(MachineHostAddressStatus.Duplicate, view.Status);
    }

    [Fact]
    public void ProjectHostAddress_ForRouter_IsNotEditable()
    {
        var draft = SuggestedDraft();
        var index = MemberIndex(draft);
        var vms = draft.Vms.ToList();
        vms[index] = vms[index] with { IsRouter = true };
        var routerDraft = draft with { Vms = vms };

        var view = TemplatesBuilderMachineBasicsAuthoring.ProjectHostAddress(routerDraft, index);

        Assert.False(view.IsEditable);
        Assert.Equal(MachineHostAddressView.Unavailable, view);
    }

    [Fact]
    public void SetMachineHostOctets_OnRouter_NoOps()
    {
        var draft = SuggestedDraft();
        var index = MemberIndex(draft);
        var vms = draft.Vms.ToList();
        vms[index] = vms[index] with { IsRouter = true };
        var routerDraft = draft with { Vms = vms };
        var before = routerDraft.Vms[index].Nics[0].IpAddress;

        var result = TemplatesBuilderMachineBasicsAuthoring.SetMachineHostOctets(routerDraft, index, new[] { 77 });

        Assert.Equal(before, result.Draft.Vms[index].Nics[0].IpAddress);
    }

    private static TemplatesBuilderDraftSnapshot SuggestedDraft()
    {
        var referenceData = new TemplatesBuilderReferenceData(
            ["vSwitch-Core"],
            [
                new TemplateVhdxCatalogOption("disk-dc", @"C:\base\disk-dc.vhdx", "Windows Server", "2022", 2, "sig-dc"),
                new TemplateVhdxCatalogOption("disk-member", @"C:\base\disk-member.vhdx", "Windows Server", "2022", 2, "sig-member")
            ]);
        return TemplatesBuilderDraftMapper.CreateSuggestedDraft(referenceData);
    }

    private static int MemberIndex(TemplatesBuilderDraftSnapshot draft)
        => draft.Vms.ToList().FindIndex(vm =>
            !vm.IsActiveDirectoryDomainController && !vm.IsRouter && !string.IsNullOrWhiteSpace(vm.DomainId));
}
