using LabAssistant.Models.Deployment;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.GuestExecution;

namespace LabAssistant.Business.Runtime;

/// <summary>
/// Shared per-attempt observability for the guest readiness retry loops in the V2 runtime.
/// </summary>
/// <remarks>
/// Every readiness gate (guest transport, base remote access, domain readiness, replica readiness, joined-domain
/// readiness, forest-trust validation) polls the guest in a bounded retry loop. Before this helper those loops
/// logged nothing per attempt, so a deploy that sat in a ~15 minute retry produced no trace of why. This centralizes
/// two things so every loop behaves the same:
/// <list type="bullet">
/// <item>a structured event plus a human-readable log line per failed attempt (attempt count, elapsed time,
/// classified category, and a redacted error summary), and</item>
/// <item>the classification itself, so a loop that authenticates with the local bootstrap credential can fail fast
/// on a deterministic credential rejection instead of exhausting its whole budget.</item>
/// </list>
/// The guest password is injected out-of-band as a secure runspace variable and never appears in the command text
/// or in the guest error string, so logging the error summary here cannot leak a secret.
/// </remarks>
internal static class GuestReadinessLog
{
    private const int MaxErrorSummaryLength = 400;

    /// <summary>
    /// Records one failed readiness attempt and returns its classified category.
    /// </summary>
    public static GuestCommandErrorCategory Attempt(
        VmDeploymentContext context,
        string stepKey,
        int attempt,
        int maxRetries,
        long elapsedMs,
        string? error)
    {
        var category = GuestErrorClassifier.Classify(error);
        var summary = SummarizeError(error);

        context.LogCallback?.Invoke(
            $"[{stepKey}] {context.VmName}: attempt {attempt}/{maxRetries} not ready after {elapsedMs} ms ({category})"
            + (summary.Length == 0 ? string.Empty : $": {summary}"));

        context.StructuredEventEmitter?.Invoke(
            category == GuestCommandErrorCategory.AuthenticationRejected
                ? LaStatus.DeployGuest_ReadinessAttemptCredentialRejected
                : LaStatus.DeployGuest_ReadinessAttemptNotReady,
            "retrying",
            new Dictionary<string, object?>
            {
                ["stepKey"] = stepKey,
                ["attempt"] = attempt,
                ["maxRetries"] = maxRetries,
                ["elapsedMs"] = elapsedMs,
                ["errorCategory"] = category.ToString(),
                ["error"] = summary
            });

        return category;
    }

    /// <summary>
    /// Builds an actionable failure message for a deterministic credential rejection during guest login.
    /// </summary>
    public static string DescribeCredentialRejection(string vmName, string? slotKey, string username, string? error)
    {
        var slot = string.IsNullOrWhiteSpace(slotKey)
            ? "the bootstrap credential"
            : $"credential slot '{slotKey}'";
        var summary = SummarizeError(error);
        return $"Guest '{vmName}' rejected {slot} (user '{username}'): the registered password does not match this "
            + "VM's base image. Update the credential and redeploy."
            + (summary.Length == 0 ? string.Empty : $" Last guest error: {summary}");
    }

    /// <summary>
    /// Collapses whitespace and truncates a guest error string so it stays readable in logs.
    /// </summary>
    public static string SummarizeError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return string.Empty;
        }

        var collapsed = string.Join(' ', error.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= MaxErrorSummaryLength
            ? collapsed
            : collapsed[..MaxErrorSummaryLength] + "...";
    }
}
