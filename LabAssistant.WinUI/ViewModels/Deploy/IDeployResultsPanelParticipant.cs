namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Represents the narrow lane-facing panel intent surface that shared Deploy panel coordination may consume.
/// </summary>
internal interface IDeployResultsPanelParticipant
{
    bool ShouldAutoOpenResultsPanel { get; }

    string ResultsPanelTitle { get; }

    void ApplyResultsPanelState(bool isActive, bool showPanel, bool panelUnavailable);
}
