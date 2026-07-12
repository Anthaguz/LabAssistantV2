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
}
