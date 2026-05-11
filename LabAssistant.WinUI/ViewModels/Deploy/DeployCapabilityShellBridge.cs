using LabAssistant.Models.Deployment;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Exposes only the shell-owned interactions and route state that the Deploy runtime still needs.
/// </summary>
internal sealed class DeployCapabilityShellBridge
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Func<XamlRoot?> _getXamlRoot;
    private readonly Func<bool> _isDeployCapabilityActive;
    private readonly Func<bool> _isDeployOverviewActive;
    private readonly Func<bool> _isDeployOnTheFlyActive;
    private readonly Func<bool> _isDeployFromTemplateActive;
    private readonly Action<string> _navigateToRoute;
    private readonly Action _requestResultsPanelToggle;
    private readonly Action _refreshResultsPanelState;

    public DeployCapabilityShellBridge(
        DispatcherQueue dispatcherQueue,
        Func<XamlRoot?> getXamlRoot,
        Func<bool> isDeployCapabilityActive,
        Func<bool> isDeployOverviewActive,
        Func<bool> isDeployOnTheFlyActive,
        Func<bool> isDeployFromTemplateActive,
        Action<string> navigateToRoute,
        Action requestResultsPanelToggle,
        Action refreshResultsPanelState)
    {
        _dispatcherQueue = dispatcherQueue;
        _getXamlRoot = getXamlRoot;
        _isDeployCapabilityActive = isDeployCapabilityActive;
        _isDeployOverviewActive = isDeployOverviewActive;
        _isDeployOnTheFlyActive = isDeployOnTheFlyActive;
        _isDeployFromTemplateActive = isDeployFromTemplateActive;
        _navigateToRoute = navigateToRoute;
        _requestResultsPanelToggle = requestResultsPanelToggle;
        _refreshResultsPanelState = refreshResultsPanelState;
    }

    public DispatcherQueue DispatcherQueue => _dispatcherQueue;

    public XamlRoot? XamlRoot => _getXamlRoot();

    public bool IsDeployCapabilityActive => _isDeployCapabilityActive();

    public bool IsDeployOverviewActive => _isDeployOverviewActive();

    public bool IsDeployOnTheFlyActive => _isDeployOnTheFlyActive();

    public bool IsDeployFromTemplateActive => _isDeployFromTemplateActive();

    public void NavigateToRoute(string routeKey) => _navigateToRoute(routeKey);

    public void RequestResultsPanelToggle() => _requestResultsPanelToggle();

    public void RefreshResultsPanelState() => _refreshResultsPanelState();

    public void AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated)
    {
        foreach (var vmContext in context.VmContexts)
        {
            var vmName = string.IsNullOrWhiteSpace(vmContext.VmName) ? "Unnamed-VM" : vmContext.VmName.Trim();
            vmContext.LogCallback = message => _dispatcherQueue.TryEnqueue(() => onLogMessage(vmName, message));
            vmContext.StepStateEmitter = update => _dispatcherQueue.TryEnqueue(() => onStepStateUpdated(vmName, update));
        }
    }
}
