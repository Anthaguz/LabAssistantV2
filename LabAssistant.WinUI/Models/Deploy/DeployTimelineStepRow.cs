namespace LabAssistant.WinUI.Models.Deploy;

public sealed record DeployTimelineStepRow(
    string Label,
    DeployTimelineStepState State)
{
    public bool IsRunning => State == DeployTimelineStepState.Running;
}
