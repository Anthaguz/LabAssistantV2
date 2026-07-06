using CommunityToolkit.Mvvm.Input;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A forest container in the directory-topology tree. Renders as a bordered card (accent 2px border
/// when selected) whose header is a selectable nav button when <see cref="CanSelect"/> is true or a
/// plain subhead otherwise, followed by the flattened pre-order list of its domain nodes (or the
/// "No domains in this forest." placeholder). Immutable; rebuilt per projection.
/// </summary>
public sealed class BuilderTopologyForestViewModel
{
    public BuilderTopologyForestViewModel(
        string label,
        bool canSelect,
        bool isSelected,
        IRelayCommand? selectCommand,
        IReadOnlyList<BuilderTopologyNodeViewModel> nodes)
    {
        Label = label;
        CanSelect = canSelect;
        IsSelected = isSelected;
        SelectCommand = selectCommand;
        Nodes = nodes;
    }

    public string Label { get; }

    public bool CanSelect { get; }

    public bool IsSelected { get; }

    public IRelayCommand? SelectCommand { get; }

    public IReadOnlyList<BuilderTopologyNodeViewModel> Nodes { get; }

    public bool HasNodes => Nodes.Count > 0;

    public bool IsEmpty => Nodes.Count == 0;
}
