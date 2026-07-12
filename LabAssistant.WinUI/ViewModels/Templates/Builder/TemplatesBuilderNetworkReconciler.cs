using System;
using System.Collections.Generic;
using System.Linq;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// Pure, idempotent reconciler that keeps a draft's network layout consistent with the one-switch-per-domain
/// model. It is applied after every structural authoring mutation (add/remove a domain or a machine) so the
/// draft always satisfies the locked design:
///
/// - every domain owns exactly one Internal switch, its subnet auto-allocated from a base plan
///   (10.0.0.0/24, 10.0.1.0/24, ...), stable across edits (an existing subnet is never reshuffled);
/// - standalone (workgroup) machines share a single standalone switch;
/// - a router VM is created ONLY when there are at least two switches to bridge (so a single-subnet lab - one
///   domain, or standalone-only - has no router, since there is nothing to route between); when present it
///   bridges every switch, holding the first usable address (.1) on each;
/// - the .1 slot is always reserved so VM addressing stays stable whether or not a router exists (adding a
///   second switch later must not renumber the machines that were already placed);
/// - the LabAssistant host implicitly holds the last usable address (.254 on a /24) - never assigned to a VM;
/// - each VM's primary NIC sits on its switch, is gatewayed through the router when one exists (no gateway in
///   a single-subnet lab, where a .1 default route would point at nothing), points DNS at its domain
///   controller, and is assigned the next free host address when it lacks a valid in-subnet one (user-set
///   valid addresses are preserved; duplicates are left for validation to flag, not silently reassigned).
///
/// Everything is value-to-value over the immutable draft snapshot, so it is fully unit-testable without a XAML
/// host and never touches Hyper-V.
/// </summary>
internal static class TemplatesBuilderNetworkReconciler
{
    /// <summary>The base subnet plan; each switch takes the next free /24 block by advancing the third octet.</summary>
    public const string BasePlanCidr = "10.0.0.0/24";

    private const string StandaloneNetworkId = "net-standalone";
    private const string StandaloneNetworkName = "Standalone";
    private const string StandaloneSwitchName = "vSwitch-standalone";
    private const string RouterVmId = "vm-router";
    private const string RouterVmName = "router01";
    private const string SwitchType = V2SwitchTypeCatalog.Internal;

    private static BuilderLabSubnet BasePlan
        => BuilderLabSubnet.TryParseCidr(BasePlanCidr, out var plan) ? plan : new BuilderLabSubnet(0x0A000000, 24);

    /// <summary>Reconciles the draft's networks, router, and VM IP assignments. Idempotent.</summary>
    public static TemplatesBuilderDraftSnapshot Reconcile(TemplatesBuilderDraftSnapshot draft)
    {
        var networks = EnsureNetworks(draft);
        var vms = EnsureRouter(draft.Vms ?? [], networks);
        var hasRouter = vms.Any(vm => vm.IsRouter);
        vms = AssignAddresses(vms, networks, draft.Domains ?? [], hasRouter);
        return draft with { LabNetworks = networks, Vms = vms };
    }

    // ----- networks: one per domain, plus a shared standalone switch when standalone machines exist -----

    private static IReadOnlyList<TemplatesBuilderLabNetworkDraft> EnsureNetworks(TemplatesBuilderDraftSnapshot draft)
    {
        var domains = draft.Domains ?? [];
        var existing = draft.LabNetworks ?? [];
        var basePlan = BasePlan;

        // Subnets already in use keep their block so an edit never renumbers an existing switch.
        var usedSubnets = new HashSet<uint>();
        foreach (var network in existing)
        {
            if (BuilderLabSubnet.TryParseCidr(network.Subnet, out var subnet))
            {
                usedSubnets.Add(subnet.NetworkAddress);
            }
        }

        var takenSwitchNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<TemplatesBuilderLabNetworkDraft>();

        // One switch per domain, preserving an existing domain switch (subnet/name) and creating any missing one.
        foreach (var domain in domains)
        {
            var current = existing.FirstOrDefault(network =>
                string.Equals(network.DomainId, domain.DomainId, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(domain.DomainId));

            if (!string.IsNullOrWhiteSpace(current.NetworkId) && BuilderLabSubnet.TryParseCidr(current.Subnet, out _))
            {
                var preserved = current with
                {
                    SwitchType = SwitchType,
                    Name = string.IsNullOrWhiteSpace(current.Name) ? DomainNetworkName(domain) : current.Name,
                    SwitchName = ClaimSwitchName(current.SwitchName, domain, takenSwitchNames)
                };
                result.Add(preserved);
                continue;
            }

            var subnet = AllocateNextSubnet(basePlan, usedSubnets);
            result.Add(new TemplatesBuilderLabNetworkDraft(
                NetworkId: $"net-{domain.DomainId}",
                Name: DomainNetworkName(domain),
                SwitchName: ClaimSwitchName(current.SwitchName, domain, takenSwitchNames),
                SwitchType: SwitchType,
                Subnet: subnet.ToCidrString(),
                Notes: string.Empty)
            {
                DomainId = domain.DomainId
            });
        }

        // A single standalone switch, only when there is a standalone (non-router) machine to seat on it.
        var needsStandalone = (draft.Vms ?? []).Any(vm =>
            V2MembershipModeCatalog.IsStandalone(vm.MembershipMode) && !vm.IsRouter);
        if (needsStandalone)
        {
            var current = existing.FirstOrDefault(network =>
                string.Equals(network.NetworkId, StandaloneNetworkId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(current.NetworkId) && BuilderLabSubnet.TryParseCidr(current.Subnet, out _))
            {
                result.Add(current with { SwitchType = SwitchType, DomainId = string.Empty });
            }
            else
            {
                var subnet = AllocateNextSubnet(basePlan, usedSubnets);
                result.Add(new TemplatesBuilderLabNetworkDraft(
                    NetworkId: StandaloneNetworkId,
                    Name: StandaloneNetworkName,
                    SwitchName: StandaloneSwitchName,
                    SwitchType: SwitchType,
                    Subnet: subnet.ToCidrString(),
                    Notes: string.Empty)
                {
                    DomainId = string.Empty
                });
            }
        }

        return result;
    }

    private static BuilderLabSubnet AllocateNextSubnet(BuilderLabSubnet basePlan, ISet<uint> usedNetworkAddresses)
    {
        for (var ordinal = 0; ordinal < 4096; ordinal++)
        {
            var candidate = basePlan.AllocateAt(ordinal);
            if (usedNetworkAddresses.Add(candidate.NetworkAddress))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Exhausted the lab subnet plan while allocating a switch.");
    }

    private static string ClaimSwitchName(string? preferred, TemplatesBuilderDomainDraft domain, ISet<string> taken)
    {
        var seed = string.IsNullOrWhiteSpace(preferred) ? $"vSwitch-{Token(domain)}" : preferred.Trim();
        var candidate = seed;
        var suffix = 2;
        while (!taken.Add(candidate))
        {
            candidate = $"{seed}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string DomainNetworkName(TemplatesBuilderDomainDraft domain)
        => string.IsNullOrWhiteSpace(domain.NetBiosName) ? Token(domain) : domain.NetBiosName.Trim();

    private static string Token(TemplatesBuilderDomainDraft domain)
    {
        var source = !string.IsNullOrWhiteSpace(domain.DnsName) ? domain.DnsName : domain.NetBiosName;
        var label = (source ?? string.Empty).Trim().Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var sanitized = new string((label ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return string.IsNullOrWhiteSpace(sanitized) ? "lab" : sanitized;
    }

    // ----- router: a single standalone VM with a NIC (holding .1) on every switch, only when >= 2 switches -----

    private static IReadOnlyList<TemplatesBuilderVmDraft> EnsureRouter(
        IReadOnlyList<TemplatesBuilderVmDraft> vms,
        IReadOnlyList<TemplatesBuilderLabNetworkDraft> networks)
    {
        var withoutRouter = vms.Where(vm => !vm.IsRouter).ToList();

        // The router only earns its keep when there are at least two switches to bridge and route between. A
        // single-subnet lab (one domain, or standalone-only) needs no router, so drop any stale one it may hold.
        if (networks.Count < 2)
        {
            return withoutRouter;
        }

        var existingRouter = vms.FirstOrDefault(vm => vm.IsRouter);
        var routerNics = networks
            .Select(network =>
            {
                BuilderLabSubnet.TryParseCidr(network.Subnet, out var subnet);
                var routerIp = subnet.RouterAddress is { } address ? BuilderLabSubnet.FormatAddress(address) : string.Empty;
                return new TemplatesBuilderNicDraft(
                    NicId: $"nic-router-{network.NetworkId}",
                    Name: $"to-{network.SwitchName}",
                    NetworkId: network.NetworkId,
                    SwitchName: network.SwitchName,
                    IpAddress: routerIp,
                    PrefixLength: subnet.PrefixLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    DefaultGateway: string.Empty,
                    DnsServers: []);
            })
            .ToList();

        TemplatesBuilderVmDraft router;
        if (!string.IsNullOrWhiteSpace(existingRouter.VmId))
        {
            router = existingRouter with
            {
                MembershipMode = V2MembershipModeCatalog.Standalone,
                DomainId = string.Empty,
                IsActiveDirectoryDomainController = false,
                Nics = routerNics
            };
        }
        else
        {
            // The router runs the same base OS as the rest of the lab, so seed its disk and local-bootstrap slot
            // from an existing VM. The user can override both in the detail panel; a later reconcile preserves them.
            var donor = withoutRouter.FirstOrDefault(vm => !string.IsNullOrWhiteSpace(vm.VhdxId));
            var localSlot = withoutRouter
                .Select(vm => vm.CredentialSlots.LocalBootstrap)
                .FirstOrDefault(slot => !string.IsNullOrWhiteSpace(slot)) ?? string.Empty;
            router = new TemplatesBuilderVmDraft(
                VmId: RouterVmId,
                Name: RouterVmName,
                MemoryMb: "2048",
                CpuCount: "2",
                VhdxId: donor.VhdxId ?? string.Empty,
                MembershipMode: V2MembershipModeCatalog.Standalone,
                DomainId: string.Empty,
                IsActiveDirectoryDomainController: false,
                CredentialSlots: new TemplatesBuilderVmCredentialSlotDraft(localSlot, string.Empty, string.Empty, string.Empty, string.Empty),
                Nics: routerNics)
            {
                IsRouter = true
            };
        }

        // The router leads the VM list so it is the visible anchor of the standalone infrastructure.
        var ordered = new List<TemplatesBuilderVmDraft> { router };
        ordered.AddRange(withoutRouter);
        return ordered;
    }

    // ----- IP assignment: DC-first sequential fill per switch, preserving valid user addresses -----

    private static IReadOnlyList<TemplatesBuilderVmDraft> AssignAddresses(
        IReadOnlyList<TemplatesBuilderVmDraft> vms,
        IReadOnlyList<TemplatesBuilderLabNetworkDraft> networks,
        IReadOnlyList<TemplatesBuilderDomainDraft> domains,
        bool hasRouter)
    {
        var domainNetwork = networks
            .Where(network => !string.IsNullOrWhiteSpace(network.DomainId))
            .GroupBy(network => network.DomainId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var standaloneNetwork = networks.FirstOrDefault(network => string.IsNullOrWhiteSpace(network.DomainId));

        var updated = vms.ToList();
        var usedByNetwork = new Dictionary<string, HashSet<uint>>(StringComparer.OrdinalIgnoreCase);

        HashSet<uint> UsedFor(string networkId)
        {
            if (!usedByNetwork.TryGetValue(networkId, out var set))
            {
                set = new HashSet<uint>();
                usedByNetwork[networkId] = set;
            }

            return set;
        }

        // Seed each network's used set with only the router's reservation (.1). VM addresses are added as each VM
        // is bound below, in deterministic DC-then-member order - never pre-seeded - so a VM's own address is
        // never in "used" when we decide whether to keep it. That keeps Reconcile idempotent while still letting
        // an address collision with an EARLIER-bound VM force a reassignment (no reconcile-introduced duplicates).
        foreach (var network in networks)
        {
            if (BuilderLabSubnet.TryParseCidr(network.Subnet, out var subnet) && subnet.RouterAddress is { } routerAddress)
            {
                UsedFor(network.NetworkId).Add(routerAddress);
            }
        }

        // Resolve each domain's DC address first (members point DNS at it), then fill members and standalones.
        var dcAddressByDomain = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Pass 1: domain controllers.
        for (var i = 0; i < updated.Count; i++)
        {
            var vm = updated[i];
            if (vm.IsRouter || !vm.IsActiveDirectoryDomainController || string.IsNullOrWhiteSpace(vm.DomainId) ||
                !domainNetwork.TryGetValue(vm.DomainId, out var network))
            {
                continue;
            }

            var (bound, address) = BindNic(vm, network, gatewayDns: null, UsedFor(network.NetworkId), hasRouter);
            updated[i] = bound;
            if (!string.IsNullOrWhiteSpace(address) && !dcAddressByDomain.ContainsKey(vm.DomainId))
            {
                dcAddressByDomain[vm.DomainId] = address;
            }
        }

        // Pass 2: domain members and standalones.
        for (var i = 0; i < updated.Count; i++)
        {
            var vm = updated[i];
            if (vm.IsRouter || vm.IsActiveDirectoryDomainController)
            {
                continue;
            }

            if (V2MembershipModeCatalog.IsDomainMember(vm.MembershipMode) && !string.IsNullOrWhiteSpace(vm.DomainId) &&
                domainNetwork.TryGetValue(vm.DomainId, out var memberNetwork))
            {
                dcAddressByDomain.TryGetValue(vm.DomainId, out var dnsAddress);
                var (bound, _) = BindNic(vm, memberNetwork, dnsAddress, UsedFor(memberNetwork.NetworkId), hasRouter);
                updated[i] = bound;
            }
            else if (V2MembershipModeCatalog.IsStandalone(vm.MembershipMode) && !string.IsNullOrWhiteSpace(standaloneNetwork.NetworkId))
            {
                var (bound, _) = BindNic(vm, standaloneNetwork, gatewayDns: null, UsedFor(standaloneNetwork.NetworkId), hasRouter);
                updated[i] = bound;
            }
        }

        return updated;
    }

    // Binds a VM's primary NIC to a network: pins network/switch/prefix/gateway/DNS and assigns the next free
    // host address unless the NIC already holds a valid in-subnet one. Returns the VM and its assigned address.
    private static (TemplatesBuilderVmDraft Vm, string Address) BindNic(
        TemplatesBuilderVmDraft vm,
        TemplatesBuilderLabNetworkDraft network,
        string? gatewayDns,
        HashSet<uint> used,
        bool hasRouter)
    {
        if (!BuilderLabSubnet.TryParseCidr(network.Subnet, out var subnet))
        {
            return (vm, string.Empty);
        }

        var nics = (vm.Nics ?? []).ToList();
        var existing = nics.Count > 0 ? nics[0] : new TemplatesBuilderNicDraft($"nic-{vm.Name}", "Primary", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, []);

        uint chosen;
        var keepsCurrent = BuilderLabSubnet.TryParseAddress(existing.IpAddress, out var current) &&
                           subnet.Contains(current) && !subnet.IsNetworkOrBroadcast(current) &&
                           !subnet.IsRouterReserved(current) && !subnet.IsHostReserved(current) &&
                           !used.Contains(current);
        if (keepsCurrent)
        {
            chosen = current;
        }
        else if (subnet.NextFreeHost(used) is { } free)
        {
            chosen = free;
        }
        else
        {
            // Subnet full: leave the NIC address empty but still pin the rest so validation can flag it.
            chosen = 0;
        }

        var address = chosen == 0 && !keepsCurrent ? string.Empty : BuilderLabSubnet.FormatAddress(chosen);
        if (!string.IsNullOrWhiteSpace(address) && BuilderLabSubnet.TryParseAddress(address, out var addr))
        {
            used.Add(addr);
        }

        // A default route is only meaningful when a router actually holds .1; in a single-subnet lab we leave the
        // gateway empty rather than point every VM at a non-existent .1.
        var router = hasRouter && subnet.RouterAddress is { } routerAddress ? BuilderLabSubnet.FormatAddress(routerAddress) : string.Empty;
        IReadOnlyList<string> dns = vm.IsActiveDirectoryDomainController && !string.IsNullOrWhiteSpace(address)
            ? [address]
            : !string.IsNullOrWhiteSpace(gatewayDns) ? [gatewayDns!] : existing.DnsServers ?? [];

        var boundNic = existing with
        {
            NetworkId = network.NetworkId,
            SwitchName = network.SwitchName,
            IpAddress = address,
            PrefixLength = subnet.PrefixLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DefaultGateway = router,
            DnsServers = dns
        };

        if (nics.Count > 0)
        {
            nics[0] = boundNic;
        }
        else
        {
            nics.Add(boundNic);
        }

        return (vm with { Nics = nics }, address);
    }
}
