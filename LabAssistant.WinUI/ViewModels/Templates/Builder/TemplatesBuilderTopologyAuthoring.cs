using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// The outcome of a topology authoring mutation: the new draft snapshot and the resource the caller should
/// select afterward (so the canvas and detail panel focus the thing the user just created or the sensible
/// survivor after a delete).
/// </summary>
internal readonly record struct TopologyAuthoringResult(
    TemplatesBuilderDraftSnapshot Draft,
    BuilderForestDomainResourceKind SelectedKind,
    int SelectedIndex);

/// <summary>
/// Pure, runtime-independent authoring rules for the directory-topology canvas (Phase 1).
///
/// Everything here operates on an immutable <see cref="TemplatesBuilderDraftSnapshot"/> and returns a new
/// snapshot, so the rules are unit-testable without a XAML host and never depend on Hyper-V. The invariants
/// implemented are the ones locked in <c>docs/03-architecture/winui-templates-builder-canvas.md</c>:
/// every forest has exactly one root domain; a non-root domain is only ever Tree or Child; the forest name
/// derives from its root domain; and every domain is born with a domain controller so the draft stays valid
/// the instant it is created.
/// </summary>
internal static class TemplatesBuilderTopologyAuthoring
{
    // Friendly default first-labels for newly authored domains, generalized from the forest cycle in the
    // canvas spec (contoso, fabrikam, then contoso3, contoso4, ...). Purely a naming convenience; the user
    // renames freely afterward. The sequence guarantees a valid, unique default so a new lab is immediately
    // nameable without typing.
    private static string FriendlyLabelAt(int ordinal)
        => ordinal switch
        {
            0 => "contoso",
            1 => "fabrikam",
            _ => $"contoso{ordinal + 1}"
        };

    /// <summary>
    /// Adds a new forest, born with its single root domain and that domain's first domain controller.
    /// Selects the new forest.
    /// </summary>
    public static TopologyAuthoringResult AddForest(TemplatesBuilderDraftSnapshot draft)
    {
        var takenFirstLabels = FirstLabelsInUse(draft);
        var token = NextFriendlyToken(takenFirstLabels);
        var dnsName = $"{token}.lab";

        var forestId = UniqueId($"forest-{token}", ForestIds(draft));
        var domainId = UniqueId($"domain-{token}", DomainIds(draft));

        var rootDomain = new TemplatesBuilderDomainDraft(
            domainId,
            dnsName,
            MakeNetBios(token),
            forestId,
            nameof(V2DomainRelationKind.Root),
            string.Empty);

        var draftWithForest = draft with
        {
            Forests = draft.Forests.Append(new TemplatesBuilderForestDraft(forestId, domainId)).ToList(),
            Domains = draft.Domains.Append(rootDomain).ToList(),
            IsSaveConfirmed = false
        };

        var withDc = BornWithDomainController(draftWithForest, domainId, dnsName);
        return new TopologyAuthoringResult(
            withDc,
            BuilderForestDomainResourceKind.Forest,
            withDc.Forests.Count - 1);
    }

    /// <summary>
    /// Adds a new tree domain (a second DNS namespace rooted directly under the forest, with no parent
    /// domain) to the given forest, born with its first domain controller. Selects the new domain.
    /// </summary>
    public static TopologyAuthoringResult AddTree(TemplatesBuilderDraftSnapshot draft, string forestId)
    {
        var forestIndex = IndexOfForest(draft, forestId);
        if (forestIndex < 0)
        {
            return Unchanged(draft);
        }

        var resolvedForestId = draft.Forests[forestIndex].ForestId;
        var token = NextFriendlyToken(FirstLabelsInUse(draft));
        var dnsName = $"{token}.lab";
        var domainId = UniqueId($"domain-{token}", DomainIds(draft));

        var treeDomain = new TemplatesBuilderDomainDraft(
            domainId,
            dnsName,
            MakeNetBios(token),
            resolvedForestId,
            nameof(V2DomainRelationKind.Tree),
            string.Empty);

        var draftWithTree = draft with
        {
            Domains = draft.Domains.Append(treeDomain).ToList(),
            IsSaveConfirmed = false
        };

        var withDc = BornWithDomainController(draftWithTree, domainId, dnsName);
        return new TopologyAuthoringResult(
            withDc,
            BuilderForestDomainResourceKind.Domain,
            IndexOfDomain(withDc, domainId));
    }

    /// <summary>
    /// Adds a child domain under the given parent (inheriting the parent's forest, named as a subdomain of
    /// the parent), born with its first domain controller. Selects the new domain.
    /// </summary>
    public static TopologyAuthoringResult AddChildDomain(TemplatesBuilderDraftSnapshot draft, string parentDomainId)
    {
        var parentIndex = IndexOfDomain(draft, parentDomainId);
        if (parentIndex < 0)
        {
            return Unchanged(draft);
        }

        var parent = draft.Domains[parentIndex];
        var takenFirstLabels = FirstLabelsInUse(draft);
        var token = NextFriendlyToken(takenFirstLabels);
        var dnsName = $"{token}.{parent.DnsName}";
        var domainId = UniqueId($"domain-{token}", DomainIds(draft));

        var childDomain = new TemplatesBuilderDomainDraft(
            domainId,
            dnsName,
            MakeNetBios(token),
            parent.ForestId,
            nameof(V2DomainRelationKind.Child),
            parent.DomainId);

        var draftWithChild = draft with
        {
            Domains = draft.Domains.Append(childDomain).ToList(),
            IsSaveConfirmed = false
        };

        var withDc = BornWithDomainController(draftWithChild, domainId, dnsName);
        return new TopologyAuthoringResult(
            withDc,
            BuilderForestDomainResourceKind.Domain,
            IndexOfDomain(withDc, domainId));
    }

    /// <summary>
    /// Deletes a domain. Deleting a forest's root domain removes the whole forest and every domain and VM in
    /// it, because a forest cannot exist without its root. Deleting a non-root domain (a tree or child)
    /// removes only that domain, its descendant domains, and every VM in the removed set. Selects the first
    /// surviving forest, or clears selection when nothing remains.
    /// </summary>
    public static TopologyAuthoringResult DeleteDomain(TemplatesBuilderDraftSnapshot draft, string domainId)
    {
        var target = draft.Domains.FirstOrDefault(domain =>
            string.Equals(domain.DomainId, domainId, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(target.DomainId))
        {
            return Unchanged(draft);
        }

        var owningForest = draft.Forests.FirstOrDefault(forest =>
            string.Equals(forest.RootDomainId, target.DomainId, StringComparison.OrdinalIgnoreCase));
        var deletingForestRoot = !string.IsNullOrWhiteSpace(owningForest.ForestId);

        // Deleting a forest root removes the whole forest. This is allowed even for the only forest: a lab may
        // legitimately hold just standalone (workgroup) machines and no directory at all, and the user can add a
        // forest or standalone machine again from Level 1, so it never dead-ends. Save is still gated on there
        // being at least one machine (see TemplatesBuilderDraftValidator), which is the real floor.

        HashSet<string> removedDomainIds;
        var remainingForests = draft.Forests.ToList();

        if (deletingForestRoot)
        {
            // The whole forest goes: every domain scoped to it, plus the forest entry itself.
            removedDomainIds = draft.Domains
                .Where(domain => string.Equals(domain.ForestId, owningForest.ForestId, StringComparison.OrdinalIgnoreCase))
                .Select(domain => domain.DomainId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            removedDomainIds.Add(target.DomainId);
            remainingForests = remainingForests
                .Where(forest => !string.Equals(forest.ForestId, owningForest.ForestId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        else
        {
            removedDomainIds = CollectSubtree(draft, target.DomainId);
        }

        var remainingDomains = draft.Domains
            .Where(domain => !removedDomainIds.Contains(domain.DomainId))
            .ToList();
        var remainingVms = draft.Vms
            .Where(vm => string.IsNullOrWhiteSpace(vm.DomainId) || !removedDomainIds.Contains(vm.DomainId.Trim()))
            .ToList();

        var result = draft with
        {
            Forests = remainingForests,
            Domains = remainingDomains,
            Vms = remainingVms,
            IsSaveConfirmed = false
        };

        return PostDeleteSelection(result);
    }

    /// <summary>
    /// Deletes a forest by its id (removes the forest and everything in it). Equivalent to deleting its root
    /// domain, and a no-op when the forest has no root yet. Deleting the only forest is allowed: the lab may be
    /// left with just standalone machines (or empty), and Level 1 can always add a forest again.
    /// </summary>
    public static TopologyAuthoringResult DeleteForest(TemplatesBuilderDraftSnapshot draft, string forestId)
    {
        var forest = draft.Forests.FirstOrDefault(item =>
            string.Equals(item.ForestId, forestId, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(forest.ForestId))
        {
            return Unchanged(draft);
        }

        var removedDomainIds = draft.Domains
            .Where(domain => string.Equals(domain.ForestId, forest.ForestId, StringComparison.OrdinalIgnoreCase))
            .Select(domain => domain.DomainId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = draft with
        {
            Forests = draft.Forests
                .Where(item => !string.Equals(item.ForestId, forest.ForestId, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            Domains = draft.Domains.Where(domain => !removedDomainIds.Contains(domain.DomainId)).ToList(),
            Vms = draft.Vms
                .Where(vm => string.IsNullOrWhiteSpace(vm.DomainId) || !removedDomainIds.Contains(vm.DomainId.Trim()))
                .ToList(),
            IsSaveConfirmed = false
        };

        return PostDeleteSelection(result);
    }

    // After a delete, focus the first surviving forest; if the lab is now directory-less, focus the Standalone
    // container when a workgroup machine survived, otherwise leave nothing meaningful selected (Forest/0 clamps
    // to "nothing" when there are no forests). Keeps the canvas and detail panel pointed at something coherent.
    private static TopologyAuthoringResult PostDeleteSelection(TemplatesBuilderDraftSnapshot draft)
    {
        if (draft.Forests.Count > 0)
        {
            return new TopologyAuthoringResult(draft, BuilderForestDomainResourceKind.Forest, 0);
        }

        var hasStandaloneMachine = draft.Vms.Any(vm =>
            V2MembershipModeCatalog.IsStandalone(vm.MembershipMode) && !vm.IsRouter);
        return hasStandaloneMachine
            ? new TopologyAuthoringResult(draft, BuilderForestDomainResourceKind.Standalone, 0)
            : new TopologyAuthoringResult(draft, BuilderForestDomainResourceKind.Forest, 0);
    }

    /// <summary>
    /// Applies a relation change requested from the domain detail panel while preserving the invariants.
    /// A forest root is locked to Root. A non-root domain may only become Tree or Child, never a second Root.
    /// Switching to Tree clears the parent; switching to Child seeds a valid parent (the forest root) when
    /// one is not already set, so the draft never lands in an invalid state.
    /// </summary>
    public static TemplatesBuilderDraftSnapshot ApplyDomainRelationEdit(
        TemplatesBuilderDraftSnapshot draft,
        int domainIndex,
        string requestedRelation)
    {
        if (domainIndex < 0 || domainIndex >= draft.Domains.Count)
        {
            return draft;
        }

        var existing = draft.Domains[domainIndex];
        if (IsForestRoot(draft, existing.DomainId))
        {
            // The root relation is locked; ignore any attempt to change it.
            return existing.RelationKind == nameof(V2DomainRelationKind.Root)
                ? draft
                : ReplaceDomain(draft, domainIndex, existing with { RelationKind = nameof(V2DomainRelationKind.Root) });
        }

        var requested = Enum.TryParse<V2DomainRelationKind>(requestedRelation, ignoreCase: true, out var parsed)
            ? parsed
            : V2DomainRelationKind.Tree;

        // A non-root domain can never become a second root.
        if (requested == V2DomainRelationKind.Root)
        {
            requested = V2DomainRelationKind.Tree;
        }

        // Becoming a Child needs a valid parent. On a well-formed draft the forest root always qualifies, but a
        // malformed/orphan domain (its forest or root is missing) has none - coerce it to Tree rather than emit a
        // Child with an empty parent, honoring the invariant that this method never lands the draft in an invalid
        // relation state.
        var resolvedParent = requested == V2DomainRelationKind.Child
            ? ResolveChildParent(draft, existing)
            : string.Empty;
        if (requested == V2DomainRelationKind.Child && string.IsNullOrWhiteSpace(resolvedParent))
        {
            requested = V2DomainRelationKind.Tree;
        }

        var updated = requested == V2DomainRelationKind.Tree
            ? existing with { RelationKind = nameof(V2DomainRelationKind.Tree), ParentDomainId = string.Empty }
            : existing with
            {
                RelationKind = nameof(V2DomainRelationKind.Child),
                ParentDomainId = resolvedParent
            };

        return ReplaceDomain(draft, domainIndex, updated);
    }

    /// <summary>True when the domain is the root domain of some forest (its relation is locked to Root).</summary>
    public static bool IsForestRoot(TemplatesBuilderDraftSnapshot draft, string domainId)
        => !string.IsNullOrWhiteSpace(domainId) &&
           draft.Forests.Any(forest => string.Equals(forest.RootDomainId, domainId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The display name of a forest: its root domain's DNS name (derived and read-only), falling back to the
    /// forest id when the root cannot be resolved. Renaming the root domain renames the forest automatically.
    /// </summary>
    public static string ResolveForestName(TemplatesBuilderDraftSnapshot draft, TemplatesBuilderForestDraft forest)
        => ResolveForestName(draft.Domains, forest);

    /// <summary>Forest display-name resolution against an explicit domain list (used by the projector).</summary>
    public static string ResolveForestName(
        IReadOnlyList<TemplatesBuilderDomainDraft> domains,
        TemplatesBuilderForestDraft forest)
    {
        var root = domains.FirstOrDefault(domain =>
            string.Equals(domain.DomainId, forest.RootDomainId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(root.DnsName))
        {
            return root.DnsName.Trim();
        }

        return string.IsNullOrWhiteSpace(forest.ForestId) ? string.Empty : forest.ForestId.Trim();
    }

    // ----- helpers -----

    private static TemplatesBuilderDraftSnapshot BornWithDomainController(
        TemplatesBuilderDraftSnapshot draft,
        string domainId,
        string domainDnsName)
    {
        var token = FirstLabel(domainDnsName);
        var existingDcCount = draft.Vms.Count(vm =>
            vm.IsActiveDirectoryDomainController &&
            string.Equals(vm.DomainId, domainId, StringComparison.OrdinalIgnoreCase));

        var takenVmNames = draft.Vms.Select(vm => vm.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var takenVmIds = draft.Vms.Select(vm => vm.VmId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sequence = existingDcCount + 1;
        string name;
        do
        {
            name = $"{token}dc{sequence:D2}";
            sequence++;
        }
        while (takenVmNames.Contains(name));

        var vmId = UniqueId($"vm-{name}", takenVmIds);
        var primaryNetworkId = draft.LabNetworks.Count > 0 ? draft.LabNetworks[0].NetworkId : string.Empty;

        var dc = new TemplatesBuilderVmDraft(
            vmId,
            name,
            "4096",
            "2",
            string.Empty,
            V2MembershipModeCatalog.DomainMember,
            domainId,
            IsActiveDirectoryDomainController: true,
            ResolveDomainControllerCredentialSlots(draft),
            [
                new TemplatesBuilderNicDraft(
                    $"nic-{name}",
                    "Domain",
                    primaryNetworkId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    [])
            ]);

        return draft with { Vms = draft.Vms.Append(dc).ToList() };
    }

    private static TemplatesBuilderVmCredentialSlotDraft ResolveDomainControllerCredentialSlots(
        TemplatesBuilderDraftSnapshot draft)
    {
        string SlotFor(params string[] hints)
        {
            var match = draft.CredentialSlots.FirstOrDefault(slot =>
                hints.Any(hint => (slot.ScopeHint ?? string.Empty).Contains(hint, StringComparison.OrdinalIgnoreCase) ||
                                  (slot.SlotKey ?? string.Empty).Contains(hint, StringComparison.OrdinalIgnoreCase)));
            return string.IsNullOrWhiteSpace(match.SlotKey) ? string.Empty : match.SlotKey;
        }

        return new TemplatesBuilderVmCredentialSlotDraft(
            SlotFor("local"),
            SlotFor("admin", "administration"),
            string.Empty,
            SlotFor("dsrm", "recovery"),
            string.Empty);
    }

    private static string ResolveChildParent(TemplatesBuilderDraftSnapshot draft, TemplatesBuilderDomainDraft domain)
    {
        var domainIds = DomainIds(draft);
        if (!string.IsNullOrWhiteSpace(domain.ParentDomainId) &&
            domainIds.Contains(domain.ParentDomainId.Trim()) &&
            !string.Equals(domain.ParentDomainId.Trim(), domain.DomainId, StringComparison.OrdinalIgnoreCase))
        {
            return domain.ParentDomainId.Trim();
        }

        // Prefer the forest root as the parent, then any other domain in the same forest.
        var forest = draft.Forests.FirstOrDefault(item =>
            string.Equals(item.ForestId, domain.ForestId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(forest.RootDomainId) &&
            !string.Equals(forest.RootDomainId, domain.DomainId, StringComparison.OrdinalIgnoreCase))
        {
            return forest.RootDomainId;
        }

        var sibling = draft.Domains.FirstOrDefault(other =>
            string.Equals(other.ForestId, domain.ForestId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(other.DomainId, domain.DomainId, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(sibling.DomainId) ? string.Empty : sibling.DomainId;
    }

    private static HashSet<string> CollectSubtree(TemplatesBuilderDraftSnapshot draft, string rootDomainId)
    {
        var removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootDomainId };
        var childrenByParent = draft.Domains
            .Where(domain => !string.IsNullOrWhiteSpace(domain.ParentDomainId))
            .GroupBy(domain => domain.ParentDomainId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var queue = new Queue<string>();
        queue.Enqueue(rootDomainId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var child in children.Where(child => removed.Add(child.DomainId)))
            {
                queue.Enqueue(child.DomainId);
            }
        }

        return removed;
    }

    private static TemplatesBuilderDraftSnapshot ReplaceDomain(
        TemplatesBuilderDraftSnapshot draft,
        int index,
        TemplatesBuilderDomainDraft domain)
    {
        var domains = draft.Domains.ToList();
        domains[index] = domain;
        return draft with { Domains = domains, IsSaveConfirmed = false };
    }

    private static TopologyAuthoringResult Unchanged(TemplatesBuilderDraftSnapshot draft)
        => new(draft, BuilderForestDomainResourceKind.Forest, draft.Forests.Count > 0 ? 0 : 0);

    private static string NextFriendlyToken(ISet<string> takenFirstLabels)
    {
        for (var ordinal = 0; ; ordinal++)
        {
            var candidate = FriendlyLabelAt(ordinal);
            if (!takenFirstLabels.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static HashSet<string> FirstLabelsInUse(TemplatesBuilderDraftSnapshot draft)
        => draft.Domains
            .Select(domain => FirstLabel(domain.DnsName))
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> DomainIds(TemplatesBuilderDraftSnapshot draft)
        => draft.Domains
            .Select(domain => domain.DomainId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> ForestIds(TemplatesBuilderDraftSnapshot draft)
        => draft.Forests
            .Select(forest => forest.ForestId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static int IndexOfDomain(TemplatesBuilderDraftSnapshot draft, string domainId)
    {
        for (var index = 0; index < draft.Domains.Count; index++)
        {
            if (string.Equals(draft.Domains[index].DomainId, domainId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static int IndexOfForest(TemplatesBuilderDraftSnapshot draft, string forestId)
    {
        for (var index = 0; index < draft.Forests.Count; index++)
        {
            if (string.Equals(draft.Forests[index].ForestId, forestId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string FirstLabel(string dnsName)
    {
        if (string.IsNullOrWhiteSpace(dnsName))
        {
            return string.Empty;
        }

        var trimmed = dnsName.Trim();
        var dot = trimmed.IndexOf('.');
        return dot < 0 ? trimmed : trimmed[..dot];
    }

    private static string MakeNetBios(string token)
    {
        var cleaned = new string((token ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToUpperInvariant();
        if (cleaned.Length == 0)
        {
            cleaned = "DOMAIN";
        }

        return cleaned.Length > 15 ? cleaned[..15] : cleaned;
    }

    private static string UniqueId(string desired, ISet<string> taken)
    {
        if (!taken.Contains(desired))
        {
            return desired;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{desired}-{suffix}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
