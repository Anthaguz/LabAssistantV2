using LabAssistant.WinUI.Views.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

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
