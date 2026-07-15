using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Runtime-independent coverage for the migrated <see cref="TemplatesBuilderViewModel"/>'s interaction
/// contract: linear + free step navigation and its gating, the review projection deriving from current
/// validation state, draft &lt;-&gt; <see cref="LabTemplate"/> mapping round-trips, and the live-refresh
/// + caret-preservation invariant (editing a per-field view model refreshes the resource lists / VM
/// overview / review without rebuilding the focused detail panel). All exercised with no dispatcher and
/// no Hyper-V, mirroring how the declarative x:Bind view drives the view model.
/// </summary>
public sealed class TemplatesBuilderViewModelInteractionTests
{
    [Fact]
    public void BuilderStepNavigation_LinearNextTraversesTopStepsThenDrillsIntoVmSections()
    {
        var viewModel = CreateViewModel();
        var draft = NamedDraft();
        Assert.NotEmpty(draft.Vms);
        viewModel.LoadNewDraft(draft);

        // Linear Next walks the four leading steps in order (a render has to run first; LoadNewDraft
        // does not render, so the first Next renders and advances).
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsNetworksVisible);

        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsForestsDomainsVisible);

        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsCredentialsVisible);

        // Fifth Next lands on the VM step at its overview.
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsVmsVisible);
        Assert.True(viewModel.IsVmOverviewVisible);
        Assert.False(viewModel.IsVmDetailVisible);

        // Continuing Next drills into the first VM's sections (still the VM step, now a detail route) -
        // linear navigation traverses the flattened route list, not just the six top-level steps.
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsVmsVisible);
        Assert.False(viewModel.IsVmOverviewVisible);
        Assert.True(viewModel.IsVmDetailVisible);

        // Previous walks back out to the VM overview.
        viewModel.PreviousStepCommand.Execute(null);
        Assert.True(viewModel.IsVmsVisible);
        Assert.True(viewModel.IsVmOverviewVisible);

        // And back up through the leading steps.
        viewModel.PreviousStepCommand.Execute(null);
        Assert.True(viewModel.IsCredentialsVisible);
    }

    [Fact]
    public void BuilderStepNavigation_FreeJumpViaNavigatorRowSelectsAnyStep()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());

        // Render the navigator (root depth exposes all six steps in order).
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsNetworksVisible);
        Assert.Equal(6, viewModel.NavigatorRows.Count);

        // Jump straight to Review (index 5), skipping the intermediate steps.
        viewModel.NavigatorRows[5].Command!.Execute(null);
        Assert.True(viewModel.IsReviewVisible);
        // Review is the terminal step: Next hides, Save surfaces.
        Assert.False(viewModel.NextStepVisible);
        Assert.True(viewModel.SaveVisible);

        // Jump straight back to General (index 0).
        viewModel.NavigatorRows[0].Command!.Execute(null);
        Assert.True(viewModel.IsGeneralVisible);
    }

    [Fact]
    public void BuilderStepNavigation_WithoutActiveDraft_IsGatedOff()
    {
        var viewModel = CreateViewModel();

        // No draft loaded: the footer navigation is gated off.
        Assert.False(viewModel.HasActiveDraft);
        Assert.False(viewModel.NextStepEnabled);
        Assert.False(viewModel.PreviousStepEnabled);

        // Loading a draft and rendering enables forward navigation.
        viewModel.LoadNewDraft(NamedDraft());
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.HasActiveDraft);
        Assert.True(viewModel.NextStepEnabled);
    }

    [Fact]
    public void BuilderReview_CleanSuggestedDraft_ShowsNoBlockerAndSummarizesState()
    {
        var viewModel = CreateViewModel();
        var draft = NamedDraft();

        // A clean suggested draft validates without blockers.
        viewModel.LoadNewDraft(draft);
        Assert.False(viewModel.HasValidationBlockers, string.Join(" | ", viewModel.ValidationState.Blockers.Select(issue => issue.Message)));

        // Rendering derives the review projection from validation state - no manual Validate action.
        viewModel.SetDeploymentProfileCommand.Execute("Balanced");
        Assert.Equal(
            viewModel.ValidationState.HasBlockers || viewModel.ValidationState.Warnings.Count > 0,
            viewModel.IsReviewBlockerVisible);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ReviewSummaryText));
    }

    [Fact]
    public void BuilderSave_OnCanvasDraftWithoutReviewNavigation_IsVisibleAndEnabled()
    {
        // Phase 4 retired the stepper/Review route: the topbar Save / Save As must be visible and enabled on a clean
        // draft without ever navigating to the (now-removed) Review step. Regression guard for the shell refactor that
        // left Save gated on the unreachable Review route, which hid and disabled both buttons permanently.
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());

        Assert.False(
            viewModel.HasValidationBlockers,
            string.Join(" | ", viewModel.ValidationState.Blockers.Select(issue => issue.Message)));
        Assert.True(viewModel.SaveVisible);
        Assert.True(viewModel.SaveEnabled);
        Assert.True(viewModel.SaveAsVisible);
        Assert.True(viewModel.SaveAsEnabled);
    }

    [Fact]
    public void BuilderDraftMapping_RoundTripsVmsDomainsAndNetworksThroughLabTemplate()
    {
        var draft = NamedDraft();
        Assert.NotEmpty(draft.Vms);

        var build = TemplatesBuilderDraftMapper.BuildDocument(draft, "template-round-trip", 1, "1.0.0", sourceFilePath: null);
        Assert.NotNull(build.Document);

        var template = build.Document!.Template;
        var mappedBack = TemplatesBuilderDraftMapper.FromTemplate(template);

        Assert.Equal(draft.TemplateName, mappedBack.TemplateName);
        Assert.Equal(draft.Vms.Count, mappedBack.Vms.Count);
        Assert.Equal(
            draft.Vms.Select(vm => vm.Name),
            mappedBack.Vms.Select(vm => vm.Name));
        Assert.Equal(draft.Domains.Count, mappedBack.Domains.Count);
        Assert.Equal(
            draft.Domains.Select(domain => domain.DnsName),
            mappedBack.Domains.Select(domain => domain.DnsName));
        Assert.Equal(draft.LabNetworks.Count, mappedBack.LabNetworks.Count);
    }

    [Fact]
    public void BuilderDraftMapping_RoundTripsForestTrustThroughLabTemplate()
    {
        // A forest trust authored between two forests persists as a Forest / Bidirectional trust anchored on the
        // two forests' root domain ids, and must survive draft -> LabTemplate -> draft without loss.
        var twoForests = TemplatesBuilderTopologyAuthoring.AddForest(NamedDraft()).Draft;
        var authored = TemplatesBuilderTopologyAuthoring.AddForestTrust(twoForests, 0, 1).Draft;
        var expected = (authored.Trusts ?? [])[0];

        var build = TemplatesBuilderDraftMapper.BuildDocument(authored, "template-forest-trust", 1, "1.0.0", sourceFilePath: null);
        Assert.NotNull(build.Document);

        var persisted = Assert.Single(build.Document!.Template.DirectoryTopology!.Trusts);
        Assert.Equal(V2TrustType.Forest, persisted.TrustType);
        Assert.Equal(V2TrustDirection.Bidirectional, persisted.Direction);

        var mappedBack = TemplatesBuilderDraftMapper.FromTemplate(build.Document.Template);
        var trust = Assert.Single(mappedBack.Trusts ?? []);
        Assert.Equal(expected.TrustId, trust.TrustId);
        Assert.Equal(expected.SourceDomainId, trust.SourceDomainId);
        Assert.Equal(expected.TargetDomainId, trust.TargetDomainId);
        Assert.Equal(nameof(V2TrustType.Forest), trust.TrustType);
        Assert.Equal(nameof(V2TrustDirection.Bidirectional), trust.Direction);
    }

    [Fact]
    public void BuilderDraftMapping_PreservesHandAuthoredExternalTrust()
    {
        // A hand-authored / unknown trust (e.g. an External domain-to-domain trust, a future concept) must not be
        // silently dropped by the round-trip - the mapper preserves whatever trusts the draft carries.
        var baseDraft = NamedDraft();
        var external = new TemplatesBuilderTrustDraft(
            "trust-hand-authored",
            baseDraft.Domains[0].DomainId,
            "domain-partner",
            nameof(V2TrustType.External),
            nameof(V2TrustDirection.Inbound));
        var draft = baseDraft with { Trusts = new[] { external } };

        var build = TemplatesBuilderDraftMapper.BuildDocument(draft, "template-external-trust", 1, "1.0.0", sourceFilePath: null);
        Assert.NotNull(build.Document);

        var mappedBack = TemplatesBuilderDraftMapper.FromTemplate(build.Document!.Template);
        var trust = Assert.Single(mappedBack.Trusts ?? []);
        Assert.Equal("trust-hand-authored", trust.TrustId);
        Assert.Equal(nameof(V2TrustType.External), trust.TrustType);
        Assert.Equal(nameof(V2TrustDirection.Inbound), trust.Direction);
        Assert.Equal("domain-partner", trust.TargetDomainId);
    }

    [Fact]
    public void ForestTrust_SurvivesUnrelatedAuthoringEditThroughCaptureApply()
    {
        // The trust model lives on the draft snapshot as an init-only property. An unrelated authoring edit
        // reconstructs the snapshot; the trust must survive that capture -> apply round-trip.
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        var twoForests = TemplatesBuilderTopologyAuthoring.AddForest(viewModel.CaptureDraft()).Draft;
        var withTrust = TemplatesBuilderTopologyAuthoring.AddForestTrust(twoForests, 0, 1).Draft;
        viewModel.ApplyDraft(withTrust);
        Assert.Single(viewModel.CaptureDraft().Trusts ?? []);

        // An unrelated edit (add a standalone machine) reconstructs the snapshot without touching trusts.
        var edited = TemplatesBuilderMachineAuthoring.AddStandaloneComputer(viewModel.CaptureDraft()).Draft;
        viewModel.ApplyDraft(edited);

        Assert.Single(viewModel.CaptureDraft().Trusts ?? []);
    }

    [Fact]
    public void ForestTrustAffordance_AddThenRemoveTrustFromSelectedForest()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        // Render so the forest/domain detail panel populates, then add a second forest to make trusts possible.
        viewModel.NextStepCommand.Execute(null);
        var twoForests = TemplatesBuilderTopologyAuthoring.AddForest(viewModel.CaptureDraft()).Draft;
        viewModel.ApplyDraft(twoForests);

        // Select the first forest via its canvas frame so the affordance state renders.
        var forestFrameId = viewModel.CaptureDraft().Forests[0].ForestId;
        var frame = viewModel.TopologyCanvas!.Frames.First(f => f.FrameId.Contains(forestFrameId));
        frame.SelectCommand!.Execute(null);

        Assert.True(viewModel.CanAuthorForestTrust);
        Assert.NotNull(viewModel.SelectedForestTrustTarget);

        viewModel.AddForestTrustCommand.Execute(null);
        Assert.Single(viewModel.CaptureDraft().Trusts ?? []);

        // Re-select the source forest and remove the trust via its row command.
        var sourceForestId = viewModel.CaptureDraft().Trusts![0].SourceDomainId;
        var sourceFrame = viewModel.TopologyCanvas!.Frames.First(f =>
            viewModel.CaptureDraft().Forests.Any(forest =>
                f.FrameId.Contains(forest.ForestId) && forest.RootDomainId == sourceForestId));
        sourceFrame.SelectCommand!.Execute(null);
        Assert.NotEmpty(viewModel.ForestTrustRows);
        viewModel.ForestTrustRows[0].RemoveCommand.Execute(null);

        Assert.Empty(viewModel.CaptureDraft().Trusts ?? []);
    }

    [Fact]
    public void BuilderDraftMapping_RoundTripsStandaloneMachineThroughLabTemplateWithoutLoss()
    {
        // A standalone machine (a workgroup box: router, root CA, ...) must survive the draft -> LabTemplate ->
        // draft round-trip as a standalone with no domain, so Level 2 authoring never weakens the contract.
        // Reconcile first (as the app does on every edit) so both sides agree on the auto-created router that a
        // second switch brings.
        var draft = TemplatesBuilderNetworkReconciler.Reconcile(
            TemplatesBuilderMachineAuthoring.AddStandaloneComputer(NamedDraft()).Draft);
        var standaloneName = draft.Vms.Single(vm => V2MembershipModeCatalog.IsStandalone(vm.MembershipMode) && !vm.IsRouter).Name;

        var build = TemplatesBuilderDraftMapper.BuildDocument(draft, "template-standalone-round-trip", 1, "1.0.0", sourceFilePath: null);
        Assert.NotNull(build.Document);

        var mappedBack = TemplatesBuilderDraftMapper.FromTemplate(build.Document!.Template);

        Assert.Equal(draft.Vms.Count, mappedBack.Vms.Count);
        var standalone = mappedBack.Vms.Single(vm => vm.Name == standaloneName);
        Assert.True(V2MembershipModeCatalog.IsStandalone(standalone.MembershipMode));
        Assert.True(string.IsNullOrEmpty(standalone.DomainId));
        Assert.False(standalone.IsActiveDirectoryDomainController);
    }

    [Fact]
    public void BuilderDraftMapping_RoundTripsAuthoredNonStructuralRoleThroughRoleConfig()
    {
        // Authoring a non-structural role (here DHCP on a member server) must survive the
        // draft -> LabTemplate -> draft round-trip. The role persists through the implemented role guest
        // step (RoleConfig), never the app-capability taxonomy, so the deploy contract stays honest.
        var draft = NamedDraft();
        var memberIndex = FirstNonDomainControllerIndex(draft);

        var authored = TemplatesBuilderRoleAuthoring.SetAdditionalRole(
            draft, memberIndex, TemplatesBuilderRoleProjectionCatalog.DhcpServerRoleKey, enabled: true).Draft;

        var build = TemplatesBuilderDraftMapper.BuildDocument(authored, "template-roles", 1, "1.0.0", sourceFilePath: null);
        Assert.NotNull(build.Document);

        var vmName = authored.Vms[memberIndex].Name;
        var templateVm = build.Document!.Template.VmTemplates.Single(vm => vm.Name == vmName);
        Assert.True(templateVm.RoleConfig?.Enabled);
        Assert.Contains(TemplatesBuilderRoleProjectionCatalog.DhcpServerRoleKey, templateVm.RoleConfig!.Roles!);

        var mappedBack = TemplatesBuilderDraftMapper.FromTemplate(build.Document.Template);
        var mappedVm = mappedBack.Vms.Single(vm => vm.Name == vmName);
        Assert.Contains(TemplatesBuilderRoleProjectionCatalog.DhcpServerRoleKey, mappedVm.AdditionalRoles ?? []);
    }

    [Fact]
    public void BuilderDraftMapping_PreservesUnknownAuthoredRoleKeysWithoutDropping()
    {
        // A template authored by a newer builder (or hand-edited) may carry role keys this build does not
        // recognize. The mapper must round-trip them untouched so reopening never silently drops a role.
        var draft = NamedDraft();
        var memberIndex = FirstNonDomainControllerIndex(draft);
        var vms = draft.Vms.ToList();
        vms[memberIndex] = vms[memberIndex] with { AdditionalRoles = new[] { "future-mystery-role" } };
        var seeded = draft with { Vms = vms };
        var vmName = seeded.Vms[memberIndex].Name;

        var build = TemplatesBuilderDraftMapper.BuildDocument(seeded, "template-unknown-roles", 1, "1.0.0", sourceFilePath: null);
        Assert.NotNull(build.Document);

        var mappedBack = TemplatesBuilderDraftMapper.FromTemplate(build.Document!.Template);
        var mappedVm = mappedBack.Vms.Single(vm => vm.Name == vmName);
        Assert.Contains("future-mystery-role", mappedVm.AdditionalRoles ?? []);
    }

    [Fact]
    public async Task BuilderShowDocument_LoadsMappedVmsIntoDraftAtViewModelLevel()
    {
        var draft = NamedDraft();
        var build = TemplatesBuilderDraftMapper.BuildDocument(draft, "template-show", 1, "1.0.0", sourceFilePath: null);
        Assert.NotNull(build.Document);

        var viewModel = CreateViewModel();
        await viewModel.ShowDocumentAsync(build.Document!);

        var loaded = viewModel.CaptureDraft();
        Assert.True(viewModel.HasActiveDraft);
        Assert.Equal(draft.Vms.Select(vm => vm.Name), loaded.Vms.Select(vm => vm.Name));
        Assert.Equal(draft.TemplateName, loaded.TemplateName);
    }

    [Fact]
    public void BuilderLiveRefresh_FieldEditRefreshesResourceListsWithoutRebuildingFocusedPanel()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());

        // Enter the Networks step so the network detail panel and its per-field view models are live.
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsNetworksVisible);
        Assert.NotEmpty(viewModel.NetworkDetailRows);

        var focusedPanelBefore = viewModel.NetworkDetailRows;
        var nameField = viewModel.NetworkDetailRows[0].Left;
        var resourceListBefore = viewModel.NetworkRows;

        // Simulate a keystroke: the two-way bound field updates its source.
        nameField.Value = "Renamed Segment";

        // Live refresh: the resource list and the working draft reflect the edit immediately.
        Assert.False(ReferenceEquals(resourceListBefore, viewModel.NetworkRows));
        Assert.Equal("Renamed Segment", viewModel.CaptureDraft().LabNetworks[0].Name);

        // Caret preservation: the focused detail panel is NOT rebuilt - same collection and same field
        // view model instance survive the edit, so the caret stays put.
        Assert.True(ReferenceEquals(focusedPanelBefore, viewModel.NetworkDetailRows));
        Assert.True(ReferenceEquals(nameField, viewModel.NetworkDetailRows[0].Left));
    }

    [Fact]
    public void BuilderDetailTitles_PopulateWhenDrillingIntoResourceAndVmDetails()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());

        // Networks step: the detail panel exposes its constant category title alongside the field rows.
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsNetworksVisible);
        Assert.True(viewModel.HasNetworkDetail);
        Assert.Equal("Selected Network Detail", viewModel.NetworkDetailTitle);

        // Drill through the leading steps into the first VM's detail route (6 Next steps total).
        viewModel.NextStepCommand.Execute(null); // ForestsDomains
        viewModel.NextStepCommand.Execute(null); // Credentials
        viewModel.NextStepCommand.Execute(null); // VM overview
        viewModel.NextStepCommand.Execute(null); // first VM detail
        Assert.True(viewModel.IsVmDetailVisible);

        // The VM detail primary title carries the projection's per-VM title (regression: it was dropped).
        Assert.True(viewModel.HasVmDetailTitle);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.VmDetailTitle));
    }

    [Fact]
    public void AddForest_BornWithRootDomainAndDomainControllerSelectingForest_StaysValid()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        var before = viewModel.CaptureDraft();

        viewModel.AddForestCommand.Execute(null);

        var after = viewModel.CaptureDraft();
        Assert.Equal(before.Forests.Count + 1, after.Forests.Count);
        Assert.Equal(before.Domains.Count + 1, after.Domains.Count);

        // The new forest is born with its root domain's first domain controller, so the draft stays valid.
        var newForest = after.Forests[^1];
        var rootDomain = after.Domains.Single(domain => domain.DomainId == newForest.RootDomainId);
        Assert.Contains(after.Vms, vm => vm.IsActiveDirectoryDomainController && vm.DomainId == rootDomain.DomainId);
        Assert.False(viewModel.HasValidationBlockers, string.Join(" | ", viewModel.ValidationState.Blockers.Select(issue => issue.Message)));

        // Selection follows the new forest so its detail panel is what the user sees.
        Assert.Equal("Selected Forest Detail", viewModel.ForestDomainDetailTitle);
    }

    [Fact]
    public void AddTree_AddsTreeDomainWithBornDomainControllerToActiveForest()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        var before = viewModel.CaptureDraft();
        var forestId = before.Forests[0].ForestId;

        viewModel.AddTreeCommand.Execute(null);

        var after = viewModel.CaptureDraft();
        var tree = after.Domains[^1];
        Assert.Equal(nameof(V2DomainRelationKind.Tree), tree.RelationKind);
        Assert.Equal(forestId, tree.ForestId);
        Assert.Equal(string.Empty, tree.ParentDomainId);
        Assert.Contains(after.Vms, vm => vm.IsActiveDirectoryDomainController && vm.DomainId == tree.DomainId);
        Assert.False(viewModel.HasValidationBlockers, string.Join(" | ", viewModel.ValidationState.Blockers.Select(issue => issue.Message)));
    }

    [Fact]
    public void CanvasAddChild_AddsChildDomainUnderHoveredDomainAndSelectsIt()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        viewModel.NextStepCommand.Execute(null); // force a render so the canvas is populated
        var before = viewModel.CaptureDraft();

        var domainNode = viewModel.TopologyCanvas!.Nodes.First(node => !node.IsForest);
        Assert.NotNull(domainNode.AddChildCommand);
        domainNode.AddChildCommand!.Execute(null);

        var after = viewModel.CaptureDraft();
        Assert.Equal(before.Domains.Count + 1, after.Domains.Count);
        var child = after.Domains[^1];
        Assert.Equal(nameof(V2DomainRelationKind.Child), child.RelationKind);
        Assert.Equal(before.Domains[0].DomainId, child.ParentDomainId);
        Assert.Contains(after.Vms, vm => vm.IsActiveDirectoryDomainController && vm.DomainId == child.DomainId);
        Assert.False(viewModel.HasValidationBlockers, string.Join(" | ", viewModel.ValidationState.Blockers.Select(issue => issue.Message)));

        // The freshly created child domain is selected.
        Assert.Equal("Selected Domain Detail", viewModel.ForestDomainDetailTitle);
    }

    [Fact]
    public void CanvasDelete_RemovesForestAndItsDomainsAndVms()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        viewModel.NextStepCommand.Execute(null);

        // A lab must keep at least one directory, so a forest is only deletable when another forest exists.
        // Add a second forest, then delete the first and confirm its domains/VMs are gone while the other stays.
        viewModel.AddForestCommand.Execute(null);
        var before = viewModel.CaptureDraft();
        Assert.Equal(2, before.Forests.Count);
        var firstForestId = before.Forests[0].ForestId;
        var survivingForestId = before.Forests[1].ForestId;
        var removedDomainIds = before.Domains
            .Where(domain => string.Equals(domain.ForestId, firstForestId, System.StringComparison.OrdinalIgnoreCase))
            .Select(domain => domain.DomainId)
            .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

        var forestFrame = viewModel.TopologyCanvas!.Frames.First(frame => frame.FrameId == $"forest:{firstForestId}");
        Assert.NotNull(forestFrame.DeleteCommand);
        forestFrame.DeleteCommand!.Execute(null);

        var after = viewModel.CaptureDraft();
        Assert.Single(after.Forests);
        Assert.Contains(after.Forests, forest => forest.ForestId == survivingForestId);
        Assert.DoesNotContain(after.Forests, forest => forest.ForestId == firstForestId);
        Assert.DoesNotContain(after.Domains, domain => domain.ForestId == firstForestId);
        // Deleting the forest removes its domain-joined VMs (the whole forest is gone).
        Assert.DoesNotContain(after.Vms, vm => vm.DomainId is not null && removedDomainIds.Contains(vm.DomainId));
    }

    [Fact]
    public void CanvasDelete_RemovingTheSelectedDomain_KeepsDetailPanelCoherent()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        viewModel.NextStepCommand.Execute(null);

        // Add a child domain (it becomes the selection), then delete that same selected domain. The selection
        // index now points past the shrunk domain list, so the detail re-render must stay in bounds and clear
        // or reselect coherently instead of throwing IndexOutOfRangeException.
        var rootNode = viewModel.TopologyCanvas!.Nodes.First(node => !node.IsForest);
        rootNode.AddChildCommand!.Execute(null);
        var child = viewModel.CaptureDraft().Domains[^1];
        var childNode = viewModel.TopologyCanvas!.Nodes.Single(node => node.NodeId == $"domain:{child.DomainId}");
        Assert.NotNull(childNode.DeleteCommand);

        childNode.DeleteCommand!.Execute(null);

        var after = viewModel.CaptureDraft();
        Assert.Single(after.Domains);
        Assert.DoesNotContain(after.Domains, domain => domain.DomainId == child.DomainId);
        Assert.False(viewModel.HasValidationBlockers, string.Join(" | ", viewModel.ValidationState.Blockers.Select(issue => issue.Message)));
    }

    [Fact]
    public void RootDomainRelation_IsRenderedReadOnlyAndLockedToRoot()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        viewModel.NextStepCommand.Execute(null);

        var rootNode = viewModel.TopologyCanvas!.Nodes.First(node => !node.IsForest);
        rootNode.SelectCommand!.Execute(null);

        var relation = FindForestDomainField(viewModel, "Relation");
        Assert.NotNull(relation);
        Assert.True(relation!.IsReadOnly);
        Assert.Equal(nameof(V2DomainRelationKind.Root), relation.Value);
    }

    [Fact]
    public void NonRootRelationEdit_ChildToTree_ClearsParentThroughEngine()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        viewModel.NextStepCommand.Execute(null);

        // Create and select a child domain, then flip its relation to Tree via the detail combo.
        var domainNode = viewModel.TopologyCanvas!.Nodes.First(node => !node.IsForest);
        domainNode.AddChildCommand!.Execute(null);
        var childBefore = viewModel.CaptureDraft().Domains[^1];
        Assert.Equal(nameof(V2DomainRelationKind.Child), childBefore.RelationKind);
        Assert.False(string.IsNullOrEmpty(childBefore.ParentDomainId));

        var relation = FindForestDomainField(viewModel, "Relation");
        Assert.NotNull(relation);
        Assert.False(relation!.IsReadOnly);
        relation.Value = nameof(V2DomainRelationKind.Tree);

        var childAfter = viewModel.CaptureDraft().Domains[^1];
        Assert.Equal(nameof(V2DomainRelationKind.Tree), childAfter.RelationKind);
        Assert.Equal(string.Empty, childAfter.ParentDomainId);
        Assert.False(viewModel.HasValidationBlockers, string.Join(" | ", viewModel.ValidationState.Blockers.Select(issue => issue.Message)));
    }

    [Fact]
    public void ForestDetail_IsReadOnlyAndNameFollowsRootDomainDnsName()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        viewModel.NextStepCommand.Execute(null);

        var forestFrame = viewModel.TopologyCanvas!.Frames.First();
        forestFrame.SelectCommand!.Execute(null);

        var rootDnsName = viewModel.CaptureDraft().Domains[0].DnsName;
        var forestName = FindForestDomainField(viewModel, "Forest Name");
        Assert.NotNull(forestName);
        Assert.True(forestName!.IsReadOnly);
        Assert.Equal(rootDnsName, forestName.Value);
    }

    [Fact]
    public void DomainSubnetEditor_ShowsDomainSubnetAndHidesForForest()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        GoToForestsDomains(viewModel);

        var draft = viewModel.CaptureDraft();
        var domain = draft.Domains[0];
        var domainNode = viewModel.TopologyCanvas!.Nodes.First(node => !node.IsForest);
        domainNode.SelectCommand!.Execute(null);

        var network = viewModel.CaptureDraft().LabNetworks.Single(candidate => candidate.DomainId == domain.DomainId);
        Assert.True(viewModel.ShowDomainSubnetEditor);
        Assert.Equal(network.Subnet, viewModel.DomainSubnetValue);

        var forestFrame = viewModel.TopologyCanvas!.Frames.First();
        forestFrame.SelectCommand!.Execute(null);

        Assert.False(viewModel.ShowDomainSubnetEditor);
        Assert.False(viewModel.HasDomainSubnetValidationMessage);
        Assert.Equal(string.Empty, viewModel.DomainSubnetValidationMessage);
    }

    [Fact]
    public void DomainSubnetCommit_ValidValueUpdatesNetworkPreservesDomainIdAndReflowsDomainControllerIp()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        GoToForestsDomains(viewModel);

        var domain = viewModel.CaptureDraft().Domains[0];
        var domainNode = viewModel.TopologyCanvas!.Nodes.First(node => !node.IsForest);
        domainNode.SelectCommand!.Execute(null);

        viewModel.CommitSelectedDomainSubnet("10.9.9.0/24");

        var draftAfter = viewModel.CaptureDraft();
        var network = draftAfter.LabNetworks.Single(candidate => candidate.DomainId == domain.DomainId);
        Assert.Equal("10.9.9.0/24", network.Subnet);
        Assert.Equal(domain.DomainId, network.DomainId);
        Assert.False(viewModel.HasDomainSubnetValidationMessage);
        Assert.Equal(string.Empty, viewModel.DomainSubnetValidationMessage);

        var dc = draftAfter.Vms.Single(vm => vm.IsActiveDirectoryDomainController && vm.DomainId == domain.DomainId);
        Assert.NotEmpty(dc.Nics);
        Assert.True(BuilderLabSubnet.TryParseCidr("10.9.9.0/24", out var subnet));
        Assert.True(BuilderLabSubnet.TryParseAddress(dc.Nics[0].IpAddress, out var dcAddress));
        Assert.True(subnet.Contains(dcAddress), dc.Nics[0].IpAddress);
    }

    [Fact]
    public void DomainSubnetCommit_InvalidValueKeepsDraftSubnetShowsRawTextAndDoesNotReconcileVmIps()
    {
        var viewModel = CreateViewModel();
        var twoDomainDraft = TemplatesBuilderNetworkReconciler.Reconcile(
            TemplatesBuilderTopologyAuthoring.AddForest(NamedDraft()).Draft);
        viewModel.LoadNewDraft(twoDomainDraft);
        GoToForestsDomains(viewModel);

        var domain = viewModel.CaptureDraft().Domains[1];
        var domainNode = viewModel.TopologyCanvas!.Nodes.Single(node => node.NodeId == $"domain:{domain.DomainId}");
        domainNode.SelectCommand!.Execute(null);
        var draftBefore = viewModel.CaptureDraft();
        var subnetBefore = draftBefore.LabNetworks.Single(candidate => candidate.DomainId == domain.DomainId).Subnet;
        var ipAddressesBefore = draftBefore.Vms.ToDictionary(
            vm => vm.VmId,
            vm => string.Join("|", vm.Nics.Select(nic => nic.IpAddress)));

        viewModel.CommitSelectedDomainSubnet("not-a-cidr");

        var draftAfter = viewModel.CaptureDraft();
        var network = draftAfter.LabNetworks.Single(candidate => candidate.DomainId == domain.DomainId);
        // The garbage value is NOT written into the draft: the network keeps its last valid subnet so the canvas
        // label never blanks and Save can never persist a non-CIDR subnet.
        Assert.Equal(subnetBefore, network.Subnet);
        Assert.NotEqual("not-a-cidr", network.Subnet);
        // The raw text stays in the editor field with a soft-red message so the user can fix it in place.
        Assert.Equal("not-a-cidr", viewModel.DomainSubnetValue);
        Assert.True(viewModel.HasDomainSubnetValidationMessage);
        Assert.True(viewModel.ShowDomainSubnetErrorOutline);
        Assert.Contains("valid CIDR", viewModel.DomainSubnetValidationMessage);
        foreach (var vm in draftAfter.Vms)
        {
            Assert.True(ipAddressesBefore.TryGetValue(vm.VmId, out var beforeIps));
            Assert.Equal(beforeIps, string.Join("|", vm.Nics.Select(nic => nic.IpAddress)));
        }
    }

    [Fact]
    public void DomainSubnetCommit_ClearingSubnetKeepsDraftSubnetAndCanvasLabel()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        GoToForestsDomains(viewModel);

        var domain = viewModel.CaptureDraft().Domains[0];
        var domainNode = viewModel.TopologyCanvas!.Nodes.First(node => !node.IsForest);
        domainNode.SelectCommand!.Execute(null);

        var subnetBefore = viewModel.CaptureDraft().LabNetworks.Single(n => n.DomainId == domain.DomainId).Subnet;
        Assert.False(string.IsNullOrWhiteSpace(subnetBefore));

        // Reproduces the reported bug: clearing the field must not blank the domain's on-canvas subnet label.
        viewModel.CommitSelectedDomainSubnet(string.Empty);

        var draftAfter = viewModel.CaptureDraft();
        var network = draftAfter.LabNetworks.Single(n => n.DomainId == domain.DomainId);
        Assert.Equal(subnetBefore, network.Subnet);
        Assert.Equal(string.Empty, viewModel.DomainSubnetValue);
        Assert.True(viewModel.HasDomainSubnetValidationMessage);

        // The canvas node still carries the CIDR in its subtext, so the label stays visible.
        var node = viewModel.TopologyCanvas!.Nodes.Single(n => n.NodeId == $"domain:{domain.DomainId}");
        Assert.Contains(subnetBefore, node.Subtext);
    }

    [Fact]
    public void DomainSubnetCommit_CollidingValueNamesTheOtherDomainAndStillPersists()
    {
        var viewModel = CreateViewModel();
        var twoDomainDraft = TemplatesBuilderNetworkReconciler.Reconcile(
            TemplatesBuilderTopologyAuthoring.AddForest(NamedDraft()).Draft);
        viewModel.LoadNewDraft(twoDomainDraft);
        GoToForestsDomains(viewModel);

        var draft = viewModel.CaptureDraft();
        var domainA = draft.Domains[0];
        var domainB = draft.Domains[1];
        var subnetA = draft.LabNetworks.Single(network => network.DomainId == domainA.DomainId).Subnet;
        var domainBNode = viewModel.TopologyCanvas!.Nodes.Single(node => node.NodeId == $"domain:{domainB.DomainId}");
        domainBNode.SelectCommand!.Execute(null);

        viewModel.CommitSelectedDomainSubnet(subnetA);

        var draftAfter = viewModel.CaptureDraft();
        var networkB = draftAfter.LabNetworks.Single(network => network.DomainId == domainB.DomainId);
        Assert.Equal(subnetA, networkB.Subnet);
        Assert.True(viewModel.HasDomainSubnetValidationMessage);
        Assert.Contains(domainA.DnsName, viewModel.DomainSubnetValidationMessage);
    }

    [Fact]
    public void AddStandaloneMachine_CreatesStandaloneVmAndZoomsIntoLevel2()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        GoToForestsDomains(viewModel);
        var before = viewModel.CaptureDraft();

        viewModel.AddStandaloneMachineCommand.Execute(null);

        var after = viewModel.CaptureDraft();
        // Adding the first standalone machine to a single-domain lab keeps ONE switch (the standalone rides the
        // domain's switch), so no router is materialized: exactly one VM is added, the standalone itself.
        Assert.Equal(before.Vms.Count + 1, after.Vms.Count);
        var added = after.Vms.Single(vm => V2MembershipModeCatalog.IsStandalone(vm.MembershipMode) && !vm.IsRouter);
        Assert.True(V2MembershipModeCatalog.IsStandalone(added.MembershipMode));
        Assert.Equal(string.Empty, added.DomainId);
        Assert.False(added.IsActiveDirectoryDomainController);

        // Adding a standalone machine zooms straight into the Standalone container's Level 2 list, which holds
        // just the one standalone machine (no auto-router in a single-domain lab).
        Assert.True(viewModel.IsMachineLevelVisible);
        Assert.Equal("Standalone", viewModel.MachineLevelTitle);
        Assert.True(viewModel.HasMachineCards);
        Assert.Single(viewModel.MachineCards);
        Assert.DoesNotContain(after.Vms, vm => vm.IsRouter);
        Assert.False(viewModel.HasValidationBlockers, string.Join(" | ", viewModel.ValidationState.Blockers.Select(issue => issue.Message)));
    }

    [Fact]
    public void ManageMachines_ZoomIntoDomain_ListsDomainControllerFirstThenMember()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        GoToForestsDomains(viewModel);

        var domainNode = viewModel.TopologyCanvas!.Nodes.First(node => !node.IsForest);
        Assert.NotNull(domainNode.ManageMachinesCommand);
        domainNode.ManageMachinesCommand!.Execute(null);

        Assert.True(viewModel.IsMachineLevelVisible);
        Assert.Equal("contoso.com", viewModel.MachineLevelTitle);
        Assert.Equal(2, viewModel.MachineCards.Count);
        // The directory anchor (domain controller) sorts first.
        Assert.True(viewModel.MachineCards[0].IsDomainController);
        Assert.False(viewModel.MachineCards[1].IsDomainController);
    }

    [Fact]
    public void AddComputer_InDomain_AddsMemberServerAndSelectsItStayingValid()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        GoToForestsDomains(viewModel);

        var domainNode = viewModel.TopologyCanvas!.Nodes.First(node => !node.IsForest);
        domainNode.ManageMachinesCommand!.Execute(null);
        var before = viewModel.CaptureDraft();

        viewModel.AddComputerCommand.Execute(null);

        var after = viewModel.CaptureDraft();
        Assert.Equal(before.Vms.Count + 1, after.Vms.Count);
        var added = after.Vms[^1];
        Assert.False(added.IsActiveDirectoryDomainController);
        Assert.Equal("domain-contoso", added.DomainId);
        Assert.True(V2MembershipModeCatalog.IsDomainMember(added.MembershipMode));

        // The Level 2 list refreshes to include the new machine, selected.
        Assert.Equal(3, viewModel.MachineCards.Count);
        Assert.Contains(viewModel.MachineCards, card => card.VmIndex == after.Vms.Count - 1 && card.IsSelected);
        Assert.False(viewModel.HasValidationBlockers, string.Join(" | ", viewModel.ValidationState.Blockers.Select(issue => issue.Message)));
    }

    [Fact]
    public void DeleteMachine_RemovesMemberButOnlyDomainControllerOffersNoDelete()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        GoToForestsDomains(viewModel);

        var domainNode = viewModel.TopologyCanvas!.Nodes.First(node => !node.IsForest);
        domainNode.ManageMachinesCommand!.Execute(null);

        // The lone domain controller cannot be deleted, so its card exposes no delete affordance.
        var dcCard = viewModel.MachineCards.Single(card => card.IsDomainController);
        Assert.False(dcCard.CanDelete);
        Assert.Null(dcCard.DeleteCommand);

        // A member server can be removed; the container drops to just the domain controller.
        var memberCard = viewModel.MachineCards.Single(card => !card.IsDomainController);
        Assert.True(memberCard.CanDelete);
        memberCard.DeleteCommand!.Execute(null);

        var after = viewModel.CaptureDraft();
        Assert.DoesNotContain(after.Vms, vm => vm.VmId == "vm-member01");
        Assert.Contains(after.Vms, vm => vm.IsActiveDirectoryDomainController && vm.DomainId == "domain-contoso");
        Assert.Single(viewModel.MachineCards);
        Assert.True(viewModel.MachineCards[0].IsDomainController);
    }

    [Fact]
    public void MachineInspectorBasics_CommitCommandsUpdateSelectedMemberFields()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        ZoomIntoDomainMachineLevel(viewModel);

        var memberCard = viewModel.MachineCards.Single(card => !card.IsDomainController);
        memberCard.SelectCommand!.Execute(null);
        var memberVmId = viewModel.CaptureDraft().Vms[memberCard.VmIndex].VmId;
        var inspector = viewModel.SelectedMachineInspector;
        Assert.NotNull(inspector);

        inspector!.CommitMachineNameCommand!.Execute("Member-File-01");
        inspector = viewModel.SelectedMachineInspector;
        inspector!.CommitMachineCpuCountCommand!.Execute("6");
        inspector = viewModel.SelectedMachineInspector;
        inspector!.CommitMachineMemoryMbCommand!.Execute("12288");
        inspector = viewModel.SelectedMachineInspector;
        inspector!.CommitMachineBaseDiskCommand!.Execute("disk-dc");

        var draftAfter = viewModel.CaptureDraft();
        var updated = draftAfter.Vms.Single(vm => vm.VmId == memberVmId);
        Assert.Equal("Member-File-01", updated.Name);
        Assert.Equal("6", updated.CpuCount);
        Assert.Equal("12288", updated.MemoryMb);
        Assert.Equal("disk-dc", updated.VhdxId);
    }

    [Fact]
    public void MachineInspectorHostIp_CommitOctetsUpdatesSelectedMachinePrimaryNicAddress()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        ZoomIntoDomainMachineLevel(viewModel);

        var memberCard = viewModel.MachineCards.Single(card => !card.IsDomainController);
        memberCard.SelectCommand!.Execute(null);
        var selectedCard = viewModel.MachineCards.Single(card => card.IsSelected);
        Assert.Equal(memberCard.VmIndex, selectedCard.VmIndex);
        var draftBefore = viewModel.CaptureDraft();
        var memberVmId = draftBefore.Vms[memberCard.VmIndex].VmId;
        var memberIndex = draftBefore.Vms.ToList().FindIndex(vm => vm.VmId == memberVmId);
        Assert.True(memberIndex >= 0);
        var inspector = viewModel.SelectedMachineInspector;
        Assert.NotNull(inspector);
        Assert.Equal(memberCard.Label, inspector!.MachineTitle);
        Assert.True(inspector!.IsHostAddressEditable);

        var editableOctets = FindHostOctetsForStatus(draftBefore, memberIndex, MachineHostAddressStatus.Ok, requireDifferentFromCurrent: true);
        inspector.CommitMachineHostOctetsCommand!.Execute(editableOctets);

        var expectedAddress = $"{inspector.HostAddressFixedOctetPrefix}{string.Join(".", editableOctets)}";
        var draftAfter = viewModel.CaptureDraft();
        var updated = draftAfter.Vms.Single(vm => vm.VmId == memberVmId);
        Assert.Contains(updated.Nics, nic => nic.IpAddress == expectedAddress);

        var updatedIndex = draftAfter.Vms.ToList().FindIndex(vm => vm.VmId == memberVmId);
        Assert.True(updatedIndex >= 0);
        var projectedAfter = TemplatesBuilderMachineBasicsAuthoring.ProjectHostAddress(draftAfter, updatedIndex);
        Assert.Equal(editableOctets, projectedAfter.Octets);
    }

    [Fact]
    public void MachineGridItems_MirrorsMachineCardsThenAppendsTheAddPlaceholder()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        ZoomIntoDomainMachineLevel(viewModel);

        // The grid source is the machine cards in order followed by exactly one trailing add-computer
        // placeholder tile, so the "+ Add computer" affordance wraps inline with the cards. MachineCards
        // itself stays machine-only (the count/Single interaction tests rely on that).
        Assert.Equal(viewModel.MachineCards.Count + 1, viewModel.MachineGridItems.Count);
        for (var i = 0; i < viewModel.MachineCards.Count; i++)
        {
            Assert.Same(viewModel.MachineCards[i], viewModel.MachineGridItems[i]);
        }

        var placeholder = Assert.IsType<BuilderAddMachinePlaceholder>(viewModel.MachineGridItems[^1]);
        Assert.True(placeholder.IsEnabled);
        Assert.Single(viewModel.MachineGridItems.OfType<BuilderAddMachinePlaceholder>());
    }

    [Fact]
    public void MachineGridItems_IsClearedWhenLeavingTheMachineLevel()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        ZoomIntoDomainMachineLevel(viewModel);
        Assert.NotEmpty(viewModel.MachineGridItems);

        viewModel.BackToTopologyCommand.Execute(null);

        Assert.False(viewModel.IsMachineLevelVisible);
        Assert.Empty(viewModel.MachineGridItems);
    }

    [Fact]
    public void MachineInspectorHostIp_ProjectionExposesMemberEditorAndRouterHiddenRow()
    {
        var viewModel = CreateViewModel();
        // A router only exists once there are two switches, so seed a second forest (a second domain, hence a
        // second switch) and reconcile before loading, giving the draft both a domain member and a router.
        viewModel.LoadNewDraft(TemplatesBuilderNetworkReconciler.Reconcile(
            TemplatesBuilderTopologyAuthoring.AddForest(NamedDraft()).Draft));
        ZoomIntoDomainMachineLevel(viewModel);

        var draft = viewModel.CaptureDraft();
        var memberCard = viewModel.MachineCards.Single(card => !card.IsDomainController);
        var memberVm = draft.Vms[memberCard.VmIndex];
        var memberHostView = TemplatesBuilderMachineBasicsAuthoring.ProjectHostAddress(draft, memberCard.VmIndex);
        Assert.True(memberHostView.IsEditable);

        var memberIpParts = memberVm.Nics[0].IpAddress.Split('.');
        Assert.Equal(4, memberIpParts.Length);
        var fixedOctetCount = 4 - memberHostView.EditableOctetCount;
        var expectedPrefix = $"{string.Join(".", memberIpParts.Take(fixedOctetCount))}.";
        Assert.Equal(expectedPrefix, memberHostView.FixedOctetPrefix);

        var routerIndex = draft.Vms.ToList().FindIndex(vm => vm.IsRouter);
        Assert.True(routerIndex >= 0);
        var routerHostView = TemplatesBuilderMachineBasicsAuthoring.ProjectHostAddress(draft, routerIndex);
        Assert.False(routerHostView.IsEditable);
    }

    [Fact]
    public void MachineInspectorHostIp_ReservedRouterCommitSurfacesReservedRouterStatusInInspector()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        ZoomIntoDomainMachineLevel(viewModel);

        var draft = viewModel.CaptureDraft();
        var memberCard = viewModel.MachineCards.Single(card => !card.IsDomainController);
        var memberVmId = draft.Vms[memberCard.VmIndex].VmId;
        var memberIndex = draft.Vms.ToList().FindIndex(vm => vm.VmId == memberVmId);
        Assert.True(memberIndex >= 0);
        var memberHostView = TemplatesBuilderMachineBasicsAuthoring.ProjectHostAddress(draft, memberCard.VmIndex);
        Assert.True(memberHostView.IsEditable);
        var reservedRouterOctets = FindHostOctetsForStatus(draft, memberIndex, MachineHostAddressStatus.ReservedRouter, requireDifferentFromCurrent: false);

        memberCard.SelectCommand!.Execute(null);
        var selectedCard = viewModel.MachineCards.Single(card => card.IsSelected);
        Assert.Equal(memberCard.VmIndex, selectedCard.VmIndex);
        var inspector = viewModel.SelectedMachineInspector;
        Assert.NotNull(inspector);
        Assert.Equal(memberCard.Label, inspector!.MachineTitle);
        inspector!.CommitMachineHostOctetsCommand!.Execute(reservedRouterOctets);

        Assert.Equal(nameof(MachineHostAddressStatus.ReservedRouter), viewModel.SelectedMachineInspector!.HostAddressStatusKey);
    }

    [Fact]
    public void BackToTopology_LeavesLevel2AndShowsStandaloneContainerAtLevel1()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadNewDraft(NamedDraft());
        GoToForestsDomains(viewModel);

        viewModel.AddStandaloneMachineCommand.Execute(null);
        Assert.True(viewModel.IsMachineLevelVisible);

        viewModel.BackToTopologyCommand.Execute(null);
        Assert.False(viewModel.IsMachineLevelVisible);

        // The Standalone container is now present as a Level 1 node (it exists only once a standalone machine does).
        Assert.Contains(viewModel.TopologyCanvas!.Nodes, node => node.IsStandalone);
    }

    private static void ZoomIntoDomainMachineLevel(TemplatesBuilderViewModel viewModel)
    {
        GoToForestsDomains(viewModel);
        var domainNode = viewModel.TopologyCanvas!.Nodes.First(node => !node.IsForest);
        domainNode.ManageMachinesCommand!.Execute(null);
        Assert.True(viewModel.IsMachineLevelVisible);
    }

    private static IReadOnlyList<int> FindHostOctetsForStatus(
        TemplatesBuilderDraftSnapshot draft,
        int vmIndex,
        MachineHostAddressStatus targetStatus,
        bool requireDifferentFromCurrent)
    {
        var projected = TemplatesBuilderMachineBasicsAuthoring.ProjectHostAddress(draft, vmIndex);
        Assert.True(projected.IsEditable);

        if (projected.EditableOctetCount == 1)
        {
            for (var octet = 0; octet <= 255; octet++)
            {
                var candidate = new[] { octet };
                if (requireDifferentFromCurrent && projected.Octets.SequenceEqual(candidate))
                {
                    continue;
                }

                var authored = TemplatesBuilderMachineBasicsAuthoring.SetMachineHostOctets(draft, vmIndex, candidate).Draft;
                if (TemplatesBuilderMachineBasicsAuthoring.ProjectHostAddress(authored, vmIndex).Status == targetStatus)
                {
                    return candidate;
                }
            }
        }

        if (projected.EditableOctetCount == 2)
        {
            for (var first = 0; first <= 255; first++)
            {
                for (var second = 0; second <= 255; second++)
                {
                    var candidate = new[] { first, second };
                    if (requireDifferentFromCurrent && projected.Octets.SequenceEqual(candidate))
                    {
                        continue;
                    }

                    var authored = TemplatesBuilderMachineBasicsAuthoring.SetMachineHostOctets(draft, vmIndex, candidate).Draft;
                    if (TemplatesBuilderMachineBasicsAuthoring.ProjectHostAddress(authored, vmIndex).Status == targetStatus)
                    {
                        return candidate;
                    }
                }
            }
        }

        throw new Xunit.Sdk.XunitException($"No editable octets produced status '{targetStatus}' for vm index {vmIndex}.");
    }

    private static void GoToForestsDomains(TemplatesBuilderViewModel viewModel)
    {
        // LoadNewDraft does not render; the first Next renders and advances to Networks, the second to Forests & Domains.
        viewModel.NextStepCommand.Execute(null);
        viewModel.NextStepCommand.Execute(null);
        Assert.True(viewModel.IsForestsDomainsVisible);
    }

    private static BuilderFieldViewModel? FindForestDomainField(TemplatesBuilderViewModel viewModel, string header)
        => viewModel.ForestDomainDetailRows
            .SelectMany(row => row.Right is null ? new[] { row.Left } : new[] { row.Left, row.Right })
            .FirstOrDefault(field => field is not null && field.Header == header);

    private static TemplatesBuilderViewModel CreateViewModel()
    {
        var viewModel = new TemplatesBuilderViewModel(new StubTemplatesCapabilityService());
        viewModel.Attach(new StubBuilderHost());
        return viewModel;
    }

    private static TemplatesBuilderDraftSnapshot NamedDraft()
    {
        var referenceData = new TemplatesBuilderReferenceData(
            ["vSwitch-Core"],
            [
                new TemplateVhdxCatalogOption("disk-dc", @"C:\base\disk-dc.vhdx", "Windows Server", "2022", 2, "sig-dc"),
                new TemplateVhdxCatalogOption("disk-member", @"C:\base\disk-member.vhdx", "Windows Server", "2022", 2, "sig-member")
            ]);

        return TemplatesBuilderDraftMapper.CreateSuggestedDraft(referenceData) with { TemplateName = "Interaction Lab" };
    }

    private static int FirstNonDomainControllerIndex(TemplatesBuilderDraftSnapshot draft)
    {
        for (var i = 0; i < draft.Vms.Count; i++)
        {
            if (!draft.Vms[i].IsActiveDirectoryDomainController)
            {
                return i;
            }
        }

        return draft.Vms.Count - 1;
    }

    private sealed class StubTemplatesCapabilityService : ITemplatesCapabilityService
    {
        public Task<TemplateLibraryLoadResult> LoadLibraryAsync(string? searchText = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateLibraryLoadResult());

        public Task<TemplateEditorDocument> CreateDraftAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateEditorDocument());

        public Task<TemplateEditorDocument> LoadForEditorAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateEditorDocument());

        public Task<TemplatesVhdxCatalogLoadResult> LoadVhdxCatalogOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplatesVhdxCatalogLoadResult());

        public Task<TemplateOperationResult> SaveAsync(
            TemplateEditorDocument document,
            string? targetFilePath = null,
            bool saveAs = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateOperationResult { Success = true, UserMessage = "Saved." });

        public Task<TemplateValidationSummaryResult> ValidateAsync(TemplateEditorDocument document, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateValidationSummaryResult { IsValid = true });

        public Task<TemplateOperationResult> DeleteAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateOperationResult());

        public Task<TemplateOperationResult> ImportAsync(string sourceFilePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateOperationResult());

        public Task<TemplateOperationResult> ExportAsync(
            string sourceFilePath,
            string destinationFilePath,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplateOperationResult());
    }

    private sealed class StubBuilderHost : ITemplatesBuilderHost
    {
        public Task<TemplatesBuilderReferenceData> LoadBuilderReferenceDataAsync(bool forceRefresh)
            => Task.FromResult(new TemplatesBuilderReferenceData(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>()));

        public Task ReloadLibraryAsync(bool forceRefresh) => Task.CompletedTask;

        public Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName)
            => Task.FromResult<string?>(@"C:\templates\save-as.json");

        public void NavigateToBuilder()
        {
        }

        public void NavigateToLibrary()
        {
        }
    }
}
