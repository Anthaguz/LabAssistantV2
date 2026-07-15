using System.Linq;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Runtime-independent coverage for the Level 2 roles/features foundation: the catalog projection rules
/// (<see cref="TemplatesBuilderRoleProjectionCatalog"/>) and the non-structural role authoring engine
/// (<see cref="TemplatesBuilderRoleAuthoring"/>). These assert the source-of-truth split (the domain-controller
/// role stays on the structural flag, everything else on the additive <c>AdditionalRoles</c> collection) and the
/// one-way Active Directory -> DNS link.
/// </summary>
public sealed class TemplatesBuilderRoleAuthoringTests
{
    private const string Adds = TemplatesBuilderRoleProjectionCatalog.ActiveDirectoryDomainControllerRoleKey;
    private const string Dns = TemplatesBuilderRoleProjectionCatalog.DnsServerRoleKey;
    private const string Dhcp = TemplatesBuilderRoleProjectionCatalog.DhcpServerRoleKey;
    private const string Adcs = TemplatesBuilderRoleProjectionCatalog.CertificateServicesRoleKey;

    [Fact]
    public void Catalog_ExposesTheAuthorableRolesAndAnEmptyFeaturesPanel()
    {
        var roles = TemplatesBuilderRoleProjectionCatalog.GetRoles();

        Assert.Contains(roles, entry => entry.Key == Adds && entry.IsStructural);
        Assert.Contains(roles, entry => entry.Key == Dns);
        Assert.Contains(roles, entry => entry.Key == Dhcp);
        Assert.Contains(roles, entry => entry.Key == TemplatesBuilderRoleProjectionCatalog.FileServerRoleKey);
        Assert.Contains(roles, entry => entry.Key == Adcs && entry.IsInstallOnly);
        Assert.Empty(TemplatesBuilderRoleProjectionCatalog.GetFeatures());
    }

    [Fact]
    public void ProjectVmRoles_ForAMember_TracksTheAdditionalRolesCollection()
    {
        var member = Member();

        var enabled = TemplatesBuilderRoleAuthoring.SetAdditionalRole(DraftWith(member), 0, Dhcp, enabled: true);
        var projection = TemplatesBuilderRoleProjectionCatalog.ProjectVmRoles(enabled.Draft.Vms[0]);

        var dhcp = projection.Single(role => role.RoleKey == Dhcp);
        Assert.True(dhcp.IsAssigned);
        Assert.False(dhcp.IsLocked);

        var disabled = TemplatesBuilderRoleAuthoring.SetAdditionalRole(enabled.Draft, 0, Dhcp, enabled: false);
        Assert.False(TemplatesBuilderRoleProjectionCatalog.IsRoleAssigned(disabled.Draft.Vms[0], Dhcp));
    }

    [Fact]
    public void ProjectVmRoles_ForADomainController_ForcesDnsOnAndLocksIt()
    {
        var dc = DomainController();

        var projection = TemplatesBuilderRoleProjectionCatalog.ProjectVmRoles(dc);

        var adds = projection.Single(role => role.RoleKey == Adds);
        Assert.True(adds.IsAssigned);

        var dns = projection.Single(role => role.RoleKey == Dns);
        Assert.True(dns.IsAssigned);
        Assert.True(dns.IsLocked);
        Assert.False(string.IsNullOrWhiteSpace(dns.StatusNote));
    }

    [Fact]
    public void SetAdditionalRole_CannotDisableDnsWhileTheVmIsADomainController()
    {
        var dc = DomainController();

        var result = TemplatesBuilderRoleAuthoring.SetAdditionalRole(DraftWith(dc), 0, Dns, enabled: false);

        Assert.True(TemplatesBuilderRoleProjectionCatalog.IsRoleAssigned(result.Draft.Vms[0], Dns));
    }

    [Fact]
    public void SetAdditionalRole_IgnoresTheStructuralDomainControllerRole()
    {
        var member = Member();

        var result = TemplatesBuilderRoleAuthoring.SetAdditionalRole(DraftWith(member), 0, Adds, enabled: true);

        // The domain-controller role is owned by the structural flag, not this collection: nothing changes.
        Assert.False(result.Draft.Vms[0].IsActiveDirectoryDomainController);
        Assert.False(TemplatesBuilderRoleProjectionCatalog.HasAdditionalRole(result.Draft.Vms[0], Adds));
    }

    [Fact]
    public void ApplyAddsImplications_PersistsDnsSoItSurvivesDemotion()
    {
        var dc = DomainController();

        var withDns = TemplatesBuilderRoleAuthoring.ApplyAddsImplications(dc);
        Assert.True(TemplatesBuilderRoleProjectionCatalog.HasAdditionalRole(withDns, Dns));

        // Disabling Active Directory (one-way link) must not remove DNS.
        var demoted = withDns with { IsActiveDirectoryDomainController = false };
        var dns = TemplatesBuilderRoleProjectionCatalog.ProjectRole(demoted, TemplatesBuilderRoleProjectionCatalog.FindRole(Dns)!.Value);
        Assert.True(dns.IsAssigned);
        Assert.False(dns.IsLocked);
    }

    [Fact]
    public void SetAdditionalRole_EnablingAdcs_MarksItInstallOnly()
    {
        var member = Member();

        var result = TemplatesBuilderRoleAuthoring.SetAdditionalRole(DraftWith(member), 0, Adcs, enabled: true);
        var adcs = TemplatesBuilderRoleProjectionCatalog.ProjectRole(result.Draft.Vms[0], TemplatesBuilderRoleProjectionCatalog.FindRole(Adcs)!.Value);

        Assert.True(adcs.IsAssigned);
        Assert.True(adcs.IsInstallOnly);
        // The install-only state is shown by the IsInstallOnly badge alone now; no long status note.
        Assert.True(string.IsNullOrWhiteSpace(adcs.StatusNote));
    }

    [Fact]
    public void SetAdditionalRole_OutOfRangeIndexOrUnknownRole_IsNoOp()
    {
        var draft = DraftWith(Member());

        Assert.Equal(draft, TemplatesBuilderRoleAuthoring.SetAdditionalRole(draft, 5, Dhcp, enabled: true).Draft);
        Assert.Equal(draft, TemplatesBuilderRoleAuthoring.SetAdditionalRole(draft, 0, "not-a-role", enabled: true).Draft);
    }

    // ----- helpers -----

    private static TemplatesBuilderDraftSnapshot DraftWith(TemplatesBuilderVmDraft vm)
        => new(
            TemplateName: "Roles Lab",
            TemplateDescription: string.Empty,
            DeploymentProfile: "Balanced",
            LabNetworks: [],
            CredentialSlots: [],
            Forests: [],
            Domains: [],
            Vms: [vm],
            IsSaveConfirmed: false);

    private static TemplatesBuilderVmDraft Member()
        => new(
            "vm-member",
            "member01",
            "4096",
            "2",
            string.Empty,
            "DomainMember",
            "domain-1",
            IsActiveDirectoryDomainController: false,
            new TemplatesBuilderVmCredentialSlotDraft(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            []);

    private static TemplatesBuilderVmDraft DomainController()
        => Member() with { VmId = "vm-dc", Name = "contosodc01", IsActiveDirectoryDomainController = true };
}
