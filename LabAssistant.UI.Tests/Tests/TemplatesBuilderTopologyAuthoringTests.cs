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
    public void DeleteDomain_ForestRoot_RemovesWholeForestAndAllItsDomainsAndVms()
    {
        var start = SuggestedDraft();
        var root = start.Domains[0];

        var result = TemplatesBuilderTopologyAuthoring.DeleteDomain(start, root.DomainId);
        var draft = result.Draft;

        Assert.Empty(draft.Forests);
        Assert.Empty(draft.Domains);
        // Both suggested VMs belonged to the deleted forest's domain, so none may reference a removed domain.
        Assert.DoesNotContain(draft.Vms, vm => vm.DomainId == root.DomainId);
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
    public void ResolveForestName_DerivesFromRootDomainDnsNameAndFollowsRename()
    {
        var start = SuggestedDraft();
        Assert.Equal("contoso.com", TemplatesBuilderTopologyAuthoring.ResolveForestName(start, start.Forests[0]));

        var renamedRoot = start.Domains[0] with { DnsName = "renamed.lab" };
        var renamed = start with { Domains = new[] { renamedRoot }.Concat(start.Domains.Skip(1)).ToList() };
        Assert.Equal("renamed.lab", TemplatesBuilderTopologyAuthoring.ResolveForestName(renamed, renamed.Forests[0]));
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
