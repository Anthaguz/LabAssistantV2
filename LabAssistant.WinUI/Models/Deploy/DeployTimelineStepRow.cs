using LabAssistant.WinUI.Theming;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.Models.Deploy;

public sealed record DeployTimelineStepRow(
    string Label,
    DeployTimelineStepState State)
{
    public bool IsRunning => State == DeployTimelineStepState.Running;

    public Visibility RunningIndicatorVisibility => IsRunning ? Visibility.Visible : Visibility.Collapsed;

    public Visibility StaticIconVisibility => IsRunning ? Visibility.Collapsed : Visibility.Visible;

    public string IconGlyph => DeployTimelineIconCatalog.GetGlyph(State);
}
