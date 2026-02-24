using System.Collections.Generic;
using System.Threading;

namespace LabAssistant.Models.Deployment;

public class MultiVmDeploymentContext
{
    private readonly CancellationTokenSource _operationCancellation = new();

    public string OperationId { get; set; } = Guid.NewGuid().ToString("N");
    public List<VmDeploymentContext> VmContexts { get; set; } = new();
    public bool StopAllOnAnyVmFailure { get; set; }
    public List<VmCleanupResult> CleanupResults { get; } = new();
    public bool UserCancellationRequested { get; private set; }
    public DeploymentOperationState OperationState { get; private set; } = DeploymentOperationState.Idle;
    public event EventHandler<DeploymentOperationStateChangedEventArgs>? OperationStateChanged;

    public bool IsCancellationRequested => _operationCancellation.IsCancellationRequested;
    public CancellationToken CancellationToken => _operationCancellation.Token;

    public void MarkRunning() => SetOperationState(DeploymentOperationState.Running);

    public void RequestUserCancellation()
    {
        UserCancellationRequested = true;
        SetOperationState(DeploymentOperationState.Cancelling);
        RequestCancellation();
    }

    public void RequestCancellation()
    {
        if (!_operationCancellation.IsCancellationRequested)
        {
            _operationCancellation.Cancel();
        }
    }

    public void MarkCleanupInProgress() => SetOperationState(DeploymentOperationState.CleanupInProgress);

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
