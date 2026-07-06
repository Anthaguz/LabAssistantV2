using CommunityToolkit.Mvvm.Input;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A chrome button (or inline placeholder text) in the Builder's left navigator, resource lists,
/// deployment profile selector, or a forest header. Reproduces the selection chrome the imperative
/// <c>CreateNavButtonContent</c> rendered: selected rows carry the accent border/background and a
/// semibold accent label. Placeholder rows render as plain secondary text (the former
/// "No VMs in this draft." style empty markers). Rows are immutable and rebuilt on each projection,
/// so the bindings are one-time.
/// </summary>
public sealed class BuilderNavRowViewModel
{
    private BuilderNavRowViewModel(string label, bool isSelected, bool isEnabled, bool isPlaceholder, string tooltip, IRelayCommand? command)
    {
        Label = label;
        IsSelected = isSelected;
        IsEnabled = isEnabled;
        IsPlaceholder = isPlaceholder;
        Tooltip = tooltip;
        Command = command;
    }

    public string Label { get; }

    public bool IsSelected { get; }

    public bool IsEnabled { get; }

    /// <summary>When true the row renders as plain secondary text instead of a selectable button.</summary>
    public bool IsPlaceholder { get; }

    public string Tooltip { get; }

    public IRelayCommand? Command { get; }

    public static BuilderNavRowViewModel Button(string label, bool isSelected, bool isEnabled, IRelayCommand command)
        => new(label, isSelected, isEnabled, isPlaceholder: false, label, command);

    public static BuilderNavRowViewModel Button(string label, bool isSelected, bool isEnabled, IRelayCommand command, string tooltip)
        => new(label, isSelected, isEnabled, isPlaceholder: false, tooltip, command);

    public static BuilderNavRowViewModel Placeholder(string label)
        => new(label, isSelected: false, isEnabled: false, isPlaceholder: true, label, command: null);
}
