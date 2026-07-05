using LabAssistant.Business.Machines;

namespace LabAssistant.WinUI.ViewModels.Settings;

/// <summary>
/// A selectable Machines deletion-policy option surfaced in the Settings capability. Pairs the
/// backing <see cref="MachineDeletionPolicyMode"/> with the human-readable label shown in the
/// policy selector, so the view binds a single strongly typed option rather than parsing tags.
/// </summary>
public sealed class SettingsDeletionPolicyOption
{
    public SettingsDeletionPolicyOption(MachineDeletionPolicyMode mode, string label)
    {
        Mode = mode;
        Label = label;
    }

    /// <summary>The deletion-policy mode this option applies when saved.</summary>
    public MachineDeletionPolicyMode Mode { get; }

    /// <summary>The label shown for this option in the policy selector.</summary>
    public string Label { get; }
}
