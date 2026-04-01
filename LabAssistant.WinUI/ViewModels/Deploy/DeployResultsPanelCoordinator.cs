namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Coordinates Deploy lane panel intent while leaving shell container sizing, visibility, and open-state ownership in MainWindow.
/// </summary>
internal sealed class DeployResultsPanelCoordinator
{
    private readonly DeployOnTheFlyWorkspaceOwner _onTheFlyWorkspaceOwner;
    private readonly DeployFromTemplateWorkspaceComposition _fromTemplateWorkspaceComposition;
    private readonly Func<bool> _isDeployOverviewActive;
    private readonly Func<bool> _isDeployOnTheFlyActive;
    private readonly Func<bool> _isDeployFromTemplateActive;

    public DeployResultsPanelCoordinator(
        DeployOnTheFlyWorkspaceOwner onTheFlyWorkspaceOwner,
        DeployFromTemplateWorkspaceComposition fromTemplateWorkspaceComposition,
        Func<bool> isDeployOverviewActive,
        Func<bool> isDeployOnTheFlyActive,
        Func<bool> isDeployFromTemplateActive)
    {
        _onTheFlyWorkspaceOwner = onTheFlyWorkspaceOwner;
        _fromTemplateWorkspaceComposition = fromTemplateWorkspaceComposition;
        _isDeployOverviewActive = isDeployOverviewActive;
        _isDeployOnTheFlyActive = isDeployOnTheFlyActive;
        _isDeployFromTemplateActive = isDeployFromTemplateActive;
    }

    public void ResetRightPanelBehavior()
    {
        _fromTemplateWorkspaceComposition.ResetPanelState();
    }

    public bool ShouldAutoOpenRightPanel()
    {
        return _fromTemplateWorkspaceComposition.ShouldAutoOpenResultsPanel ||
            _onTheFlyWorkspaceOwner.ShouldAutoOpenResultsPanel;
    }

    public void ApplyRightPanelState(bool showPanel, bool panelUnavailable)
    {
        _fromTemplateWorkspaceComposition.ApplyResultsPanelState(_isDeployFromTemplateActive(), showPanel, panelUnavailable);
        _onTheFlyWorkspaceOwner.ApplyResultsPanelState(_isDeployOnTheFlyActive(), showPanel, panelUnavailable);
    }

    public string GetRightPanelTitleText()
    {
        return _isDeployFromTemplateActive()
            ? _fromTemplateWorkspaceComposition.ResultsPanelTitle
            : _isDeployOnTheFlyActive()
                ? _onTheFlyWorkspaceOwner.ResultsPanelTitle
                : "Details";
    }

    public bool ShouldShowRightPanelEmptyState(bool showPanel)
    {
        return showPanel && _isDeployOverviewActive() && !_isDeployFromTemplateActive() && !_isDeployOnTheFlyActive();
    }
}
