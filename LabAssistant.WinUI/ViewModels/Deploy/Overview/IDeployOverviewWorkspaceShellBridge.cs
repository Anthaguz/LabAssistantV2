namespace LabAssistant.WinUI.ViewModels.Deploy;

internal interface IDeployOverviewWorkspaceShellBridge
{
    bool IsDeployOverviewActive { get; }

    void NavigateToRoute(string routeKey);
}
