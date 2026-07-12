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
        IRelayCommand? deleteCommand = null,
        bool isStandalone = false,
        IRelayCommand? manageMachinesCommand = null,
        bool isRootDomain = false)
    {
        NodeId = nodeId;
        IsForest = isForest;
        IsStandalone = isStandalone;
        Label = label;
        Subtext = subtext;
        Tooltip = tooltip;
        IsSelected = isSelected;
        IsAccent = isAccent;
        IsRootDomain = isRootDomain;
        HasMissingParent = hasMissingParent;
        Width = width;
        Height = height;
        _x = x;
        _y = y;
        SelectCommand = selectCommand;
        AddChildCommand = addChildCommand;
        AddTreeCommand = addTreeCommand;
        DeleteCommand = deleteCommand;
        ManageMachinesCommand = manageMachinesCommand;
    }

    public string NodeId { get; }

    /// <summary>True for a forest container, false for a domain. Drives the distinct forest chrome.</summary>
    public bool IsForest { get; }

    /// <summary>
    /// True for the Level 1 Standalone container box. It shares the forest container chrome shape but is
    /// styled gray/dashed to read as "not a directory" - it holds domain-unset machines (router, root CA,
    /// any workgroup box) and, like a domain, zooms into Level 2 when managed.
    /// </summary>
    public bool IsStandalone { get; }

    public string Label { get; }

    public string Subtext { get; }

    public string Tooltip { get; }

    public bool IsSelected { get; }

    /// <summary>
    /// True for a selected node or a root domain. Consumed only by the offline canvas snapshot renderer to draw
    /// an emphasis border; the live XAML does not bind this. In the app the selection border is driven by
    /// <see cref="IsSelected"/> alone and root emphasis by <see cref="IsRootDomain"/> label styling, so a
    /// non-root selection never leaves the root showing a second selection outline.
    /// </summary>
    public bool IsAccent { get; }

    /// <summary>
    /// True when this node is the root domain of its forest. Drives a persistent label emphasis (bold + accent
    /// text) that is independent of selection, so the root reads as special without stealing the selection
    /// outline - selection is shown by the border (<see cref="IsSelected"/>) alone to avoid two blue outlines.
    /// </summary>
    public bool IsRootDomain { get; }

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
    /// buttons (manage machines, add child, delete). Kept on the node so the reveal is per-node without
    /// view-tree scans; it carries no draft meaning and is never persisted.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowManageMachinesAffordance))]
    [NotifyPropertyChangedFor(nameof(ShowAddChildAffordance))]
    [NotifyPropertyChangedFor(nameof(ShowDeleteAffordance))]
    private bool _affordancesRevealed;

    /// <summary>Null when the node cannot be selected (for example the unassigned-domains pseudo forest).</summary>
    public IRelayCommand? SelectCommand { get; }

    /// <summary>Domain hover-+ affordance: create a child domain under this domain. Null on forest nodes.</summary>
    public IRelayCommand? AddChildCommand { get; }

    /// <summary>Forest header +tree affordance: add a tree domain to this forest. Null on domain nodes.</summary>
    public IRelayCommand? AddTreeCommand { get; }

    /// <summary>Delete affordance: remove this forest (whole tree) or this domain subtree. Null when not deletable.</summary>
    public IRelayCommand? DeleteCommand { get; }

    /// <summary>
    /// Manage-machines affordance: zoom into this container's Level 2 machine list. Offered on domain nodes
    /// and the Standalone container; null on forest nodes (a forest has no machines of its own). Bound both to
    /// a hover button and to the node's double-tap so the zoom is discoverable and quick.
    /// </summary>
    public IRelayCommand? ManageMachinesCommand { get; }

    /// <summary>True when the domain hover-+ (add child) affordance should be offered.</summary>
    public bool CanAddChild => AddChildCommand is not null;

    /// <summary>True when the forest +tree affordance should be offered.</summary>
    public bool CanAddTree => AddTreeCommand is not null;

    /// <summary>True when the delete affordance should be offered.</summary>
    public bool CanDelete => DeleteCommand is not null;

    /// <summary>True when the manage-machines (zoom to Level 2) affordance should be offered.</summary>
    public bool CanManageMachines => ManageMachinesCommand is not null;

    /// <summary>Hover-gated visibility for the manage-machines affordance (revealed only while hovered).</summary>
    public bool ShowManageMachinesAffordance => AffordancesRevealed && CanManageMachines;

    /// <summary>Hover-gated visibility for the add-child affordance (revealed only while hovered).</summary>
    public bool ShowAddChildAffordance => AffordancesRevealed && CanAddChild;

    /// <summary>Hover-gated visibility for the delete affordance (revealed only while hovered).</summary>
    public bool ShowDeleteAffordance => AffordancesRevealed && CanDelete;

    public double CenterX => X + (Width / 2);

    public double CenterY => Y + (Height / 2);
}
