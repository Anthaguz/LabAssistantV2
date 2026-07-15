using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// The outcome of a machine authoring mutation: the new draft and the index into <c>draft.Vms</c> the caller
/// should select at Level 2 afterward (the machine just created, or a sensible survivor after a delete;
/// <c>-1</c> when nothing remains in the container).
/// </summary>
internal readonly record struct MachineAuthoringResult(
    TemplatesBuilderDraftSnapshot Draft,
    int SelectedVmIndex);

/// <summary>
/// Pure, runtime-independent authoring rules for Level 2 machines (Phase 2). Like the topology engine, every
/// method takes an immutable <see cref="TemplatesBuilderDraftSnapshot"/> and returns a new one, so the rules
/// are unit-testable without a XAML host and never touch Hyper-V.
///
/// A machine created here is born valid: a domain computer is a domain member wired to the domain, a
/// standalone computer is a workgroup box with no domain. Deleting a domain's only domain controller is
/// refused, because a domain cannot exist without one and the draft must never be pushed into that state from
/// the machine surface.
/// </summary>
internal static class TemplatesBuilderMachineAuthoring
{
    /// <summary>
    /// Adds a member computer to a domain (not a domain controller; the operating system and any roles are
    /// chosen afterward in the inspector). Selects the new machine. No-op for an unknown domain id.
    /// </summary>
    public static MachineAuthoringResult AddDomainComputer(TemplatesBuilderDraftSnapshot draft, string domainId)
    {
        var domain = draft.Domains.FirstOrDefault(candidate =>
            string.Equals(candidate.DomainId?.Trim(), domainId?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(domain.DomainId))
        {
            return new MachineAuthoringResult(draft, -1);
        }

        var token = FirstLabel(domain.DnsName, fallback: domain.NetBiosName, secondFallback: "srv");
        var name = NextVmName(draft, $"{token}srv");
        var vm = BuildComputer(
            draft,
            name,
            V2MembershipModeCatalog.DomainMember,
            domain.DomainId,
            "Domain");

        var result = draft with { Vms = draft.Vms.Append(vm).ToList(), IsSaveConfirmed = false };
        return new MachineAuthoringResult(result, result.Vms.Count - 1);
    }

    /// <summary>
    /// Adds a standalone (workgroup) computer that belongs to no domain. Selects the new machine.
    /// </summary>
    public static MachineAuthoringResult AddStandaloneComputer(TemplatesBuilderDraftSnapshot draft)
    {
        var name = NextVmName(draft, "standalone");
        var vm = BuildComputer(
            draft,
            name,
            V2MembershipModeCatalog.Standalone,
            string.Empty,
            "Standalone");

        var result = draft with { Vms = draft.Vms.Append(vm).ToList(), IsSaveConfirmed = false };
        return new MachineAuthoringResult(result, result.Vms.Count - 1);
    }

    /// <summary>
    /// Deletes a machine by index. Refuses to delete a domain's only domain controller (returns the draft
    /// unchanged with the same selection), since a domain must keep at least one. Otherwise selects the
    /// nearest surviving machine in the same container, or <c>-1</c> when the container is now empty.
    /// </summary>
    public static MachineAuthoringResult DeleteComputer(TemplatesBuilderDraftSnapshot draft, int vmIndex)
    {
        if (vmIndex < 0 || vmIndex >= draft.Vms.Count)
        {
            return new MachineAuthoringResult(draft, vmIndex);
        }

        var target = draft.Vms[vmIndex];
        if (target.IsActiveDirectoryDomainController && IsOnlyDomainControllerInDomain(draft, target))
        {
            return new MachineAuthoringResult(draft, vmIndex);
        }

        var wasStandalone = TemplatesBuilderMachineProjector.IsStandalone(target);
        var remaining = draft.Vms.Where((_, index) => index != vmIndex).ToList();
        var result = draft with { Vms = remaining, IsSaveConfirmed = false };

        var survivor = FindSurvivorInContainer(result, target, wasStandalone);
        return new MachineAuthoringResult(result, survivor);
    }

    // ----- helpers -----

    private static TemplatesBuilderVmDraft BuildComputer(
        TemplatesBuilderDraftSnapshot draft,
        string name,
        string membershipMode,
        string domainId,
        string nicRole)
    {
        var takenVmIds = draft.Vms.Select(vm => vm.VmId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var vmId = UniqueId($"vm-{name}", takenVmIds);
        var primaryNetworkId = draft.LabNetworks.Count > 0 ? draft.LabNetworks[0].NetworkId : string.Empty;

        return new TemplatesBuilderVmDraft(
            vmId,
            name,
            "4096",
            "2",
            string.Empty,
            membershipMode,
            domainId,
            IsActiveDirectoryDomainController: false,
            new TemplatesBuilderVmCredentialSlotDraft(
                ResolveLocalBootstrapSlot(draft),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty),
            [
                new TemplatesBuilderNicDraft(
                    $"nic-{name}",
                    nicRole,
                    primaryNetworkId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    [])
            ]);
    }

    private static bool IsOnlyDomainControllerInDomain(TemplatesBuilderDraftSnapshot draft, TemplatesBuilderVmDraft target)
    {
        if (string.IsNullOrWhiteSpace(target.DomainId))
        {
            return false;
        }

        var domainControllerCount = draft.Vms.Count(vm =>
            vm.IsActiveDirectoryDomainController &&
            string.Equals(vm.DomainId?.Trim(), target.DomainId.Trim(), StringComparison.OrdinalIgnoreCase));
        return domainControllerCount <= 1;
    }

    private static int FindSurvivorInContainer(
        TemplatesBuilderDraftSnapshot draft,
        TemplatesBuilderVmDraft removed,
        bool wasStandalone)
    {
        for (var index = 0; index < draft.Vms.Count; index++)
        {
            var vm = draft.Vms[index];
            var sameContainer = wasStandalone
                ? TemplatesBuilderMachineProjector.IsStandalone(vm)
                : TemplatesBuilderMachineProjector.BelongsToDomain(vm, removed.DomainId ?? string.Empty);
            if (sameContainer)
            {
                return index;
            }
        }

        return -1;
    }

    private static string ResolveLocalBootstrapSlot(TemplatesBuilderDraftSnapshot draft)
    {
        var match = draft.CredentialSlots.FirstOrDefault(slot =>
            (slot.ScopeHint ?? string.Empty).Contains("local", StringComparison.OrdinalIgnoreCase) ||
            (slot.SlotKey ?? string.Empty).Contains("local", StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(match.SlotKey) ? string.Empty : match.SlotKey;
    }

    private static string NextVmName(TemplatesBuilderDraftSnapshot draft, string baseToken)
    {
        var taken = draft.Vms.Select(vm => vm.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sequence = 1;
        string name;
        do
        {
            name = $"{baseToken}{sequence:D2}";
            sequence++;
        }
        while (taken.Contains(name));
        return name;
    }

    private static string FirstLabel(string dnsName, string fallback, string secondFallback)
    {
        var source = !string.IsNullOrWhiteSpace(dnsName) ? dnsName : fallback;
        if (string.IsNullOrWhiteSpace(source))
        {
            return secondFallback;
        }

        var label = source.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var sanitized = new string((label ?? secondFallback).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return string.IsNullOrWhiteSpace(sanitized) ? secondFallback : sanitized;
    }

    private static string UniqueId(string baseId, ISet<string> taken)
    {
        var candidate = baseId;
        var suffix = 2;
        while (taken.Contains(candidate))
        {
            candidate = $"{baseId}-{suffix}";
            suffix++;
        }

        return candidate;
    }
}
