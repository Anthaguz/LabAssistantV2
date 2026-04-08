namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Coordinates Deploy lane panel intent while leaving shell container sizing, visibility, and open-state ownership in MainWindow.
/// </summary>
internal sealed class DeployResultsPanelCoordinator
{
    private readonly DeployOnTheFlyWorkspaceOwner _onTheFlyWorkspaceOwner;
    private readonly DeployFromTemplateWorkspaceOwner _fromTemplateWorkspaceOwner;
    private readonly Func<bool> _isDeployOverviewActive;
    private readonly Func<bool> _isDeployOnTheFlyActive;
    private readonly Func<bool> _isDeployFromTemplateActive;

    public DeployResultsPanelCoordinator(
        DeployOnTheFlyWorkspaceOwner onTheFlyWorkspaceOwner,
        DeployFromTemplateWorkspaceOwner fromTemplateWorkspaceOwner,
        Func<bool> isDeployOverviewActive,
        Func<bool> isDeployOnTheFlyActive,
        Func<bool> isDeployFromTemplateActive)
    {
        _onTheFlyWorkspaceOwner = onTheFlyWorkspaceOwner;
        _fromTemplateWorkspaceOwner = fromTemplateWorkspaceOwner;
        _isDeployOverviewActive = isDeployOverviewActive;
        _isDeployOnTheFlyActive = isDeployOnTheFlyActive;
        _isDeployFromTemplateActive = isDeployFromTemplateActive;
    }

    public void ResetRightPanelBehavior()
    {
        _fromTemplateWorkspaceOwner.ResetPanelState();
    }

    public bool ShouldAutoOpenRightPanel()
    {
        return _fromTemplateWorkspaceOwner.ShouldAutoOpenResultsPanel ||
            _onTheFlyWorkspaceOwner.ShouldAutoOpenResultsPanel;
    }

    public void ApplyRightPanelState(bool showPanel, bool panelUnavailable)
    {
        _fromTemplateWorkspaceOwner.ApplyResultsPanelState(_isDeployFromTemplateActive(), showPanel, panelUnavailable);
        _onTheFlyWorkspaceOwner.ApplyResultsPanelState(_isDeployOnTheFlyActive(), showPanel, panelUnavailable);
    }

    public string GetRightPanelTitleText()
    {
        return _isDeployFromTemplateActive()
            ? _fromTemplateWorkspaceOwner.ResultsPanelTitle
            : _isDeployOnTheFlyActive()
                ? _onTheFlyWorkspaceOwner.ResultsPanelTitle
                : "Details";
    }

    public bool ShouldShowRightPanelEmptyState(bool showPanel)
    {
        return showPanel && _isDeployOverviewActive() && !_isDeployFromTemplateActive() && !_isDeployOnTheFlyActive();
    }
}
