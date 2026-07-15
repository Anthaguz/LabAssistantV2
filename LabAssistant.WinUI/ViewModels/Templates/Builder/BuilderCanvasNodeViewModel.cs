using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A positioned node on the directory-topology canvas: a forest container or a domain. Immutable chrome
/// (label, relation subtext, selection accents) plus a mutable <see cref="X"/>/<see cref="Y"/> top-left
/// position the view binds to <c>Canvas.Left</c>/<c>Canvas.Top</c>. Position is mutated in place while the
/// user drags so the node moves without rebuilding the collection. Purely runtime-independent so it can be
/// unit-tested without a XAML host.
/// </summary>
public sealed partial class BuilderCanvasNodeViewModel : ObservableObject
{
    public BuilderCanvasNodeViewModel(
        string nodeId,
        bool isForest,
        string label,
        string subtext,
        string tooltip,
        bool isSelected,
        bool isAccent,
        bool hasMissingParent,
        double width,
        double height,
        double x,
        double y,
        IRelayCommand? selectCommand)
    {
        NodeId = nodeId;
        IsForest = isForest;
        Label = label;
        Subtext = subtext;
        Tooltip = tooltip;
        IsSelected = isSelected;
        IsAccent = isAccent;
        HasMissingParent = hasMissingParent;
        Width = width;
        Height = height;
        _x = x;
        _y = y;
        SelectCommand = selectCommand;
    }

    public string NodeId { get; }

    /// <summary>True for a forest container, false for a domain. Drives the distinct forest chrome.</summary>
    public bool IsForest { get; }

    public string Label { get; }

    public string Subtext { get; }

    public string Tooltip { get; }

    public bool IsSelected { get; }

    /// <summary>True for a selected node or a root domain; drives the accent border and emphasized label.</summary>
    public bool IsAccent { get; }

    public bool HasMissingParent { get; }

    public double Width { get; }

    public double Height { get; }

    /// <summary>Left edge of the node in canvas coordinates. Mutated live while dragging.</summary>
    [ObservableProperty]
    private double _x;

    /// <summary>Top edge of the node in canvas coordinates. Mutated live while dragging.</summary>
    [ObservableProperty]
    private double _y;

    /// <summary>Null when the node cannot be selected (for example the unassigned-domains pseudo forest).</summary>
    public IRelayCommand? SelectCommand { get; }

    public double CenterX => X + (Width / 2);

    public double CenterY => Y + (Height / 2);
}
