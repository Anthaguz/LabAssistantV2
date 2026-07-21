using LabAssistant.Models.Deployment;
using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// A single de-duplicated readiness/compatibility concern surfaced to the user. It unifies the two
/// overlapping issue sources (editor-time compatibility issues and the authoritative preflight
/// readiness report) into one entry per real problem so counts and reasons stay honest.
/// </summary>
internal sealed record DeployMergedIssue(
    string Scope,
    bool IsBlocking,
    string Message,
    string Guidance,
    DeploymentReadinessCategory Category);

/// <summary>
/// Merges the two issue sources that describe the same deployment readiness reality - the
/// editor-time <see cref="DeployCompatibilityIssue"/> list and the authoritative
/// <see cref="DeploymentReadinessReport"/> - into one de-duplicated list. Before this projection the
/// same underlying problem (for example a missing base disk) was reported by both sources and counted
/// twice, so the UI showed "Blocking: 2" for a single issue.
///
/// De-duplication is deliberately asymmetric so it corrects the cross-source double count without
/// hiding real problems. Every distinct readiness result is kept, because a single VM legitimately
/// raises several failures that share a (scope, category, severity) tuple (for example three separate
/// guest-configuration gaps), and each carries its own reason the user must see. An editor-time
/// compatibility issue is suppressed only when a readiness result already covers the same
/// (scope, category, severity), since in that case both sources describe the same concern and the
/// readiness result carries the more precise, user-facing message.
/// </summary>
internal static class DeployReadinessProjection
{
    public const string GlobalScope = "Global";

    public static IReadOnlyList<DeployMergedIssue> Merge(
        IReadOnlyList<DeployCompatibilityIssue> compatibilityIssues,
        DeploymentReadinessReport? readinessReport)
    {
        var merged = new List<DeployMergedIssue>();

        // Tuples covered by the authoritative readiness report. Used only to suppress the overlapping
        // editor-time compatibility issue for the same concern - never to collapse distinct readiness
        // results against one another.
        var readinessCovered = new HashSet<(string Scope, DeploymentReadinessCategory Category, bool IsBlocking)>();

        if (readinessReport is not null)
        {
            foreach (var result in readinessReport.Results)
            {
                if (result.Status is not (DeploymentReadinessStatus.Fail or DeploymentReadinessStatus.Warn))
                {
                    continue;
                }

                var isBlocking = result.Status == DeploymentReadinessStatus.Fail;
                var scopes = result.AffectedVmNames.Count > 0
                    ? result.AffectedVmNames
                    : [GlobalScope];

                foreach (var rawScope in scopes)
                {
                    var scope = NormalizeScope(rawScope);
                    readinessCovered.Add((KeyScope(scope), result.Category, isBlocking));
                    merged.Add(new DeployMergedIssue(scope, isBlocking, result.Message.Trim(), result.ActionableGuidance.Trim(), result.Category));
                }
            }
        }

        foreach (var issue in compatibilityIssues)
        {
            var scope = NormalizeScope(issue.VmName);
            if (!readinessCovered.Contains((KeyScope(scope), issue.Category, issue.IsBlocking)))
            {
                merged.Add(new DeployMergedIssue(scope, issue.IsBlocking, issue.Message.Trim(), issue.Guidance.Trim(), issue.Category));
            }
        }

        return merged;
    }

    public static (int Blocking, int Warnings) Count(IReadOnlyList<DeployMergedIssue> merged)
    {
        var blocking = merged.Count(issue => issue.IsBlocking);
        return (blocking, merged.Count - blocking);
    }

    public static bool ScopeMatches(DeployMergedIssue issue, string vmName) =>
        string.Equals(issue.Scope, vmName, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeScope(string? scope) =>
        string.IsNullOrWhiteSpace(scope) ? GlobalScope : scope.Trim();

    private static string KeyScope(string scope) => scope.ToLowerInvariant();
}
