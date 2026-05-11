namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Defines the Quick Deploy lane boundary as seen from Deploy capability-level composition.
/// </summary>
internal interface IDeployQuickDeployLane : IDeployResultsPanelParticipant
{
    event EventHandler? SharedUiStateChanged;

    int DraftCount { get; }

    void ApplyShellState(bool isActive);
}
