using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.Theming;

public static class DeployTimelineIconCatalog
{
    private static readonly IReadOnlyDictionary<DeployTimelineStepState, string> GlyphByState =
        new Dictionary<DeployTimelineStepState, string>
        {
            [DeployTimelineStepState.Pending] = "\u25CB",
            [DeployTimelineStepState.Running] = string.Empty,
            [DeployTimelineStepState.Succeeded] = "\u2714",
            [DeployTimelineStepState.Failed] = "\u2716",
            [DeployTimelineStepState.Skipped] = "\u2298"
        };

    public static string GetGlyph(DeployTimelineStepState state)
    {
        return GlyphByState.TryGetValue(state, out var glyph) ? glyph : "\u25CB";
    }
}
