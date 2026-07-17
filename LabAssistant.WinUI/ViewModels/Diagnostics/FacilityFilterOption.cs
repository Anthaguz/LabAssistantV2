using CommunityToolkit.Mvvm.ComponentModel;

namespace LabAssistant.WinUI.ViewModels.Diagnostics;

/// <summary>
/// A single facility the user can toggle in the structured-log facility filter. Selection is applied
/// when the user presses Apply, so <see cref="IsSelected"/> only needs to be observable for the checkbox.
/// </summary>
public sealed partial class FacilityFilterOption : ObservableObject
{
    public FacilityFilterOption(byte value, string name, string title)
    {
        Value = value;
        Name = name;
        Title = title;
    }

    /// <summary>The facility byte (bits 16-23 of a status code).</summary>
    public byte Value { get; }

    /// <summary>The stable dotted facility name, for example <c>hyperv</c>.</summary>
    public string Name { get; }

    /// <summary>A human-friendly facility title for display.</summary>
    public string Title { get; }

    /// <summary>The label shown next to the checkbox: the friendly title plus the facility byte.</summary>
    public string Label => $"{Title}  (0x{Value:X2})";

    [ObservableProperty]
    private bool _isSelected;
}
