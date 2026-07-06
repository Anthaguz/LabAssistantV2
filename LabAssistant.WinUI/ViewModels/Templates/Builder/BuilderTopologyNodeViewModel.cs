using CommunityToolkit.Mvvm.Input;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A domain node in the directory-topology tree. Carries the exact chrome inputs the imperative
/// <c>CreateTopologyNodeContent</c> used: an indentation depth (rendered as <c>min(depth,4)*14</c>
/// left margin), the <c>[Root]/[Tree]/[Child]/[Domain]</c>-prefixed label, a relation/subtext line
/// that turns critical when a parent reference is missing, an accent border for selected/root domains,
/// and the selection border thickness (2 selected, 1.5 root domain, 1 otherwise). Immutable; rebuilt
/// per projection.
/// </summary>
public sealed class BuilderTopologyNodeViewModel
{
    public BuilderTopologyNodeViewModel(
        string label,
        string subtext,
        int depth,
        bool isSelected,
        bool isAccent,
        bool hasMissingParent,
        double borderThickness,
        string tooltip,
        IRelayCommand selectCommand)
    {
        Label = label;
        Subtext = subtext;
        Depth = depth;
        IsSelected = isSelected;
        IsAccent = isAccent;
        HasMissingParent = hasMissingParent;
        BorderThickness = borderThickness;
        Tooltip = tooltip;
        SelectCommand = selectCommand;
    }

    public string Label { get; }

    public string Subtext { get; }

    /// <summary>Tree depth; the view converts this to a left indentation margin.</summary>
    public int Depth { get; }

    public bool IsSelected { get; }

    /// <summary>True for selected or root domains; drives the accent border and emphasized label.</summary>
    public bool IsAccent { get; }

    public bool HasMissingParent { get; }

    public double BorderThickness { get; }

    public string Tooltip { get; }

    public IRelayCommand SelectCommand { get; }
}
