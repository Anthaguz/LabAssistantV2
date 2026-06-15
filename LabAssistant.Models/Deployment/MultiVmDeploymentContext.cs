using System.Collections.Generic;
using System.Threading;

namespace LabAssistant.Models.Deployment;

/// <summary>
/// Tracks VM, trust, cancellation, and cleanup state for one multi-VM deployment operation.
/// </summary>
public class MultiVmDeploymentContext
{
    private readonly CancellationTokenSource _operationCancellation = new();

    /// <summary>
    /// Stable identifier propagated through logs and runtime workflow state for this operation.
    /// </summary>
    public string OperationId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Runtime state for each VM participating in the deployment operation.
    /// </summary>
    public List<VmDeploymentContext> VmContexts { get; set; } = new();

    /// <summary>
    /// Runtime state for managed V2 forest trusts that may require cleanup if the deployment fails or is cancelled.
    /// </summary>
    public List<V2TrustRuntimeContext> V2TrustContexts { get; } = new();

    /// <summary>
    /// Indicates whether any VM blocking failure should cancel the remaining deployment work.
    /// </summary>
    public bool StopAllOnAnyVmFailure { get; set; }

    /// <summary>
    /// Cleanup results recorded for VM resources created during this operation.
    /// </summary>
    public List<VmCleanupResult> CleanupResults { get; } = new();

    /// <summary>
    /// Indicates whether cancellation was explicitly requested by the user.
    /// </summary>
    public bool UserCancellationRequested { get; private set; }

    /// <summary>
    /// Current operation lifecycle state.
    /// </summary>
    public DeploymentOperationState OperationState { get; private set; } = DeploymentOperationState.Idle;

    /// <summary>
    /// Raised when the operation lifecycle state changes.
    /// </summary>
    public event EventHandler<DeploymentOperationStateChangedEventArgs>? OperationStateChanged;

    /// <summary>
    /// Indicates whether this operation has requested cancellation.
    /// </summary>
    public bool IsCancellationRequested => _operationCancellation.IsCancellationRequested;

    /// <summary>
    /// Cancellation token shared across runtime workflow steps.
    /// </summary>
    public CancellationToken CancellationToken => _operationCancellation.Token;

    /// <summary>
    /// Marks the deployment operation as running.
    /// </summary>
    public void MarkRunning() => SetOperationState(DeploymentOperationState.Running);

    /// <summary>
    /// Requests user-driven cancellation and moves the operation into the cancelling state.
    /// </summary>
    public void RequestUserCancellation()
    {
        UserCancellationRequested = true;
        SetOperationState(DeploymentOperationState.Cancelling);
        RequestCancellation();
    }

    /// <summary>
    /// Requests cancellation without changing whether the request came from the user.
    /// </summary>
    public void RequestCancellation()
    {
        if (!_operationCancellation.IsCancellationRequested)
        {
            _operationCancellation.Cancel();
        }
    }

    /// <summary>
    /// Marks that cleanup work is running after a failure or cancellation.
    /// </summary>
    public void MarkCleanupInProgress() => SetOperationState(DeploymentOperationState.CleanupInProgress);

    /// <summary>
    /// Moves the operation to the final state that matches cancellation, failure, and cleanup residuals.
    /// </summary>
    public void CompleteTerminalState(bool hasFailures, bool hasCleanupResiduals)
    {
        if (UserCancellationRequested)
        {
            SetOperationState(hasCleanupResiduals
                ? DeploymentOperationState.CancelledWithResiduals
                : DeploymentOperationState.Cancelled);
            return;
        }

        if (hasFailures)
        {
            SetOperationState(hasCleanupResiduals
                ? DeploymentOperationState.FailedWithResiduals
                : DeploymentOperationState.Failed);
            return;
        }

        SetOperationState(DeploymentOperationState.Completed);
    }

    private void SetOperationState(DeploymentOperationState state)
    {
        if (OperationState == state)
        {
            return;
        }

        OperationState = state;
        OperationStateChanged?.Invoke(this, new DeploymentOperationStateChangedEventArgs(state));
    }
}

/// <summary>
/// Tracks runtime and cleanup state for one managed V2 forest trust.
/// </summary>
public sealed class V2TrustRuntimeContext
{
    /// <summary>
    /// Resolved trust identifier from the V2 plan.
    /// </summary>
    public string TrustId { get; init; } = string.Empty;

    /// <summary>
    /// Source domain identifier participating in the trust.
    /// </summary>
    public string SourceDomainId { get; init; } = string.Empty;

    /// <summary>
    /// Target domain identifier participating in the trust.
    /// </summary>
    public string TargetDomainId { get; init; } = string.Empty;

    /// <summary>
    /// Indicates that trust object creation was attempted and cleanup may be required on failure or cancellation.
    /// </summary>
    public bool TrustObjectsCreated { get; set; }

    /// <summary>
    /// Indicates that the trust reached the runtime-ready state after validation.
    /// </summary>
    public bool TrustReady { get; set; }

    /// <summary>
    /// Indicates that cleanup was attempted for this trust.
    /// </summary>
    public bool CleanupAttempted { get; set; }

    /// <summary>
    /// Indicates that cleanup could not fully remove the trust objects.
    /// </summary>
    public bool CleanupResidual { get; set; }
}
