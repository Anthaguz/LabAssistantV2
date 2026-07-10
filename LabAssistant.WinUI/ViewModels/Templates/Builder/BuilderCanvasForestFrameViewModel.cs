using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A forest rendered as an enclosing group frame on the directory-topology canvas: a rounded box auto-sized
/// around the bounding box of its member domain nodes, with a header pill (forest name plus an "add tree"
/// action) straddling its top border. Unlike a domain, a frame is not dragged directly - its
/// <see cref="X"/>/<see cref="Y"/>/<see cref="Width"/>/<see cref="Height"/> are recomputed by the owning
/// canvas view model whenever a member domain moves, so the frame always hugs its domains. In the view the
/// frame body is not hit-testable; only the header pill routes selection and the add-tree / delete
/// affordances, so clicks inside the frame reach the domains it contains. Runtime-independent so its state is
/// unit-testable without a XAML host.
/// </summary>
public sealed partial class BuilderCanvasForestFrameViewModel : ObservableObject
{
    public BuilderCanvasForestFrameViewModel(
        string frameId,
        string label,
        bool isSelected,
        bool canSelect,
        IRelayCommand? selectCommand,
        IRelayCommand? addTreeCommand,
        IRelayCommand? deleteCommand)
    {
        FrameId = frameId;
        Label = label;
        IsSelected = isSelected;
        CanSelect = canSelect;
        SelectCommand = selectCommand;
        AddTreeCommand = addTreeCommand;
        DeleteCommand = deleteCommand;
    }

    public string FrameId { get; }

    public string Label { get; }

    /// <summary>True when this forest is the selected resource; drives the accent border and pill highlight.</summary>
    public bool IsSelected { get; }

    /// <summary>
    /// False for the synthetic "unassigned domains" frame, which only groups orphaned rows for visibility and
    /// offers no selection, add-tree, or delete affordances.
    /// </summary>
    public bool CanSelect { get; }

    /// <summary>Null when the frame cannot be selected (the unassigned-domains pseudo frame).</summary>
    public IRelayCommand? SelectCommand { get; }

    /// <summary>Header +tree affordance: add a tree domain to this forest. Null when not offered.</summary>
    public IRelayCommand? AddTreeCommand { get; }

    /// <summary>Delete affordance: remove this forest and everything in it. Null when not offered.</summary>
    public IRelayCommand? DeleteCommand { get; }

    public bool CanAddTree => AddTreeCommand is not null;

    public bool CanDelete => DeleteCommand is not null;

    /// <summary>Left edge of the frame in canvas coordinates. Recomputed from member domain nodes.</summary>
    [ObservableProperty]
    private double _x;

    /// <summary>Top edge of the frame in canvas coordinates. Recomputed from member domain nodes.</summary>
    [ObservableProperty]
    private double _y;

    /// <summary>Frame width in canvas coordinates. Recomputed from member domain nodes.</summary>
    [ObservableProperty]
    private double _width;

    /// <summary>Frame height in canvas coordinates. Recomputed from member domain nodes.</summary>
    [ObservableProperty]
    private double _height;

    /// <summary>
    /// Presentation-only hover state: true while the pointer is over the header pill, revealing the delete
    /// affordance. Carries no draft meaning and is never persisted.
    /// </summary>
    [ObservableProperty]
    private bool _affordancesRevealed;
}
