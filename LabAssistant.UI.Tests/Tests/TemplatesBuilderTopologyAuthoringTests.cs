using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Runtime-independent coverage for the Phase 1 directory-topology authoring rules
/// (<see cref="TemplatesBuilderTopologyAuthoring"/>). These are the real bug surface of the canvas
/// redesign: they enforce one-root-per-forest, born-with-a-domain-controller, forest-name-follows-root,
/// and delete cascade, all on the immutable draft snapshot with no XAML host. Every "create" path is
/// asserted to leave the draft valid through the real <see cref="TemplatesBuilderDraftValidator"/>.
/// </summary>
public sealed class TemplatesBuilderTopologyAuthoringTests
{
    [Fact]
    public void AddForest_OnEmptyDraft_CreatesRootDomainBornWithDomainController()
    {
        var result = TemplatesBuilderTopologyAuthoring.AddForest(EmptyDraft());
        var draft = result.Draft;

        Assert.Single(draft.Forests);
        Assert.Single(draft.Domains);

        var forest = draft.Forests[0];
        var root = draft.Domains[0];
        Assert.Equal(nameof(V2DomainRelationKind.Root), root.RelationKind);
        Assert.Equal("contoso.lab", root.DnsName);
        Assert.Equal(root.DomainId, forest.RootDomainId);
        Assert.Equal(forest.ForestId, root.ForestId);

        var dc = Assert.Single(draft.Vms);
        Assert.True(dc.IsActiveDirectoryDomainController);
        Assert.Equal(V2MembershipModeCatalog.DomainMember, dc.MembershipMode);
        Assert.Equal(root.DomainId, dc.DomainId);
        Assert.Equal("contosodc01", dc.Name);

        Assert.Equal(BuilderForestDomainResourceKind.Forest, result.SelectedKind);
        Assert.Equal(0, result.SelectedIndex);
        AssertValid(draft);
    }

    [Fact]
    public void AddForest_Twice_UsesFriendlyCycleAndUniqueIdentifiers()
    {
        var first = TemplatesBuilderTopologyAuthoring.AddForest(EmptyDraft()).Draft;
        var second = TemplatesBuilderTopologyAuthoring.AddForest(first).Draft;

        Assert.Equal(2, second.Forests.Count);
        Assert.Equal("contoso.lab", second.Domains[0].DnsName);
        Assert.Equal("fabrikam.lab", second.Domains[1].DnsName);
        AssertDistinct(second.Forests.Select(forest => forest.ForestId));
        AssertDistinct(second.Domains.Select(domain => domain.DomainId));
        AssertDistinct(second.Vms.Select(vm => vm.VmId));
        AssertDistinct(second.Vms.Select(vm => vm.Name));
        AssertValid(second);
    }

    [Fact]
    public void AddTree_AddsTreeDomainWithNoParentBornWithDomainController()
    {
        var start = SuggestedDraft();
        var forestId = start.Forests[0].ForestId;

        var result = TemplatesBuilderTopologyAuthoring.AddTree(start, forestId);
        var draft = result.Draft;

        var tree = draft.Domains[result.SelectedIndex];
        Assert.Equal(BuilderForestDomainResourceKind.Domain, result.SelectedKind);
        Assert.Equal(nameof(V2DomainRelationKind.Tree), tree.RelationKind);
        Assert.Equal(string.Empty, tree.ParentDomainId);
        Assert.Equal(forestId, tree.ForestId);
        Assert.Contains(draft.Vms, vm => vm.IsActiveDirectoryDomainController && vm.DomainId == tree.DomainId);
        AssertValid(draft);
    }

    [Fact]
    public void AddTree_UnknownForest_IsNoOp()
    {
        var start = SuggestedDraft();
        var result = TemplatesBuilderTopologyAuthoring.AddTree(start, "forest-does-not-exist");
        Assert.Equal(start.Domains.Count, result.Draft.Domains.Count);
        Assert.Equal(start.Vms.Count, result.Draft.Vms.Count);
    }

    [Fact]
    public void AddChildDomain_LinksToParentAsSubdomainBornWithDomainController()
    {
        var start = SuggestedDraft();
        var parent = start.Domains[0];

        var result = TemplatesBuilderTopologyAuthoring.AddChildDomain(start, parent.DomainId);
        var draft = result.Draft;

        var child = draft.Domains[result.SelectedIndex];
        Assert.Equal(nameof(V2DomainRelationKind.Child), child.RelationKind);
        Assert.Equal(parent.DomainId, child.ParentDomainId);
        Assert.Equal(parent.ForestId, child.ForestId);
        Assert.EndsWith("." + parent.DnsName, child.DnsName);
        Assert.Contains(draft.Vms, vm => vm.IsActiveDirectoryDomainController && vm.DomainId == child.DomainId);
        AssertValid(draft);
    }

    [Fact]
    public void AddChildDomain_UnknownParent_IsNoOp()
    {
        var start = SuggestedDraft();
        var result = TemplatesBuilderTopologyAuthoring.AddChildDomain(start, "domain-nope");
        Assert.Equal(start.Domains.Count, result.Draft.Domains.Count);
    }

    [Fact]
    public void DeleteDomain_Child_RemovesChildAndItsDomainControllerButKeepsForest()
    {
        var start = SuggestedDraft();
        var withChild = TemplatesBuilderTopologyAuthoring.AddChildDomain(start, start.Domains[0].DomainId).Draft;
        var child = withChild.Domains.Last();

        var result = TemplatesBuilderTopologyAuthoring.DeleteDomain(withChild, child.DomainId);
        var draft = result.Draft;

        Assert.DoesNotContain(draft.Domains, domain => domain.DomainId == child.DomainId);
        Assert.DoesNotContain(draft.Vms, vm => vm.DomainId == child.DomainId);
        Assert.Contains(draft.Domains, domain => domain.DomainId == start.Domains[0].DomainId);
        Assert.Single(draft.Forests);
        AssertValid(draft);
    }

    [Fact]
    public void DeleteDomain_ForestRootWhenAnotherForestExists_RemovesThatForestAndKeepsTheOther()
    {
        // With more than one forest the root is deletable: deleting one forest's root removes that whole forest
        // and its domains/VMs while the other forest survives.
        var start = SuggestedDraft();
        var twoForests = TemplatesBuilderTopologyAuthoring.AddForest(start).Draft;
        var firstForestId = twoForests.Forests[0].ForestId;
        var root = twoForests.Domains.First(domain =>
            string.Equals(domain.ForestId, firstForestId, System.StringComparison.OrdinalIgnoreCase));

        var result = TemplatesBuilderTopologyAuthoring.DeleteDomain(twoForests, root.DomainId);
        var draft = result.Draft;

        Assert.Single(draft.Forests);
        Assert.DoesNotContain(draft.Forests, forest => forest.ForestId == firstForestId);
        Assert.DoesNotContain(draft.Domains, domain => domain.ForestId == firstForestId);
        Assert.DoesNotContain(draft.Vms, vm => vm.DomainId == root.DomainId);
        AssertValid(draft);
    }

    [Fact]
    public void DeleteDomain_ForestRootWithStandaloneMachines_KeepsStandaloneMachines()
    {
        // Regression: deleting one forest's root removes only that forest, never the standalone machines that
        // live outside every domain. A standalone VM carries no DomainId, so the delete cascade - which is
        // scoped to the removed forest's domains - must leave it untouched. This reproduces the reported bug
        // where deleting the first forest's root blanked the whole canvas, standalone machines included.
        var twoForests = TemplatesBuilderTopologyAuthoring.AddForest(SuggestedDraft()).Draft;
        var added = TemplatesBuilderMachineAuthoring.AddStandaloneComputer(twoForests);
        var standaloneVmId = added.Draft.Vms[added.SelectedVmIndex].VmId;
        var reconciled = TemplatesBuilderNetworkReconciler.Reconcile(added.Draft);

        var firstForestId = reconciled.Forests[0].ForestId;
        var root = reconciled.Domains.First(domain =>
            string.Equals(domain.ForestId, firstForestId, StringComparison.OrdinalIgnoreCase));

        var afterDelete = TemplatesBuilderNetworkReconciler.Reconcile(
            TemplatesBuilderTopologyAuthoring.DeleteDomain(reconciled, root.DomainId).Draft);

        Assert.DoesNotContain(afterDelete.Forests, forest => forest.ForestId == firstForestId);
        Assert.Contains(afterDelete.Vms, vm => vm.VmId == standaloneVmId);
        AssertValid(afterDelete);
    }

    [Fact]
    public void DeleteDomain_SoleForestRoot_RemovesTheForestAndKeepsStandaloneMachines()
    {
        // Deleting the root of the only forest is now allowed: the lab is left with just its standalone
        // machines (a valid workgroup-only template), and Level 1 can add a forest again, so it never
        // dead-ends. Save is separately gated on there being at least one machine.
        var start = SuggestedDraft();
        var standalone = new TemplatesBuilderVmDraft(
            "vm-rootca", "rootca", "2048", "2", start.Vms[0].VhdxId, V2MembershipModeCatalog.Standalone,
            string.Empty, false,
            new TemplatesBuilderVmCredentialSlotDraft("slot-local", string.Empty, string.Empty, string.Empty, string.Empty),
            []);
        var withStandalone = start with { Vms = start.Vms.Append(standalone).ToList() };
        var root = withStandalone.Domains[0];

        var result = TemplatesBuilderTopologyAuthoring.DeleteDomain(withStandalone, root.DomainId);

        Assert.Empty(result.Draft.Forests);
        Assert.Empty(result.Draft.Domains);
        Assert.Contains(result.Draft.Vms, vm => vm.VmId == "vm-rootca");
        Assert.DoesNotContain(result.Draft.Vms, vm => vm.DomainId == root.DomainId);
        // With no directory left but a workgroup machine surviving, focus lands on the Standalone container.
        Assert.Equal(BuilderForestDomainResourceKind.Standalone, result.SelectedKind);
    }

    [Fact]
    public void DeleteForest_SoleForest_RemovesEverythingInIt()
    {
        var start = SuggestedDraft();
        var forestId = start.Forests[0].ForestId;

        var result = TemplatesBuilderTopologyAuthoring.DeleteForest(start, forestId);

        Assert.Empty(result.Draft.Forests);
        Assert.Empty(result.Draft.Domains);
        Assert.DoesNotContain(result.Draft.Vms, vm => !string.IsNullOrWhiteSpace(vm.DomainId));
    }

    [Fact]
    public void DeleteDomain_TreeSubtree_RemovesTreeAndItsChildrenButKeepsRoot()
    {
        var start = SuggestedDraft();
        var forestId = start.Forests[0].ForestId;
        var withTree = TemplatesBuilderTopologyAuthoring.AddTree(start, forestId).Draft;
        var tree = withTree.Domains.Last();
        var withChild = TemplatesBuilderTopologyAuthoring.AddChildDomain(withTree, tree.DomainId).Draft;
        var child = withChild.Domains.Last();

        var result = TemplatesBuilderTopologyAuthoring.DeleteDomain(withChild, tree.DomainId);
        var draft = result.Draft;

        Assert.DoesNotContain(draft.Domains, domain => domain.DomainId == tree.DomainId);
        Assert.DoesNotContain(draft.Domains, domain => domain.DomainId == child.DomainId);
        Assert.DoesNotContain(draft.Vms, vm => vm.DomainId == tree.DomainId || vm.DomainId == child.DomainId);
        Assert.Contains(draft.Domains, domain => domain.DomainId == start.Domains[0].DomainId);
        Assert.Single(draft.Forests);
        AssertValid(draft);
    }

    [Fact]
    public void ApplyDomainRelationEdit_ForestRoot_StaysLockedToRoot()
    {
        var start = SuggestedDraft();
        var updated = TemplatesBuilderTopologyAuthoring.ApplyDomainRelationEdit(start, 0, nameof(V2DomainRelationKind.Child));
        Assert.Equal(nameof(V2DomainRelationKind.Root), updated.Domains[0].RelationKind);
    }

    [Fact]
    public void ApplyDomainRelationEdit_NonRootRequestingRoot_IsCoercedToTree()
    {
        var start = SuggestedDraft();
        var forestId = start.Forests[0].ForestId;
        var withTree = TemplatesBuilderTopologyAuthoring.AddTree(start, forestId).Draft;
        var treeIndex = withTree.Domains.Count - 1;

        var updated = TemplatesBuilderTopologyAuthoring.ApplyDomainRelationEdit(withTree, treeIndex, nameof(V2DomainRelationKind.Root));

        Assert.Equal(nameof(V2DomainRelationKind.Tree), updated.Domains[treeIndex].RelationKind);
        Assert.False(TemplatesBuilderTopologyAuthoring.IsForestRoot(updated, updated.Domains[treeIndex].DomainId));
    }

    [Fact]
    public void ApplyDomainRelationEdit_TreeToChild_SeedsForestRootAsParent()
    {
        var start = SuggestedDraft();
        var forestId = start.Forests[0].ForestId;
        var withTree = TemplatesBuilderTopologyAuthoring.AddTree(start, forestId).Draft;
        var treeIndex = withTree.Domains.Count - 1;

        var updated = TemplatesBuilderTopologyAuthoring.ApplyDomainRelationEdit(withTree, treeIndex, nameof(V2DomainRelationKind.Child));

        Assert.Equal(nameof(V2DomainRelationKind.Child), updated.Domains[treeIndex].RelationKind);
        Assert.Equal(start.Forests[0].RootDomainId, updated.Domains[treeIndex].ParentDomainId);
        AssertValid(updated);
    }

    [Fact]
    public void ApplyDomainRelationEdit_ChildToTree_ClearsParent()
    {
        var start = SuggestedDraft();
        var withChild = TemplatesBuilderTopologyAuthoring.AddChildDomain(start, start.Domains[0].DomainId).Draft;
        var childIndex = withChild.Domains.Count - 1;

        var updated = TemplatesBuilderTopologyAuthoring.ApplyDomainRelationEdit(withChild, childIndex, nameof(V2DomainRelationKind.Tree));

        Assert.Equal(nameof(V2DomainRelationKind.Tree), updated.Domains[childIndex].RelationKind);
        Assert.Equal(string.Empty, updated.Domains[childIndex].ParentDomainId);
        AssertValid(updated);
    }

    [Fact]
    public void ApplyDomainRelationEdit_OrphanDomainRequestingChild_CoercesToTreeInsteadOfEmptyParent()
    {
        // A malformed/orphan domain (its forest and root are absent) has no valid parent to become a Child of.
        // The edit must never produce a Child with an empty parent; it coerces to Tree instead.
        var orphan = new TemplatesBuilderDomainDraft(
            "domain-orphan", "orphan.lab", "ORPHAN", "forest-missing", nameof(V2DomainRelationKind.Tree), string.Empty);
        var draft = new TemplatesBuilderDraftSnapshot(
            "Orphan draft", string.Empty, "Balanced", [], [], [], [orphan], [], false);

        var updated = TemplatesBuilderTopologyAuthoring.ApplyDomainRelationEdit(draft, 0, nameof(V2DomainRelationKind.Child));

        Assert.Equal(nameof(V2DomainRelationKind.Tree), updated.Domains[0].RelationKind);
        Assert.Equal(string.Empty, updated.Domains[0].ParentDomainId);
    }

    [Fact]
    public void ResolveForestName_DerivesFromRootDomainDnsNameAndFollowsRename()
    {
        var start = SuggestedDraft();
        Assert.Equal("contoso.com", TemplatesBuilderTopologyAuthoring.ResolveForestName(start, start.Forests[0]));

        var renamedRoot = start.Domains[0] with { DnsName = "renamed.lab" };
        var renamed = start with { Domains = new[] { renamedRoot }.Concat(start.Domains.Skip(1)).ToList() };
        Assert.Equal("renamed.lab", TemplatesBuilderTopologyAuthoring.ResolveForestName(renamed, renamed.Forests[0]));
    }

    [Fact]
    public void AddForestTrust_BetweenTwoForests_CreatesForestBidirectionalTrustBetweenRoots()
    {
        var twoForests = TemplatesBuilderTopologyAuthoring.AddForest(SuggestedDraft()).Draft;
        var sourceRoot = twoForests.Forests[0].RootDomainId;
        var targetRoot = twoForests.Forests[1].RootDomainId;

        var result = TemplatesBuilderTopologyAuthoring.AddForestTrust(twoForests, 0, 1);
        var trusts = result.Draft.Trusts ?? [];

        var trust = Assert.Single(trusts);
        Assert.Equal(nameof(V2TrustType.Forest), trust.TrustType);
        Assert.Equal(nameof(V2TrustDirection.Bidirectional), trust.Direction);
        Assert.True(
            (trust.SourceDomainId == sourceRoot && trust.TargetDomainId == targetRoot) ||
            (trust.SourceDomainId == targetRoot && trust.TargetDomainId == sourceRoot));
        Assert.Equal(BuilderForestDomainResourceKind.Forest, result.SelectedKind);
    }

    [Fact]
    public void AddForestTrust_SameForest_IsNoOp()
    {
        var twoForests = TemplatesBuilderTopologyAuthoring.AddForest(SuggestedDraft()).Draft;

        var result = TemplatesBuilderTopologyAuthoring.AddForestTrust(twoForests, 0, 0);

        Assert.Empty(result.Draft.Trusts ?? []);
    }

    [Fact]
    public void AddForestTrust_DuplicateUnorderedPair_IsNoOp()
    {
        var twoForests = TemplatesBuilderTopologyAuthoring.AddForest(SuggestedDraft()).Draft;
        var once = TemplatesBuilderTopologyAuthoring.AddForestTrust(twoForests, 0, 1).Draft;

        // Authoring the reverse ordering is the same unordered pair and must not add a second trust.
        var twice = TemplatesBuilderTopologyAuthoring.AddForestTrust(once, 1, 0).Draft;

        Assert.Single(twice.Trusts ?? []);
    }

    [Fact]
    public void RemoveForestTrust_RemovesTrustById()
    {
        var twoForests = TemplatesBuilderTopologyAuthoring.AddForest(SuggestedDraft()).Draft;
        var authored = TemplatesBuilderTopologyAuthoring.AddForestTrust(twoForests, 0, 1).Draft;
        var trustId = (authored.Trusts ?? [])[0].TrustId;

        var result = TemplatesBuilderTopologyAuthoring.RemoveForestTrust(authored, trustId);

        Assert.Empty(result.Draft.Trusts ?? []);
    }

    [Fact]
    public void DeleteForest_PrunesTrustsAnchoredOnThatForest()
    {
        var twoForests = TemplatesBuilderTopologyAuthoring.AddForest(SuggestedDraft()).Draft;
        var authored = TemplatesBuilderTopologyAuthoring.AddForestTrust(twoForests, 0, 1).Draft;
        var doomedForestId = authored.Forests[0].ForestId;

        var result = TemplatesBuilderTopologyAuthoring.DeleteForest(authored, doomedForestId);

        Assert.Empty(result.Draft.Trusts ?? []);
    }

    [Fact]
    public void DeleteDomain_ForestRoot_PrunesTrustsAnchoredOnThatForest()
    {
        var twoForests = TemplatesBuilderTopologyAuthoring.AddForest(SuggestedDraft()).Draft;
        var authored = TemplatesBuilderTopologyAuthoring.AddForestTrust(twoForests, 0, 1).Draft;
        var doomedRoot = authored.Forests[0].RootDomainId;

        var result = TemplatesBuilderTopologyAuthoring.DeleteDomain(authored, doomedRoot);

        Assert.Empty(result.Draft.Trusts ?? []);
    }

    private static void AssertDistinct(IEnumerable<string> values)
    {
        var list = values.ToList();
        Assert.Equal(list.Count, list.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static void AssertValid(TemplatesBuilderDraftSnapshot draft)
    {
        var state = TemplatesBuilderDraftValidator.Validate(draft, TemplatesBuilderValidationRequest.All());
        Assert.True(
            state.Blockers.Count == 0,
            "Unexpected validation blockers: " + string.Join(" | ", state.Blockers.Select(issue => issue.Message)));
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

    private static TemplatesBuilderDraftSnapshot EmptyDraft()
        => new(
            TemplateName: "Empty Lab",
            TemplateDescription: string.Empty,
            DeploymentProfile: "Balanced",
            LabNetworks:
            [
                new TemplatesBuilderLabNetworkDraft("lab-core", "Core", "vSwitch-Core", string.Empty, "10.0.0.0/24", string.Empty)
            ],
            CredentialSlots:
            [
                new TemplatesBuilderCredentialSlotDraft("slot-local", "Local bootstrap", "local bootstrap"),
                new TemplatesBuilderCredentialSlotDraft("slot-admin", "Domain admin", "domain administration"),
                new TemplatesBuilderCredentialSlotDraft("slot-dsrm", "DSRM", "domain controller recovery")
            ],
            Forests: [],
            Domains: [],
            Vms: [],
            IsSaveConfirmed: false);
}
