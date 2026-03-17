using LabAssistant.WinUI.Views.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal interface IDeployOverviewWorkspaceHost
{
    int QuickDeployDraftCount { get; }

    bool IsLoadingTemplates { get; }

    int AvailableTemplateCount { get; }
}

internal interface IDeployOverviewWorkspaceShellBridge
{
    bool IsDeployOverviewActive { get; }

    void NavigateToRoute(string routeKey);
}

internal sealed class DeployOverviewWorkspaceHost : IDeployOverviewWorkspaceHost
{
    private readonly Func<int> _getQuickDeployDraftCount;
    private readonly Func<bool> _isLoadingTemplates;
    private readonly Func<int> _getAvailableTemplateCount;

    public DeployOverviewWorkspaceHost(
        Func<int> getQuickDeployDraftCount,
        Func<bool> isLoadingTemplates,
        Func<int> getAvailableTemplateCount)
    {
        _getQuickDeployDraftCount = getQuickDeployDraftCount;
        _isLoadingTemplates = isLoadingTemplates;
        _getAvailableTemplateCount = getAvailableTemplateCount;
    }

    public int QuickDeployDraftCount => _getQuickDeployDraftCount();

    public bool IsLoadingTemplates => _isLoadingTemplates();

    public int AvailableTemplateCount => _getAvailableTemplateCount();
}

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

internal sealed class DeployOverviewWorkspaceComposition
{
    private readonly DeployOverviewView _view;
    private readonly DeployOverviewWorkspaceViewModel _workspace = new();
    private readonly IDeployOverviewWorkspaceHost _host;
    private readonly IDeployOverviewWorkspaceShellBridge _shellBridge;

    public DeployOverviewWorkspaceComposition(
        DeployOverviewView view,
        IDeployOverviewWorkspaceHost host,
        IDeployOverviewWorkspaceShellBridge shellBridge)
    {
        _view = view;
        _host = host;
        _shellBridge = shellBridge;
        WireHandlers();
        ApplyWorkspaceState();
    }

    public void RefreshUiState()
    {
        if (!_shellBridge.IsDeployOverviewActive)
        {
            return;
        }

        RefreshSummary();
        ApplyWorkspaceState();
    }

    public void ApplyShellState()
    {
        if (!_shellBridge.IsDeployOverviewActive)
        {
            return;
        }

        RefreshSummary();
        ApplyWorkspaceState();
    }

    private void WireHandlers()
    {
        _view.OpenQuickDeployRequested += OpenQuickDeployRequested;
        _view.OpenFromTemplateRequested += OpenFromTemplateRequested;
    }

    private void RefreshSummary()
    {
        _workspace.RefreshSummary(
            _host.QuickDeployDraftCount,
            _host.IsLoadingTemplates,
            _host.AvailableTemplateCount);
    }

    private void ApplyWorkspaceState()
    {
        _view.UpdateSummary(_workspace.QuickDeploySummaryText, _workspace.FromTemplateSummaryText);
    }

    private void OpenQuickDeployRequested(object? sender, EventArgs e)
    {
        _shellBridge.NavigateToRoute(ShellRouteKeys.DeployOnTheFly);
    }

    private void OpenFromTemplateRequested(object? sender, EventArgs e)
    {
        _shellBridge.NavigateToRoute(ShellRouteKeys.DeployFromTemplate);
    }
}
