using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

internal enum TemplatesBuilderResourceKind
{
    Network,
    CredentialSlot
}

internal enum BuilderForestDomainResourceKind
{
    Forest,
    Domain
}

internal readonly record struct TemplatesBuilderResourceRowProjection(
    TemplatesBuilderResourceKind Kind,
    int Index,
    string Label,
    bool IsSelected);

internal readonly record struct TemplatesBuilderVmOverviewProjection(
    int TotalVmCount,
    int StandaloneVmCount,
    int DomainMemberVmCount,
    int ActiveDirectoryDomainControllerCount,
    IReadOnlyList<string> SummaryRows);

internal readonly record struct TemplatesBuilderNicRowProjection(
    int Index,
    TemplatesBuilderNicDraft Draft,
    string Label,
    bool IsSelected);

internal readonly record struct TemplatesBuilderVmDetailProjection(
    int VmIndex,
    BuilderVmDetailCategory Category,
    string Title,
    string CategoryLabel,
    TemplatesBuilderVmDraft Vm,
    IReadOnlyList<TemplatesBuilderVmRoleProjection> Roles,
    IReadOnlyList<TemplatesBuilderNicRowProjection> Nics,
    int SelectedNicIndex,
    bool IsNicDetailSelected);

internal static class TemplatesBuilderSectionProjections
{
    public static IReadOnlyList<TemplatesBuilderResourceRowProjection> ProjectNetworkRows(
        TemplatesBuilderDraftSnapshot draft,
        int selectedIndex)
        => draft.LabNetworks
            .Select((network, index) => new TemplatesBuilderResourceRowProjection(
                TemplatesBuilderResourceKind.Network,
                index,
                FormatResourceName(network.Name, network.NetworkId),
                index == selectedIndex))
            .ToList();

    public static IReadOnlyList<TemplatesBuilderResourceRowProjection> ProjectCredentialSlotRows(
        TemplatesBuilderDraftSnapshot draft,
        int selectedIndex)
        => draft.CredentialSlots
            .Select((slot, index) => new TemplatesBuilderResourceRowProjection(
                TemplatesBuilderResourceKind.CredentialSlot,
                index,
                FormatResourceName(slot.Label, slot.SlotKey),
                index == selectedIndex))
            .ToList();

    public static TemplatesBuilderVmOverviewProjection ProjectVmOverview(TemplatesBuilderDraftSnapshot draft)
    {
        var standaloneCount = draft.Vms.Count(vm => string.Equals(vm.MembershipMode, V2MembershipModeCatalog.Standalone, StringComparison.OrdinalIgnoreCase));
        var domainMemberCount = draft.Vms.Count(vm => string.Equals(vm.MembershipMode, V2MembershipModeCatalog.DomainMember, StringComparison.OrdinalIgnoreCase));
        var adDcCount = draft.Vms.Count(vm => vm.IsActiveDirectoryDomainController);

        return new TemplatesBuilderVmOverviewProjection(
            draft.Vms.Count,
            standaloneCount,
            domainMemberCount,
            adDcCount,
            draft.Vms.Select(vm =>
            {
                var role = vm.IsActiveDirectoryDomainController ? ", AD DC" : string.Empty;
                return $"{FormatResourceName(vm.Name, vm.VmId)} - {FormatResourceName(vm.MembershipMode, V2MembershipModeCatalog.Standalone)}, {vm.Nics.Count} NICs{role}";
            }).ToList());
    }

    public static TemplatesBuilderVmDetailProjection? ProjectSelectedVmDetail(
        TemplatesBuilderDraftSnapshot draft,
        BuilderWorkflowProjection workflowProjection)
    {
        if (!workflowProjection.IsVmDetailSelected ||
            draft.Vms.Count == 0 ||
            workflowProjection.SelectedVmIndex < 0 ||
            workflowProjection.SelectedVmIndex >= draft.Vms.Count)
        {
            return null;
        }

        var vm = draft.Vms[workflowProjection.SelectedVmIndex];
        return new TemplatesBuilderVmDetailProjection(
            workflowProjection.SelectedVmIndex,
            workflowProjection.SelectedVmDetailCategory,
            $"Selected VM Detail: {FormatResourceName(vm.Name, vm.VmId)}",
            GetVmDetailCategoryLabel(workflowProjection.SelectedVmDetailCategory),
            vm,
            TemplatesBuilderRoleProjectionCatalog.ProjectVmRoles(vm),
            (vm.Nics ?? Array.Empty<TemplatesBuilderNicDraft>()).Select((nic, index) => new TemplatesBuilderNicRowProjection(
                index,
                nic,
                FormatResourceName(nic.Name, nic.NicId),
                workflowProjection.IsNicDetailSelected && workflowProjection.SelectedNicIndex == index)).ToList(),
            workflowProjection.SelectedNicIndex,
            workflowProjection.IsNicDetailSelected);
    }

    private static string FormatResourceName(string primary, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Trim();
        }

        return string.IsNullOrWhiteSpace(fallback) ? "(unnamed)" : fallback.Trim();
    }

    private static string GetVmDetailCategoryLabel(BuilderVmDetailCategory category)
        => category switch
        {
            BuilderVmDetailCategory.Basics => "Basics",
            BuilderVmDetailCategory.Resources => "Resources",
            BuilderVmDetailCategory.Membership => "Membership",
            BuilderVmDetailCategory.Roles => "Roles",
            BuilderVmDetailCategory.Networking => "Networking",
            BuilderVmDetailCategory.Credentials => "Credentials",
            _ => category.ToString()
        };
}
