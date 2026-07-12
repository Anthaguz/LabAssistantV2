using System.Collections.Generic;
using System.Linq;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Behavior coverage for <see cref="TemplatesBuilderNetworkReconciler"/> - the pure function that keeps a
/// draft's network layout consistent with the locked one-switch-per-domain model. These tests build draft
/// snapshots directly (no XAML host, no Hyper-V) and assert the reconciler's contract: one Internal switch per
/// domain with a stable auto-allocated subnet, a dedicated standalone switch only in a domainless lab (standalone
/// machines ride the first domain's switch otherwise), a router bridging every switch at .1 ONLY when there are
/// at least two switches to route between (a single-subnet lab has none), VM IPs auto-assigned in subnet
/// (preserving valid ones, never duplicating), gateways through the router when one exists, DNS at the domain's
/// DC, and full idempotency.
/// </summary>
public sealed class TemplatesBuilderNetworkReconcilerTests
{
    [Fact]
    public void SingleDomain_CreatesOneInternalSwitchAndNoRouter()
    {
        var draft = Reconcile(DraftWith(
            domains: [Domain("d1", "contoso.lab", "CONTOSO")],
            vms: [Dc("dc", "d1")]));

        var network = Assert.Single(draft.LabNetworks);
        Assert.Equal("d1", network.DomainId);
        Assert.Equal("10.0.0.0/24", network.Subnet);
        Assert.Equal(V2SwitchTypeCatalog.Internal, network.SwitchType);

        // One switch means nothing to route between, so the lab carries no router VM.
        Assert.DoesNotContain(draft.Vms, vm => vm.IsRouter);
    }

    [Fact]
    public void DomainController_GetsInSubnetAddressAndSelfDnsButNoGatewayWithoutARouter()
    {
        var draft = Reconcile(DraftWith(
            domains: [Domain("d1", "contoso.lab", "CONTOSO")],
            vms: [Dc("dc", "d1")]));

        var dc = draft.Vms.Single(vm => vm.VmId == "dc");
        var nic = Assert.Single(dc.Nics);
        Assert.True(BuilderLabSubnet.TryParseCidr("10.0.0.0/24", out var subnet));
        Assert.True(BuilderLabSubnet.TryParseAddress(nic.IpAddress, out var address));
        Assert.True(subnet.Contains(address));
        // No router in a single-subnet lab, so no default route is pinned.
        Assert.Equal(string.Empty, nic.DefaultGateway);
        Assert.Equal([nic.IpAddress], nic.DnsServers);
    }

    [Fact]
    public void TwoDomains_AllocateSequentialSubnetsAndRouterBridgesBoth()
    {
        var draft = Reconcile(DraftWith(
            domains: [Domain("d1", "contoso.lab", "CONTOSO"), Domain("d2", "fabrikam.lab", "FABRIKAM")],
            vms: [Dc("dc1", "d1"), Dc("dc2", "d2")]));

        var subnets = draft.LabNetworks.Select(network => network.Subnet).OrderBy(value => value).ToList();
        Assert.Equal(["10.0.0.0/24", "10.0.1.0/24"], subnets);

        var router = draft.Vms.Single(vm => vm.IsRouter);
        var routerIps = router.Nics.Select(nic => nic.IpAddress).OrderBy(value => value).ToList();
        Assert.Equal(["10.0.0.1", "10.0.1.1"], routerIps);
    }

    [Fact]
    public void ExistingDomainSubnet_IsPreservedWhenAnotherDomainIsAdded()
    {
        // d1 already sits on 10.0.5.0/24; adding d2 must not renumber d1, and d2 takes the lowest free block.
        var existing = new TemplatesBuilderLabNetworkDraft("net-d1", "CONTOSO", "vSwitch-contoso", V2SwitchTypeCatalog.Internal, "10.0.5.0/24", string.Empty)
        {
            DomainId = "d1"
        };
        var draft = Reconcile(DraftWith(
            domains: [Domain("d1", "contoso.lab", "CONTOSO"), Domain("d2", "fabrikam.lab", "FABRIKAM")],
            vms: [Dc("dc1", "d1"), Dc("dc2", "d2")],
            networks: [existing]));

        Assert.Equal("10.0.5.0/24", draft.LabNetworks.Single(network => network.DomainId == "d1").Subnet);
        Assert.Equal("10.0.0.0/24", draft.LabNetworks.Single(network => network.DomainId == "d2").Subnet);
    }

    [Fact]
    public void StandaloneMachine_RidesFirstDomainSwitch_WithNoSeparateSwitchOrRouter()
    {
        var draft = Reconcile(DraftWith(
            domains: [Domain("d1", "contoso.lab", "CONTOSO")],
            vms: [Dc("dc", "d1"), Standalone("rootca")]));

        // A single domain plus a standalone machine stays at ONE switch: the standalone rides the domain switch,
        // so there is no dedicated standalone network and nothing to route, hence no router.
        var network = Assert.Single(draft.LabNetworks);
        Assert.Equal("d1", network.DomainId);
        Assert.DoesNotContain(draft.LabNetworks, n => string.IsNullOrEmpty(n.DomainId));
        Assert.DoesNotContain(draft.Vms, vm => vm.IsRouter);

        var standaloneVm = draft.Vms.Single(vm => vm.VmId == "rootca");
        var nic = Assert.Single(standaloneVm.Nics);
        Assert.Equal(network.NetworkId, nic.NetworkId);
        Assert.True(BuilderLabSubnet.TryParseCidr(network.Subnet, out var subnet));
        Assert.True(BuilderLabSubnet.TryParseAddress(nic.IpAddress, out var address));
        Assert.True(subnet.Contains(address));
    }

    [Fact]
    public void NoStandaloneMachine_ProducesNoStandaloneSwitch()
    {
        var draft = Reconcile(DraftWith(
            domains: [Domain("d1", "contoso.lab", "CONTOSO")],
            vms: [Dc("dc", "d1")]));

        Assert.DoesNotContain(draft.LabNetworks, network => string.IsNullOrEmpty(network.DomainId));
    }

    [Fact]
    public void RemovingADomain_DropsToOneSwitchAndRemovesTheRouter()
    {
        var two = Reconcile(DraftWith(
            domains: [Domain("d1", "contoso.lab", "CONTOSO"), Domain("d2", "fabrikam.lab", "FABRIKAM")],
            vms: [Dc("dc1", "d1"), Dc("dc2", "d2")]));
        Assert.Equal(2, two.Vms.Single(vm => vm.IsRouter).Nics.Count);

        // Drop d2 and its DC, then reconcile again: a single switch remains, so the router is retired.
        var reduced = two with
        {
            Domains = two.Domains.Where(domain => domain.DomainId != "d2").ToList(),
            Vms = two.Vms.Where(vm => vm.VmId != "dc2").ToList()
        };
        var after = Reconcile(reduced);

        Assert.Single(after.LabNetworks);
        Assert.DoesNotContain(after.Vms, vm => vm.IsRouter);
    }

    [Fact]
    public void ValidUserAddress_IsPreserved()
    {
        var member = Member("m1", "d1", ip: "10.0.0.50");
        var draft = Reconcile(DraftWith(
            domains: [Domain("d1", "contoso.lab", "CONTOSO")],
            vms: [Dc("dc", "d1"), member]));

        var kept = draft.Vms.Single(vm => vm.VmId == "m1");
        Assert.Equal("10.0.0.50", kept.Nics[0].IpAddress);
    }

    [Fact]
    public void OutOfSubnetAddress_IsReassignedInSubnet()
    {
        var member = Member("m1", "d1", ip: "192.168.99.5");
        var draft = Reconcile(DraftWith(
            domains: [Domain("d1", "contoso.lab", "CONTOSO")],
            vms: [Dc("dc", "d1"), member]));

        var reassigned = draft.Vms.Single(vm => vm.VmId == "m1");
        Assert.True(BuilderLabSubnet.TryParseCidr("10.0.0.0/24", out var subnet));
        Assert.True(BuilderLabSubnet.TryParseAddress(reassigned.Nics[0].IpAddress, out var address));
        Assert.True(subnet.Contains(address));
    }

    [Fact]
    public void DuplicateAddresses_AreDedupedNotPropagated()
    {
        // Two members both parked on .50; the reconciler keeps the first and moves the second off the collision.
        var draft = Reconcile(DraftWith(
            domains: [Domain("d1", "contoso.lab", "CONTOSO")],
            vms: [Dc("dc", "d1"), Member("m1", "d1", ip: "10.0.0.50"), Member("m2", "d1", ip: "10.0.0.50")]));

        var assigned = draft.Vms
            .Where(vm => vm.VmId is "m1" or "m2")
            .Select(vm => vm.Nics[0].IpAddress)
            .ToList();
        Assert.Equal(2, assigned.Distinct().Count());
        Assert.Contains("10.0.0.50", assigned);
    }

    [Fact]
    public void MemberDns_PointsAtItsDomainController()
    {
        var draft = Reconcile(DraftWith(
            domains: [Domain("d1", "contoso.lab", "CONTOSO")],
            vms: [Dc("dc", "d1"), Member("m1", "d1", ip: string.Empty)]));

        var dcAddress = draft.Vms.Single(vm => vm.VmId == "dc").Nics[0].IpAddress;
        var member = draft.Vms.Single(vm => vm.VmId == "m1");
        Assert.Equal([dcAddress], member.Nics[0].DnsServers);
    }

    [Fact]
    public void CreatedRouter_InheritsDiskAndLocalBootstrapFromAnExistingVm()
    {
        // Two domains yield two switches, which is what earns a router; the new router seeds its disk and local
        // bootstrap slot from an existing VM.
        var dc = Dc("dc1", "d1") with
        {
            VhdxId = "base-ws2022",
            CredentialSlots = new TemplatesBuilderVmCredentialSlotDraft("slot-local", "slot-admin", string.Empty, "slot-dsrm", string.Empty)
        };
        var draft = Reconcile(DraftWith(
            domains: [Domain("d1", "contoso.lab", "CONTOSO"), Domain("d2", "fabrikam.lab", "FABRIKAM")],
            vms: [dc, Dc("dc2", "d2")]));

        var router = draft.Vms.Single(vm => vm.IsRouter);
        Assert.Equal("base-ws2022", router.VhdxId);
        Assert.Equal("slot-local", router.CredentialSlots.LocalBootstrap);
    }

    [Fact]
    public void MultipleDomainsWithStandalone_HaveRouterAndStandaloneRidesFirstDomainSwitch()
    {
        var draft = Reconcile(DraftWith(
            domains: [Domain("d1", "contoso.lab", "CONTOSO"), Domain("d2", "fabrikam.lab", "FABRIKAM")],
            vms: [Dc("dc1", "d1"), Dc("dc2", "d2"), Standalone("rootca")]));

        // Two domains => two switches => a router. There is still no dedicated standalone switch; the standalone
        // machine rides the first domain's switch.
        Assert.Equal(2, draft.LabNetworks.Count);
        Assert.DoesNotContain(draft.LabNetworks, n => string.IsNullOrEmpty(n.DomainId));
        var router = Assert.Single(draft.Vms, vm => vm.IsRouter);
        Assert.Equal(2, router.Nics.Count);

        var firstDomainNetwork = draft.LabNetworks.Single(n => n.DomainId == "d1");
        var standaloneVm = draft.Vms.Single(vm => vm.VmId == "rootca");
        Assert.Equal(firstDomainNetwork.NetworkId, standaloneVm.Nics[0].NetworkId);
    }

    [Fact]
    public void StandaloneOnly_HasOneSwitchAndNoRouter()
    {
        var draft = Reconcile(DraftWith(domains: [], vms: [Standalone("box1"), Standalone("box2")]));

        var network = Assert.Single(draft.LabNetworks);
        Assert.Equal(string.Empty, network.DomainId);
        Assert.DoesNotContain(draft.Vms, vm => vm.IsRouter);
        // A router-less standalone switch pins no default route on its machines.
        Assert.All(draft.Vms, vm => Assert.Equal(string.Empty, vm.Nics[0].DefaultGateway));
    }

    [Fact]
    public void NoDomainsAndNoStandalone_ProducesNoNetworksAndNoRouter()
    {
        var draft = Reconcile(DraftWith(domains: [], vms: []));

        Assert.Empty(draft.LabNetworks);
        Assert.DoesNotContain(draft.Vms, vm => vm.IsRouter);
    }

    [Fact]
    public void HostReservation_IsNeverAssignedToAVm()
    {
        // Pack a /29 (10.0.0.0/29: .1 router, .6 host, assignable .2...5) so the host reservation is contended.
        var existing = new TemplatesBuilderLabNetworkDraft("net-d1", "CONTOSO", "vSwitch-contoso", V2SwitchTypeCatalog.Internal, "10.0.0.0/29", string.Empty)
        {
            DomainId = "d1"
        };
        var vms = new List<TemplatesBuilderVmDraft> { Dc("dc", "d1") };
        for (var i = 0; i < 5; i++)
        {
            vms.Add(Member($"m{i}", "d1", ip: string.Empty));
        }

        var draft = Reconcile(DraftWith(domains: [Domain("d1", "contoso.lab", "CONTOSO")], vms: vms, networks: [existing]));

        foreach (var vm in draft.Vms.Where(vm => !vm.IsRouter))
        {
            Assert.NotEqual("10.0.0.6", vm.Nics[0].IpAddress);
            Assert.NotEqual("10.0.0.1", vm.Nics[0].IpAddress);
        }
    }

    [Fact]
    public void Reconcile_IsIdempotent()
    {
        var once = Reconcile(DraftWith(
            domains: [Domain("d1", "contoso.lab", "CONTOSO"), Domain("d2", "fabrikam.lab", "FABRIKAM")],
            vms: [Dc("dc1", "d1"), Member("m1", "d1", ip: string.Empty), Dc("dc2", "d2"), Standalone("rootca")]));
        var twice = Reconcile(once);

        Assert.Equal(Fingerprint(once), Fingerprint(twice));
    }

    // ----- fingerprint for idempotency: stable projection of the reconciled network + NIC layout -----

    private static string Fingerprint(TemplatesBuilderDraftSnapshot draft)
    {
        var networks = string.Join(";", draft.LabNetworks
            .OrderBy(network => network.NetworkId)
            .Select(network => $"{network.NetworkId}|{network.DomainId}|{network.Subnet}|{network.SwitchType}"));
        var vms = string.Join(";", draft.Vms
            .OrderBy(vm => vm.VmId)
            .Select(vm =>
            {
                var nics = string.Join(",", (vm.Nics ?? []).Select(nic =>
                    $"{nic.NetworkId}:{nic.IpAddress}:{nic.DefaultGateway}:{string.Join("+", nic.DnsServers ?? [])}"));
                return $"{vm.VmId}|router={vm.IsRouter}|{nics}";
            }));
        return networks + "||" + vms;
    }

    // ----- builders -----

    private static TemplatesBuilderDraftSnapshot Reconcile(TemplatesBuilderDraftSnapshot draft)
        => TemplatesBuilderNetworkReconciler.Reconcile(draft);

    private static TemplatesBuilderDraftSnapshot DraftWith(
        IReadOnlyList<TemplatesBuilderDomainDraft> domains,
        IReadOnlyList<TemplatesBuilderVmDraft> vms,
        IReadOnlyList<TemplatesBuilderLabNetworkDraft>? networks = null)
        => new(
            TemplateName: "Lab",
            TemplateDescription: string.Empty,
            DeploymentProfile: "Balanced",
            LabNetworks: networks ?? [],
            CredentialSlots: [],
            Forests: domains.Select(domain => new TemplatesBuilderForestDraft($"forest-{domain.DomainId}", domain.DomainId)).ToList(),
            Domains: domains,
            Vms: vms,
            IsSaveConfirmed: false);

    private static TemplatesBuilderDomainDraft Domain(string id, string dns, string netbios)
        => new(id, dns, netbios, $"forest-{id}", "Root", string.Empty);

    private static TemplatesBuilderVmDraft Dc(string id, string domainId)
        => Vm(id, domainId, V2MembershipModeCatalog.DomainMember, isDc: true, ip: string.Empty);

    private static TemplatesBuilderVmDraft Member(string id, string domainId, string ip)
        => Vm(id, domainId, V2MembershipModeCatalog.DomainMember, isDc: false, ip: ip);

    private static TemplatesBuilderVmDraft Standalone(string id)
        => Vm(id, string.Empty, V2MembershipModeCatalog.Standalone, isDc: false, ip: string.Empty);

    private static TemplatesBuilderVmDraft Vm(string id, string domainId, string membership, bool isDc, string ip)
        => new(
            VmId: id,
            Name: id,
            MemoryMb: "2048",
            CpuCount: "2",
            VhdxId: string.Empty,
            MembershipMode: membership,
            DomainId: domainId,
            IsActiveDirectoryDomainController: isDc,
            CredentialSlots: new TemplatesBuilderVmCredentialSlotDraft(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            Nics:
            [
                new TemplatesBuilderNicDraft($"nic-{id}", "Primary", "old-network", "old-switch", ip, "24", string.Empty, [])
            ]);
}
