using System;
using System.Collections.Generic;
using System.Linq;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// How a machine's host address reads against its switch subnet. Drives the inspector's inline IP feedback
/// (for example a red outline) without the view re-deriving any CIDR math.
/// </summary>
internal enum MachineHostAddressStatus
{
    /// <summary>The address is a valid, assignable, non-duplicate host in the subnet.</summary>
    Ok,

    /// <summary>No address is set yet.</summary>
    Empty,

    /// <summary>The text is not a valid IPv4 literal.</summary>
    Invalid,

    /// <summary>The address parses but falls outside the switch subnet.</summary>
    OutOfSubnet,

    /// <summary>The address is the subnet's network id or directed broadcast (never assignable).</summary>
    NetworkOrBroadcast,

    /// <summary>The address is the router reservation (.1), which the router always holds.</summary>
    ReservedRouter,

    /// <summary>The address is the LabAssistant-host reservation (.254 on a /24).</summary>
    ReservedHost,

    /// <summary>Another machine on the same switch already uses this address.</summary>
    Duplicate
}

/// <summary>
/// A presentation-ready view of a machine's editable host address: the fixed subnet prefix the user cannot
/// change (for example "10.0.0."), how many trailing octets are editable (one for a /24, two for a /16), the
/// current octet values, and how the current address classifies. <see cref="IsEditable"/> is false when the
/// machine has no resolvable switch subnet or is the policy-managed router.
/// </summary>
internal readonly record struct MachineHostAddressView(
    bool IsEditable,
    string FixedOctetPrefix,
    int EditableOctetCount,
    IReadOnlyList<int> Octets,
    string SubnetCidr,
    MachineHostAddressStatus Status)
{
    /// <summary>The view for a machine whose address is not user-editable (router, or no resolvable subnet).</summary>
    public static MachineHostAddressView Unavailable { get; } =
        new(false, string.Empty, 0, Array.Empty<int>(), string.Empty, MachineHostAddressStatus.Empty);
}

/// <summary>
/// Pure authoring rules for editing an existing Level 2 machine's basic settings (name, vCPU count, memory,
/// base disk) and its last-octet host address. Like the other Builder engines, every mutating method takes an
/// immutable <see cref="TemplatesBuilderDraftSnapshot"/> and returns a new one, so the rules are unit-testable
/// without a XAML host and never touch Hyper-V.
///
/// IP composition and the reserved-address policy are delegated to <see cref="BuilderLabSubnet"/>, the single
/// source of truth for the per-domain switch model: the router holds the first usable address (.1), the
/// LabAssistant host holds the last usable address, and machines take the range in between. The router's own
/// addressing is policy-managed and therefore not editable from the inspector.
/// </summary>
internal static class TemplatesBuilderMachineBasicsAuthoring
{
    /// <summary>Sets the machine display name. The value is stored verbatim (uniqueness/format is a validator concern).</summary>
    public static MachineAuthoringResult SetMachineName(TemplatesBuilderDraftSnapshot draft, int vmIndex, string? name)
        => UpdateVm(draft, vmIndex, vm => vm with { Name = name ?? string.Empty });

    /// <summary>Sets the vCPU count text. Kept as raw text so the validator owns numeric-range enforcement.</summary>
    public static MachineAuthoringResult SetMachineCpuCount(TemplatesBuilderDraftSnapshot draft, int vmIndex, string? cpuCount)
        => UpdateVm(draft, vmIndex, vm => vm with { CpuCount = cpuCount ?? string.Empty });

    /// <summary>Sets the startup memory in MB (raw text; the validator owns numeric-range enforcement).</summary>
    public static MachineAuthoringResult SetMachineMemoryMb(TemplatesBuilderDraftSnapshot draft, int vmIndex, string? memoryMb)
        => UpdateVm(draft, vmIndex, vm => vm with { MemoryMb = memoryMb ?? string.Empty });

    /// <summary>Selects the machine's base disk by VHDX catalog id. Empty clears the selection.</summary>
    public static MachineAuthoringResult SetMachineBaseDisk(TemplatesBuilderDraftSnapshot draft, int vmIndex, string? vhdxId)
        => UpdateVm(draft, vmIndex, vm => vm with { VhdxId = (vhdxId ?? string.Empty).Trim() });

    /// <summary>
    /// Composes the machine's primary-NIC host address from the user-edited trailing octets and the switch
    /// subnet. No-ops when the machine has no resolvable subnet, is the policy-managed router, the octet count
    /// does not match the subnet, or any octet is out of range. The selection is preserved.
    /// </summary>
    public static MachineAuthoringResult SetMachineHostOctets(
        TemplatesBuilderDraftSnapshot draft,
        int vmIndex,
        IReadOnlyList<int> editableOctets)
    {
        if (vmIndex < 0 || vmIndex >= draft.Vms.Count || editableOctets is null)
        {
            return new MachineAuthoringResult(draft, vmIndex);
        }

        var vm = draft.Vms[vmIndex];
        if (vm.IsRouter || !TryResolvePrimaryNic(draft, vm, out var nicIndex, out var subnet))
        {
            return new MachineAuthoringResult(draft, vmIndex);
        }

        if (editableOctets.Count != subnet.EditableOctetCount || editableOctets.Any(octet => octet is < 0 or > 255))
        {
            return new MachineAuthoringResult(draft, vmIndex);
        }

        var address = subnet.ComposeHostAddress(editableOctets);
        var nics = vm.Nics.ToList();
        nics[nicIndex] = nics[nicIndex] with { IpAddress = BuilderLabSubnet.FormatAddress(address) };
        var vms = draft.Vms.ToList();
        vms[vmIndex] = vm with { Nics = nics };
        return new MachineAuthoringResult(draft with { Vms = vms, IsSaveConfirmed = false }, vmIndex);
    }

    /// <summary>
    /// Projects the editable host address for the machine at <paramref name="vmIndex"/>: the fixed prefix, the
    /// editable octet values, and how the current address classifies. Returns
    /// <see cref="MachineHostAddressView.Unavailable"/> for the router and for machines with no resolvable subnet.
    /// </summary>
    public static MachineHostAddressView ProjectHostAddress(TemplatesBuilderDraftSnapshot draft, int vmIndex)
    {
        if (vmIndex < 0 || vmIndex >= draft.Vms.Count)
        {
            return MachineHostAddressView.Unavailable;
        }

        var vm = draft.Vms[vmIndex];
        if (vm.IsRouter || !TryResolvePrimaryNic(draft, vm, out var nicIndex, out var subnet))
        {
            return MachineHostAddressView.Unavailable;
        }

        var nic = vm.Nics[nicIndex];
        var octets = BuilderLabSubnet.TryParseAddress(nic.IpAddress, out var current) && subnet.Contains(current)
            ? subnet.EditableOctetsOf(current)
            : new int[subnet.EditableOctetCount];

        return new MachineHostAddressView(
            IsEditable: true,
            FixedOctetPrefix: subnet.FixedOctetPrefix,
            EditableOctetCount: subnet.EditableOctetCount,
            Octets: octets,
            SubnetCidr: subnet.ToCidrString(),
            Status: ClassifyHostAddress(draft, vmIndex, subnet, nic.IpAddress));
    }

    /// <summary>Classifies a candidate host address against the subnet's reserved-address policy and its siblings.</summary>
    public static MachineHostAddressStatus ClassifyHostAddress(
        TemplatesBuilderDraftSnapshot draft,
        int vmIndex,
        BuilderLabSubnet subnet,
        string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return MachineHostAddressStatus.Empty;
        }

        if (!BuilderLabSubnet.TryParseAddress(ipAddress, out var address))
        {
            return MachineHostAddressStatus.Invalid;
        }

        if (!subnet.Contains(address))
        {
            return MachineHostAddressStatus.OutOfSubnet;
        }

        if (subnet.IsNetworkOrBroadcast(address))
        {
            return MachineHostAddressStatus.NetworkOrBroadcast;
        }

        if (subnet.IsRouterReserved(address))
        {
            return MachineHostAddressStatus.ReservedRouter;
        }

        if (subnet.IsHostReserved(address))
        {
            return MachineHostAddressStatus.ReservedHost;
        }

        return HasDuplicateOnSameNetwork(draft, vmIndex, address)
            ? MachineHostAddressStatus.Duplicate
            : MachineHostAddressStatus.Ok;
    }

    private static MachineAuthoringResult UpdateVm(
        TemplatesBuilderDraftSnapshot draft,
        int vmIndex,
        Func<TemplatesBuilderVmDraft, TemplatesBuilderVmDraft> mutate)
    {
        if (vmIndex < 0 || vmIndex >= draft.Vms.Count)
        {
            return new MachineAuthoringResult(draft, vmIndex);
        }

        var vms = draft.Vms.ToList();
        vms[vmIndex] = mutate(vms[vmIndex]);
        return new MachineAuthoringResult(draft with { Vms = vms, IsSaveConfirmed = false }, vmIndex);
    }

    // The primary NIC is the first NIC whose network resolves to a parseable switch subnet. Machines authored
    // here carry exactly one such NIC; the multi-NIC router is excluded upstream because its addressing is fixed.
    private static bool TryResolvePrimaryNic(
        TemplatesBuilderDraftSnapshot draft,
        TemplatesBuilderVmDraft vm,
        out int nicIndex,
        out BuilderLabSubnet subnet)
    {
        subnet = default;
        nicIndex = -1;
        for (var i = 0; i < vm.Nics.Count; i++)
        {
            if (TryResolveSubnet(draft, vm.Nics[i].NetworkId, out subnet))
            {
                nicIndex = i;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveSubnet(TemplatesBuilderDraftSnapshot draft, string? networkId, out BuilderLabSubnet subnet)
    {
        subnet = default;
        if (string.IsNullOrWhiteSpace(networkId))
        {
            return false;
        }

        var network = draft.LabNetworks.FirstOrDefault(candidate =>
            string.Equals(candidate.NetworkId?.Trim(), networkId.Trim(), StringComparison.OrdinalIgnoreCase));
        return !string.IsNullOrWhiteSpace(network.NetworkId) && BuilderLabSubnet.TryParseCidr(network.Subnet, out subnet);
    }

    private static bool HasDuplicateOnSameNetwork(TemplatesBuilderDraftSnapshot draft, int vmIndex, uint address)
    {
        var vm = draft.Vms[vmIndex];
        if (!TryResolvePrimaryNic(draft, vm, out var nicIndex, out _))
        {
            return false;
        }

        var networkId = vm.Nics[nicIndex].NetworkId?.Trim() ?? string.Empty;
        if (networkId.Length == 0)
        {
            return false;
        }

        for (var v = 0; v < draft.Vms.Count; v++)
        {
            if (v == vmIndex)
            {
                continue;
            }

            foreach (var nic in draft.Vms[v].Nics)
            {
                if (string.Equals(nic.NetworkId?.Trim(), networkId, StringComparison.OrdinalIgnoreCase) &&
                    BuilderLabSubnet.TryParseAddress(nic.IpAddress, out var other) &&
                    other == address)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
