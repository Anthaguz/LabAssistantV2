namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployOverviewWorkspaceShellBridge : IDeployOverviewWorkspaceShellBridge
{
    private readonly Func<bool> _isDeployOverviewActive;
    private readonly Action<string> _navigateToRoute;

    public DeployOverviewWorkspaceShellBridge(
        Func<bool> isDeployOverviewActive,
        Action<string> navigateToRoute)
    {
        _isDeployOverviewActive = isDeployOverviewActive;
        _navigateToRoute = navigateToRoute;
    }

    public bool IsDeployOverviewActive => _isDeployOverviewActive();

    public void NavigateToRoute(string routeKey) => _navigateToRoute(routeKey);
}
