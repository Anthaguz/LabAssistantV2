namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// Pure authoring rules for the non-structural roles a Level 2 machine can carry (DNS, DHCP, File Server, ADCS).
/// Mirrors the other Builder engines: every method takes an immutable draft and returns a new one, so the rules
/// are unit-testable without a XAML host and never touch Hyper-V.
///
/// The structural directory role (domain controller) is NOT toggled here - it lives on the domain-controller
/// concept and the persisted <c>TopologyRole</c>. This engine only edits the additive
/// <see cref="TemplatesBuilderVmDraft.AdditionalRoles"/> collection and enforces the one-way Active Directory ->
/// DNS link:
/// - While a VM is a domain controller, DNS is forced on and cannot be turned off.
/// - Promoting a VM to a domain controller also persists DNS into <c>AdditionalRoles</c> (via
///   <see cref="ApplyAddsImplications"/>) so DNS survives if the domain-controller role is later removed.
/// </summary>
internal static class TemplatesBuilderRoleAuthoring
{
    /// <summary>
    /// Enables or disables a non-structural role on the VM at <paramref name="vmIndex"/>. No-ops for the
    /// structural (domain-controller) role, for an unknown role key, for an out-of-range index, and for any edit
    /// that would remove DNS while the VM is a domain controller (DNS is locked on there). The selection is
    /// preserved.
    /// </summary>
    public static MachineAuthoringResult SetAdditionalRole(
        TemplatesBuilderDraftSnapshot draft,
        int vmIndex,
        string roleKey,
        bool enabled)
    {
        if (vmIndex < 0 || vmIndex >= draft.Vms.Count || string.IsNullOrWhiteSpace(roleKey))
        {
            return new MachineAuthoringResult(draft, vmIndex);
        }

        var definition = TemplatesBuilderRoleProjectionCatalog.FindRole(roleKey);
        if (definition is null)
        {
            return new MachineAuthoringResult(draft, vmIndex);
        }

        // The structural (domain-controller) role is owned by the domain-controller concept, not this collection.
        if (TemplatesBuilderRoleProjectionCatalog.IsStructuralRole(roleKey))
        {
            return new MachineAuthoringResult(draft, vmIndex);
        }

        var vm = draft.Vms[vmIndex];

        // DNS is locked on while the VM is a domain controller: refuse to disable it.
        if (!enabled &&
            TemplatesBuilderRoleProjectionCatalog.IsDnsRole(roleKey) &&
            vm.IsActiveDirectoryDomainController)
        {
            return new MachineAuthoringResult(draft, vmIndex);
        }

        var already = TemplatesBuilderRoleProjectionCatalog.HasAdditionalRole(vm, roleKey);
        if (enabled == already)
        {
            return new MachineAuthoringResult(draft, vmIndex);
        }

        var current = vm.AdditionalRoles ?? Array.Empty<string>();
        var updatedRoles = enabled
            ? Append(current, definition.Value.Key)
            : current.Where(key => !string.Equals(key, roleKey, StringComparison.OrdinalIgnoreCase)).ToList();

        var vms = draft.Vms.ToList();
        vms[vmIndex] = vm with { AdditionalRoles = updatedRoles };
        return new MachineAuthoringResult(draft with { Vms = vms, IsSaveConfirmed = false }, vmIndex);
    }

    /// <summary>
    /// Materializes the one-way Active Directory -> DNS link on a domain-controller VM: DNS is added to
    /// <see cref="TemplatesBuilderVmDraft.AdditionalRoles"/> so it persists even if the domain-controller role is
    /// later removed. Returns the VM unchanged when it is not a domain controller or already carries DNS.
    /// </summary>
    public static TemplatesBuilderVmDraft ApplyAddsImplications(TemplatesBuilderVmDraft vm)
    {
        if (!vm.IsActiveDirectoryDomainController ||
            TemplatesBuilderRoleProjectionCatalog.HasAdditionalRole(vm, TemplatesBuilderRoleProjectionCatalog.DnsServerRoleKey))
        {
            return vm;
        }

        var updated = Append(vm.AdditionalRoles ?? Array.Empty<string>(), TemplatesBuilderRoleProjectionCatalog.DnsServerRoleKey);
        return vm with { AdditionalRoles = updated };
    }

    private static IReadOnlyList<string> Append(IReadOnlyList<string> source, string value)
    {
        var list = source.ToList();
        list.Add(value);
        return list;
    }
}
