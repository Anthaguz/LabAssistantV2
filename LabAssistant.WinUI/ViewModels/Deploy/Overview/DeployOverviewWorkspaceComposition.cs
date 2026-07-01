using LabAssistant.WinUI.Views.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployOverviewWorkspaceComposition
{
    private readonly DeployOverviewView _view;
    private readonly DeployOverviewViewModel _workspace;
    private readonly IDeployOverviewWorkspaceHost _host;
    private readonly IDeployOverviewWorkspaceShellBridge _shellBridge;

    public DeployOverviewWorkspaceComposition(
        DeployOverviewView view,
        IDeployOverviewWorkspaceHost host,
        IDeployOverviewWorkspaceShellBridge shellBridge)
    {
        _view = view;
        _workspace = view.ViewModel;
        _host = host;
        _shellBridge = shellBridge;
        WireHandlers();
        RefreshSummary();
    }

    public void RefreshUiState()
    {
        if (!_shellBridge.IsDeployOverviewActive)
        {
            return;
        }

        RefreshSummary();
    }

    public void ApplyShellState()
    {
        if (!_shellBridge.IsDeployOverviewActive)
        {
            return;
        }

        RefreshSummary();
    }

    private void WireHandlers()
    {
        _workspace.OpenQuickDeployRequested += OpenQuickDeployRequested;
        _workspace.OpenFromTemplateRequested += OpenFromTemplateRequested;
    }

    private void RefreshSummary()
    {
        _workspace.RefreshSummary(
            _host.QuickDeployDraftCount,
            _host.IsLoadingTemplates,
            _host.AvailableTemplateCount);
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
