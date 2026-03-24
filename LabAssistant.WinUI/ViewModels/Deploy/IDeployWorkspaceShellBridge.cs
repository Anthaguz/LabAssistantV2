namespace LabAssistant.WinUI.ViewModels.Deploy;

internal interface IDeployWorkspaceShellBridge
{
    bool IsDeployCapabilityActive { get; }

    bool IsDeployOverviewActive { get; }

    bool IsDeployOnTheFlyActive { get; }

    bool IsDeployFromTemplateActive { get; }

    void NavigateToRoute(string routeKey);
}
