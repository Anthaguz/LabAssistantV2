using System;

namespace LabAssistant.Models.Deployment;

public enum DeploymentOperationState
{
    Idle,
    Running,
    Cancelling,
    CleanupInProgress,
    Completed,
    Cancelled,
    Failed,
    FailedWithResiduals,
    CancelledWithResiduals
}

public sealed class DeploymentOperationStateChangedEventArgs : EventArgs
{
    public DeploymentOperationStateChangedEventArgs(DeploymentOperationState state)
    {
        State = state;
    }

    public DeploymentOperationState State { get; }
}
