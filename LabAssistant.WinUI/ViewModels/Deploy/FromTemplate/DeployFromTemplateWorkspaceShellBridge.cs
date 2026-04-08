namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Exposes only the shell-owned right-panel interactions that the From Template owner still needs.
/// </summary>
internal sealed class DeployFromTemplateWorkspaceShellBridge
{
    private readonly Action _requestResultsPanelToggle;
    private readonly Action _refreshResultsPanelState;

    public DeployFromTemplateWorkspaceShellBridge(
        Action requestResultsPanelToggle,
        Action refreshResultsPanelState)
    {
        _requestResultsPanelToggle = requestResultsPanelToggle;
        _refreshResultsPanelState = refreshResultsPanelState;
    }

    public void RequestResultsPanelToggle() => _requestResultsPanelToggle();

    public void RefreshResultsPanelState() => _refreshResultsPanelState();
}
