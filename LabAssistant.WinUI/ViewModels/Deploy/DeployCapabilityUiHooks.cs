namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Lets lower Deploy seams request capability-level UI refresh without depending on shell container ownership.
/// </summary>
internal sealed class DeployCapabilityUiHooks
{
    private Action _refreshSharedUiState = static () => { };
    private Action _refreshResultsPanelState = static () => { };

    public void AttachSharedUiRefresh(Action refreshSharedUiState)
    {
        _refreshSharedUiState = refreshSharedUiState;
    }

    public void AttachResultsPanelRefresh(Action refreshResultsPanelState)
    {
        _refreshResultsPanelState = refreshResultsPanelState;
    }

    public void RefreshSharedUiState() => _refreshSharedUiState();

    public void RefreshResultsPanelState() => _refreshResultsPanelState();
}
