namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployWorkspaceShellBridge : IDeployWorkspaceShellBridge
{
    private readonly Func<bool> _isDeployCapabilityActive;
    private readonly Func<bool> _isDeployOverviewActive;
    private readonly Func<bool> _isDeployOnTheFlyActive;
    private readonly Func<bool> _isDeployFromTemplateActive;
    private readonly Action<string> _navigateToRoute;

    public DeployWorkspaceShellBridge(
        Func<bool> isDeployCapabilityActive,
        Func<bool> isDeployOverviewActive,
        Func<bool> isDeployOnTheFlyActive,
        Func<bool> isDeployFromTemplateActive,
        Action<string> navigateToRoute)
    {
        _isDeployCapabilityActive = isDeployCapabilityActive;
        _isDeployOverviewActive = isDeployOverviewActive;
        _isDeployOnTheFlyActive = isDeployOnTheFlyActive;
        _isDeployFromTemplateActive = isDeployFromTemplateActive;
        _navigateToRoute = navigateToRoute;
    }

    public bool IsDeployCapabilityActive => _isDeployCapabilityActive();

    public bool IsDeployOverviewActive => _isDeployOverviewActive();

    public bool IsDeployOnTheFlyActive => _isDeployOnTheFlyActive();

    public bool IsDeployFromTemplateActive => _isDeployFromTemplateActive();

    public void NavigateToRoute(string routeKey) => _navigateToRoute(routeKey);
}
