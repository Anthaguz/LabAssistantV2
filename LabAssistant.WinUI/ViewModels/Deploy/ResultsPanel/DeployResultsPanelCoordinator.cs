namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Coordinates Deploy lane panel intent while leaving shell container sizing, visibility, and open-state ownership in MainWindow.
/// </summary>
internal sealed class DeployResultsPanelCoordinator
{
    private readonly IDeployQuickDeployLane _quickDeployLane;
    private readonly IDeployFromTemplateLane _fromTemplateLane;
    private readonly Func<bool> _isDeployOverviewActive;
    private readonly Func<bool> _isDeployOnTheFlyActive;
    private readonly Func<bool> _isDeployFromTemplateActive;

    public DeployResultsPanelCoordinator(
        IDeployQuickDeployLane quickDeployLane,
        IDeployFromTemplateLane fromTemplateLane,
        Func<bool> isDeployOverviewActive,
        Func<bool> isDeployOnTheFlyActive,
        Func<bool> isDeployFromTemplateActive)
    {
        _quickDeployLane = quickDeployLane;
        _fromTemplateLane = fromTemplateLane;
        _isDeployOverviewActive = isDeployOverviewActive;
        _isDeployOnTheFlyActive = isDeployOnTheFlyActive;
        _isDeployFromTemplateActive = isDeployFromTemplateActive;
    }

    public void ResetRightPanelBehavior()
    {
        _fromTemplateLane.ResetPanelState();
    }

    public bool ShouldAutoOpenRightPanel()
    {
        return _fromTemplateLane.ShouldAutoOpenResultsPanel ||
            _quickDeployLane.ShouldAutoOpenResultsPanel;
    }

    public void ApplyRightPanelState(bool showPanel, bool panelUnavailable)
    {
        _fromTemplateLane.ApplyResultsPanelState(_isDeployFromTemplateActive(), showPanel, panelUnavailable);
        _quickDeployLane.ApplyResultsPanelState(_isDeployOnTheFlyActive(), showPanel, panelUnavailable);
    }

    public string GetRightPanelTitleText()
    {
        return _isDeployFromTemplateActive()
            ? _fromTemplateLane.ResultsPanelTitle
            : _isDeployOnTheFlyActive()
                ? _quickDeployLane.ResultsPanelTitle
                : "Details";
    }

    public bool ShouldShowRightPanelEmptyState(bool showPanel)
    {
        return showPanel && _isDeployOverviewActive() && !_isDeployFromTemplateActive() && !_isDeployOnTheFlyActive();
    }
}
