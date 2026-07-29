using LabAssistant.Models.Deployment;
using LabAssistant.Services.GuestExecution;

namespace LabAssistant.Business.Runtime;

/// <summary>
/// Bounded transport-drop resilience for an idempotent in-guest PowerShell Direct step, shared by every V2 runtime
/// stage that mutates or validates a guest over a single hop (guest-network prep, base remote access, the router
/// configuration and validation steps, AD DS install, DNS stabilization, and the forest-trust DNS/create/cleanup
/// steps).
/// </summary>
/// <remarks>
/// A lost PowerShell Direct session - classified as <see cref="GuestCommandErrorCategory.GuestRebooting"/> because the
/// guest rebooted mid-hop or a loaded host tore the socket down ("The Hyper-V socket target process has ended" /
/// PSSessionStateBroken / PSRemotingTransportException) - is retried up to
/// <see cref="V2RuntimeExecutionRequest.GuestTransportMaxRetries"/>, spaced by
/// <see cref="V2RuntimeExecutionRequest.GuestTransportRetryDelay"/>, because re-issuing an idempotent command simply
/// reconnects and re-confirms. Every other failure category is a genuine, deterministic in-guest error and is returned
/// immediately so the caller fails fast instead of burning the retry budget. The caller owns the terminal
/// <c>MarkFailure</c> so it can attach the step-specific message.
///
/// ONLY wrap operations that are safe to re-run (idempotent scripts, or scripts that guard the mutation behind an
/// existence check). A non-idempotent mutation with no such guard - forest promotion (<c>Install-ADDSForest</c>) or
/// domain join (<c>Add-Computer</c>) - must NOT use this; those tolerate their own expected restart boundary instead.
///
/// <paramref name="context"/> is optional: when supplied, each failed attempt is logged and emitted through
/// <see cref="GuestReadinessLog.Attempt"/> and the step honors the context's abort signal. Pass <see langword="null"/>
/// for the no-orphans cleanup path, which runs under <see cref="System.Threading.CancellationToken.None"/> and must
/// not consult the (already-cancelled) deploy abort signal; that path still classifies the drop the same way but skips
/// per-attempt logging (the cleanup stage emits its own outcome event).
///
/// <paramref name="retryAuthenticationRejection"/> defaults to <see langword="false"/> so the deploy path preserves its
/// fail-fast contract: a deterministic credential rejection surfaces immediately and never burns the retry budget. It
/// is opted into ONLY by the rollback cleanup path, where a broken PowerShell Direct session on a DC that is rebooting
/// or being torn down surfaces "The credential is invalid" wrapped in an <c>OpenError</c> / <c>PSSessionStateBroken</c> /
/// <c>PSDirectException</c> - the finding-81 transient, which is content-indistinguishable from a genuine wrong password
/// (both carry that identical structured identity). That case cannot be told apart by content, so the deploy path must
/// still fail fast on it. Cleanup can safely tolerate it because it has context the deploy path lacks: the domain-admin
/// credential was already validated when the trust was created, the delete script is idempotent (GetADTrust-guarded),
/// and the retry is still bounded by <see cref="V2RuntimeExecutionRequest.GuestTransportMaxRetries"/>, so a genuinely
/// bad credential would only slow the best-effort cleanup before residual is flagged, never hang it forever. That
/// budget (default 90 x 10s = ~15 min) also comfortably outlasts the ~340-360s a rebooting DC needs to become
/// reachable again during rollback, so the cleanup retry spans the reboot rather than expiring mid-recovery.
/// </remarks>
internal static class GuestStepTransportRetry
{
    public static async Task<GuestCommandResult> RunAsync(
        VmDeploymentContext? context,
        V2RuntimeExecutionRequest request,
        string stepKey,
        Func<CancellationToken, Task<GuestCommandResult>> operation,
        CancellationToken cancellationToken,
        bool retryAuthenticationRejection = false)
    {
        var startTick = Environment.TickCount64;
        var attempt = 1;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context?.ShouldAbort?.Invoke() == true)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            var result = await operation(cancellationToken);
            if (result.Success)
            {
                return result;
            }

            // With a context we log + emit the per-attempt readiness event (and classify inside it); without one
            // (cleanup) we still classify identically but stay silent per attempt.
            var category = context is not null
                ? GuestReadinessLog.Attempt(
                    context,
                    stepKey,
                    attempt,
                    request.GuestTransportMaxRetries,
                    Environment.TickCount64 - startTick,
                    result.Error)
                : GuestErrorClassifier.Classify(result.Error);

            // A torn-down PowerShell Direct session is always worth re-running. The rollback cleanup path additionally
            // opts into retrying a credential rejection, because a session that broke while a DC reboots surfaces
            // "the credential is invalid" indistinguishably from a genuine wrong password (see the type remarks). Every
            // other category is a real, deterministic error that must surface immediately, and the retry budget is
            // finite so a persistently failing guest still fails rather than looping forever.
            var retryable = category == GuestCommandErrorCategory.GuestRebooting
                || (retryAuthenticationRejection && category == GuestCommandErrorCategory.AuthenticationRejected);
            if (!retryable || attempt >= request.GuestTransportMaxRetries)
            {
                return result;
            }

            await Task.Delay(request.GuestTransportRetryDelay, cancellationToken);
            attempt++;
        }
    }
}
