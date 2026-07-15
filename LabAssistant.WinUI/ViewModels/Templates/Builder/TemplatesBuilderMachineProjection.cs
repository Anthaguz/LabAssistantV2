using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A single machine card at Level 2: one virtual machine inside a domain or inside the Standalone container.
/// <see cref="VmIndex"/> is the index into <c>draft.Vms</c> so selection and edits route straight back to the
/// owning machine without re-searching by id.
/// </summary>
internal readonly record struct TemplatesBuilderMachineCardProjection(
    string NodeId,
    int VmIndex,
    string VmId,
    string Label,
    string RoleLabel,
    string Subtext,
    bool IsDomainController,
    bool IsSelected);

/// <summary>
/// The Level 2 view of one container's machines: either a single domain or the Standalone container.
/// <see cref="ContainerId"/> is a domain id, or <see cref="TemplatesBuilderMachineProjector.StandaloneContainerId"/>
/// for the standalone container.
/// </summary>
internal readonly record struct TemplatesBuilderMachineContainerProjection(
    string ContainerId,
    string Title,
    bool IsStandalone,
    IReadOnlyList<TemplatesBuilderMachineCardProjection> Machines);

/// <summary>
/// Pure, runtime-independent projection of the machines that live inside a Level 2 container (a domain, or the
/// Standalone container). Mirrors the directory-topology projector: it reads an immutable draft and produces
/// ordered, labelled cards, so it is unit-testable without a XAML host and never touches Hyper-V.
///
/// Container membership is derived from the draft, not stored redundantly: a machine belongs to a domain when
/// its domain id matches (domain controllers included); a machine is standalone when its membership mode is
/// Standalone, or its domain is unset and it is not a domain controller. Domain controllers always sort first
/// inside a domain so the directory anchor is the obvious first card.
/// </summary>
internal static class TemplatesBuilderMachineProjector
{
    /// <summary>Container id used for the Standalone container (the box that holds domain-unset machines).</summary>
    public const string StandaloneContainerId = "__standalone__";

    /// <summary>Stable canvas node id for the Level 1 Standalone container box.</summary>
    public const string StandaloneContainerNodeId = "standalone:container";

    public static bool IsStandalone(TemplatesBuilderVmDraft vm)
        => V2MembershipModeCatalog.IsStandalone(vm.MembershipMode) ||
           (string.IsNullOrWhiteSpace(vm.DomainId) && !vm.IsActiveDirectoryDomainController);

    public static bool BelongsToDomain(TemplatesBuilderVmDraft vm, string domainId)
        => !IsStandalone(vm) &&
           !string.IsNullOrWhiteSpace(domainId) &&
           string.Equals(vm.DomainId?.Trim(), domainId.Trim(), StringComparison.OrdinalIgnoreCase);

    public static TemplatesBuilderMachineContainerProjection ProjectDomainMachines(
        TemplatesBuilderDraftSnapshot draft,
        string domainId,
        int selectedVmIndex)
    {
        var title = ResolveDomainTitle(draft, domainId);
        var cards = IndexedVms(draft)
            .Where(item => BelongsToDomain(item.Vm, domainId))
            .OrderBy(item => item.Vm.IsActiveDirectoryDomainController ? 0 : 1)
            .ThenBy(item => item.Index)
            .Select(item => BuildCard(item.Vm, item.Index, selectedVmIndex))
            .ToList();

        return new TemplatesBuilderMachineContainerProjection(domainId, title, false, cards);
    }

    public static TemplatesBuilderMachineContainerProjection ProjectStandaloneMachines(
        TemplatesBuilderDraftSnapshot draft,
        int selectedVmIndex)
    {
        var cards = IndexedVms(draft)
            .Where(item => IsStandalone(item.Vm))
            .OrderBy(item => item.Index)
            .Select(item => BuildCard(item.Vm, item.Index, selectedVmIndex))
            .ToList();

        return new TemplatesBuilderMachineContainerProjection(StandaloneContainerId, "Standalone", true, cards);
    }

    private static TemplatesBuilderMachineCardProjection BuildCard(
        TemplatesBuilderVmDraft vm,
        int vmIndex,
        int selectedVmIndex)
    {
        var roleLabel = vm.IsActiveDirectoryDomainController
            ? "Domain controller"
            : V2MembershipModeCatalog.IsStandalone(vm.MembershipMode) ||
              (string.IsNullOrWhiteSpace(vm.DomainId) && !vm.IsActiveDirectoryDomainController)
                ? "Standalone machine"
                : "Member server";

        var nicCount = vm.Nics?.Count ?? 0;
        var subtext = $"{roleLabel} - {nicCount} NIC{(nicCount == 1 ? string.Empty : "s")}";

        return new TemplatesBuilderMachineCardProjection(
            $"machine:{FormatStableSegment(vm.VmId, vmIndex)}",
            vmIndex,
            vm.VmId,
            FormatMachineLabel(vm, vmIndex),
            roleLabel,
            subtext,
            vm.IsActiveDirectoryDomainController,
            vmIndex == selectedVmIndex);
    }

    private static string ResolveDomainTitle(TemplatesBuilderDraftSnapshot draft, string domainId)
    {
        var domain = draft.Domains.FirstOrDefault(candidate =>
            string.Equals(candidate.DomainId?.Trim(), domainId?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(domain.DnsName))
        {
            return domain.DnsName.Trim();
        }

        return string.IsNullOrWhiteSpace(domainId) ? "Domain" : domainId.Trim();
    }

    private static IEnumerable<(int Index, TemplatesBuilderVmDraft Vm)> IndexedVms(TemplatesBuilderDraftSnapshot draft)
        => draft.Vms.Select((vm, index) => (index, vm));

    private static string FormatMachineLabel(TemplatesBuilderVmDraft vm, int index)
        => !string.IsNullOrWhiteSpace(vm.Name)
            ? vm.Name.Trim()
            : string.IsNullOrWhiteSpace(vm.VmId)
                ? $"Machine {index + 1}"
                : vm.VmId.Trim();

    private static string FormatStableSegment(string value, int index)
        => string.IsNullOrWhiteSpace(value) ? $"draft-{index}" : value.Trim();
}
