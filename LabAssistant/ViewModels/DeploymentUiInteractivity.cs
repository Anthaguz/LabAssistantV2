using LabAssistant.Models.Deployment;

namespace LabAssistant.ViewModels;

public static class DeploymentUiInteractivity
{
    public static bool HasActiveOperation(bool isDeploying, bool hasActiveContext, DeploymentOperationState state)
    {
        return isDeploying && hasActiveContext && !IsTerminalState(state);
    }

    public static bool IsTerminalState(DeploymentOperationState state)
    {
        return state is DeploymentOperationState.Completed
            or DeploymentOperationState.Cancelled
            or DeploymentOperationState.Failed
            or DeploymentOperationState.FailedWithResiduals
            or DeploymentOperationState.CancelledWithResiduals;
    }
}
