using LabAssistant.Business.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Owns the long-lived Deploy runtime boundary above shared Deploy composition and lane-local seams
/// while leaving shell routing and shell panel infrastructure in MainWindow.
/// </summary>
internal sealed class DeployCapabilityRuntime
{
    private readonly DeployCapabilityShellBridge _shellBridge;
    private readonly IDeployFromTemplateLane _fromTemplateLane;
    private readonly DeployWorkspaceComposition _workspaceComposition;
    private readonly DeployResultsPanelCoordinator _resultsPanelCoordinator;

    public DeployCapabilityRuntime(
        DeployCapabilityShellBridge shellBridge,
        IDeployFromTemplateLane fromTemplateLane,
        DeployWorkspaceComposition workspaceComposition,
        DeployResultsPanelCoordinator resultsPanelCoordinator)
    {
        _shellBridge = shellBridge;
        _fromTemplateLane = fromTemplateLane;
        _workspaceComposition = workspaceComposition;
        _resultsPanelCoordinator = resultsPanelCoordinator;
    }

    public void ApplyShellState()
    {
        _workspaceComposition.ApplyShellState();
        EnsureFromTemplateLaneLoadedForActiveRoute();
    }

    public void ResetRightPanelBehavior() => _resultsPanelCoordinator.ResetRightPanelBehavior();

    public bool ShouldAutoOpenRightPanel() => _resultsPanelCoordinator.ShouldAutoOpenRightPanel();

    public string GetRightPanelTitleText() => _resultsPanelCoordinator.GetRightPanelTitleText();

    public bool ShouldShowRightPanelEmptyState(bool showPanel) => _resultsPanelCoordinator.ShouldShowRightPanelEmptyState(showPanel);

    public void ApplyRightPanelState(bool showPanel, bool panelUnavailable)
    {
        _resultsPanelCoordinator.ApplyRightPanelState(showPanel, panelUnavailable);
    }

    public void RefreshTemplatesLoadingState()
    {
        _fromTemplateLane.RefreshUi();
    }

    public void ReconcileTemplateSelection(IReadOnlyList<TemplateLibraryItem> items)
    {
        _fromTemplateLane.ReconcileSelection(items);
        _workspaceComposition.RefreshSharedUiState();
    }

    private void EnsureFromTemplateLaneLoadedForActiveRoute()
    {
        if (_shellBridge.IsDeployFromTemplateActive)
        {
            _ = _fromTemplateLane.EnsureTemplatesLoadedAsync(forceRefresh: false);
        }
    }
}
