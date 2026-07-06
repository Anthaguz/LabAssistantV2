namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// Pure helper that merges an existing Builder validation state with the result of a scoped
/// revalidation, preserving issues from categories/scopes that were not re-evaluated and replacing
/// issues from the scopes that were. Extracted unchanged from the dissolved Builder workspace view
/// model so the scoped-revalidation contract stays covered by unit tests without a runtime.
/// </summary>
internal static class TemplatesBuilderValidationMerger
{
    public static TemplatesBuilderValidationState Merge(
        TemplatesBuilderValidationState existing,
        TemplatesBuilderValidationState updated)
    {
        var blockers = existing.Blockers
            .Where(issue => !IsIssueRefreshed(issue, updated))
            .Concat(updated.Blockers)
            .ToList();
        var warnings = existing.Warnings
            .Where(issue => !IsIssueRefreshed(issue, updated))
            .Concat(updated.Warnings)
            .ToList();
        var categories = existing.EvaluatedCategories
            .Concat(updated.EvaluatedCategories)
            .ToHashSet();
        var domainIds = existing.EvaluatedDomainIds
            .Concat(updated.EvaluatedDomainIds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var networkIds = existing.EvaluatedNetworkIds
            .Concat(updated.EvaluatedNetworkIds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var vmIds = existing.EvaluatedVmIds
            .Concat(updated.EvaluatedVmIds)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new TemplatesBuilderValidationState(blockers, warnings, categories, domainIds, networkIds, vmIds);
    }

    private static bool IsIssueRefreshed(
        TemplatesBuilderValidationIssue issue,
        TemplatesBuilderValidationState updated)
    {
        if (!updated.EvaluatedCategories.Contains(issue.Category))
        {
            return false;
        }

        return issue.Category switch
        {
            TemplatesBuilderValidationCategory.Domain => IsFullScope(updated.EvaluatedDomainIds) || MatchesScope(issue, updated.EvaluatedDomainIds),
            TemplatesBuilderValidationCategory.Network => IsFullScope(updated.EvaluatedNetworkIds) || MatchesScope(issue, updated.EvaluatedNetworkIds),
            TemplatesBuilderValidationCategory.VmIdentity => true,
            TemplatesBuilderValidationCategory.VmMembership =>
                (IsFullScope(updated.EvaluatedVmIds) && IsFullScope(updated.EvaluatedDomainIds)) ||
                MatchesScope(issue, updated.EvaluatedVmIds) ||
                MatchesScope(issue, updated.EvaluatedDomainIds),
            _ => true
        };
    }

    private static bool IsFullScope(IReadOnlySet<string> scopeIds) => scopeIds.Count == 0;

    private static bool MatchesScope(TemplatesBuilderValidationIssue issue, IReadOnlySet<string> scopeIds)
        => issue.ScopeKey is not null && scopeIds.Contains(issue.ScopeKey);
}
