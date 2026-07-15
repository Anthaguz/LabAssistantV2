using CommunityToolkit.Mvvm.ComponentModel;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A drawn connector between two topology nodes. Endpoints (<see cref="X1"/>/<see cref="Y1"/> to
/// <see cref="X2"/>/<see cref="Y2"/>) are recomputed in place when either endpoint node moves, so the
/// line follows a drag without rebuilding the collection. <see cref="IsForestRoot"/> distinguishes the
/// forest-to-root-domain link from a parent-child domain link for styling. Runtime-independent.
/// </summary>
public sealed partial class BuilderCanvasEdgeViewModel : ObservableObject
{
    public BuilderCanvasEdgeViewModel(
        string edgeId,
        string sourceNodeId,
        string targetNodeId,
        string edgeKind,
        bool isForestRoot,
        double x1,
        double y1,
        double x2,
        double y2)
    {
        EdgeId = edgeId;
        SourceNodeId = sourceNodeId;
        TargetNodeId = targetNodeId;
        EdgeKind = edgeKind;
        IsForestRoot = isForestRoot;
        _x1 = x1;
        _y1 = y1;
        _x2 = x2;
        _y2 = y2;
    }

    public string EdgeId { get; }

    public string SourceNodeId { get; }

    public string TargetNodeId { get; }

    public string EdgeKind { get; }

    /// <summary>True for a forest-to-root-domain edge; false for a parent-child domain edge.</summary>
    public bool IsForestRoot { get; }

    [ObservableProperty]
    private double _x1;

    [ObservableProperty]
    private double _y1;

    [ObservableProperty]
    private double _x2;

    [ObservableProperty]
    private double _y2;
}
