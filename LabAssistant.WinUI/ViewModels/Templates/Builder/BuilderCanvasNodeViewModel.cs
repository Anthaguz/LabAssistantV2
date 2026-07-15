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
        IRelayCommand? selectCommand,
        IRelayCommand? addChildCommand = null,
        IRelayCommand? addTreeCommand = null,
        IRelayCommand? deleteCommand = null)
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
        AddChildCommand = addChildCommand;
        AddTreeCommand = addTreeCommand;
        DeleteCommand = deleteCommand;
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

    /// <summary>
    /// Presentation-only hover state: true while the pointer is over the node, revealing its affordance
    /// buttons (hover-+, +tree, delete). Kept on the node so the reveal is per-node without view-tree scans;
    /// it carries no draft meaning and is never persisted.
    /// </summary>
    [ObservableProperty]
    private bool _affordancesRevealed;

    /// <summary>Null when the node cannot be selected (for example the unassigned-domains pseudo forest).</summary>
    public IRelayCommand? SelectCommand { get; }

    /// <summary>Domain hover-+ affordance: create a child domain under this domain. Null on forest nodes.</summary>
    public IRelayCommand? AddChildCommand { get; }

    /// <summary>Forest header +tree affordance: add a tree domain to this forest. Null on domain nodes.</summary>
    public IRelayCommand? AddTreeCommand { get; }

    /// <summary>Delete affordance: remove this forest (whole tree) or this domain subtree. Null when not deletable.</summary>
    public IRelayCommand? DeleteCommand { get; }

    /// <summary>True when the domain hover-+ (add child) affordance should be offered.</summary>
    public bool CanAddChild => AddChildCommand is not null;

    /// <summary>True when the forest +tree affordance should be offered.</summary>
    public bool CanAddTree => AddTreeCommand is not null;

    /// <summary>True when the delete affordance should be offered.</summary>
    public bool CanDelete => DeleteCommand is not null;

    public double CenterX => X + (Width / 2);

    public double CenterY => Y + (Height / 2);
}
