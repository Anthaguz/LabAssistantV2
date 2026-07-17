using LabAssistant.Services.Logging;

namespace LabAssistant.WinUI.ViewModels.Diagnostics;

/// <summary>
/// A selectable minimum-level option for the structured-log filter. <see cref="Rank"/> is null for the
/// "all levels" option; otherwise it is the lowest projected level the grid should show.
/// </summary>
public sealed class LogLevelFilterOption
{
    public LogLevelFilterOption(string label, StatusLevelRank? rank)
    {
        Label = label;
        Rank = rank;
    }

    /// <summary>The display label shown in the level dropdown.</summary>
    public string Label { get; }

    /// <summary>The minimum level to show, or null to show every level.</summary>
    public StatusLevelRank? Rank { get; }
}
