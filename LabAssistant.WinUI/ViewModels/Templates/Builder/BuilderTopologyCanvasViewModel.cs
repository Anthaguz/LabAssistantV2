using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// View model for the directory-topology canvas: the draggable node-graph rendering of the Builder's
/// forests, domains, and their edges. It is a view over the existing
/// <see cref="TemplatesBuilderDirectoryTopologyProjector"/> output, not a second source of truth. Node
/// selection routes back through <c>onSelect</c> to the owning Builder view model, exactly as the previous
/// indented list did. Positions are laid out automatically; once the user drags a node it is pinned so a
/// later rebuild (triggered by selection or draft edits) keeps it in place while newly added nodes still
/// auto-layout. Runtime-independent so its layout, edge math, and drag behavior are unit-testable without a
/// XAML host.
/// </summary>
public sealed partial class BuilderTopologyCanvasViewModel : ObservableObject
{
    private const double NodeWidth = 190;
    private const double NodeHeight = 60;
    private const double GapX = 36;
    private const double GapY = 54;
    private const double Margin = 28;
    private const double MinCanvasWidth = 480;
    private const double MinCanvasHeight = 320;

    private readonly Action<BuilderForestDomainResourceKind, int> _onSelect;
    private readonly Action<int>? _onAddChildDomain;
    private readonly Action<int>? _onAddTree;
    private readonly Action<BuilderForestDomainResourceKind, int>? _onDelete;
    private readonly Dictionary<string, BuilderCanvasNodePosition> _pinned = new(StringComparer.Ordinal);
    private Dictionary<string, BuilderCanvasNodeViewModel> _nodeLookup = new(StringComparer.Ordinal);

    internal BuilderTopologyCanvasViewModel(
        TemplatesBuilderDirectoryTopologyProjection projection,
        Action<BuilderForestDomainResourceKind, int> onSelect,
        Action<int>? onAddChildDomain = null,
        Action<int>? onAddTree = null,
        Action<BuilderForestDomainResourceKind, int>? onDelete = null)
    {
        _onSelect = onSelect;
        _onAddChildDomain = onAddChildDomain;
        _onAddTree = onAddTree;
        _onDelete = onDelete;
        BuildFrom(projection);
    }

    [ObservableProperty]
    private ObservableCollection<BuilderCanvasNodeViewModel> _nodes = [];

    [ObservableProperty]
    private ObservableCollection<BuilderCanvasEdgeViewModel> _edges = [];

    [ObservableProperty]
    private double _canvasWidth = MinCanvasWidth;

    [ObservableProperty]
    private double _canvasHeight = MinCanvasHeight;

    [ObservableProperty]
    private bool _hasNodes;

    /// <summary>Rebuilds nodes and edges from a fresh projection, preserving pinned (dragged) positions.</summary>
    internal void Rebuild(TemplatesBuilderDirectoryTopologyProjection projection) => BuildFrom(projection);

    /// <summary>
    /// Moves a node to a new top-left position (clamped to the canvas origin), pins it so later rebuilds do
    /// not move it, recomputes every edge touching a moved node, and grows the canvas extent to fit.
    /// </summary>
    public void MoveNode(string nodeId, double x, double y)
    {
        if (!_nodeLookup.TryGetValue(nodeId, out var node))
        {
            return;
        }

        node.X = Math.Max(0, x);
        node.Y = Math.Max(0, y);
        _pinned[nodeId] = new BuilderCanvasNodePosition(node.X, node.Y);
        RecomputeEdges();
        UpdateExtent();
    }

    private void BuildFrom(TemplatesBuilderDirectoryTopologyProjection projection)
    {
        var autoLayout = BuilderTopologyCanvasLayout.Compute(projection, NodeWidth, NodeHeight, GapX, GapY, Margin, Margin);
        var nodes = new List<BuilderCanvasNodeViewModel>();
        var lookup = new Dictionary<string, BuilderCanvasNodeViewModel>(StringComparer.Ordinal);

        foreach (var forest in projection.Forests)
        {
            var position = ResolvePosition(forest.NodeId, autoLayout);
            var command = forest.CanSelect
                ? new RelayCommand(() => _onSelect(BuilderForestDomainResourceKind.Forest, forest.ForestIndex))
                : null;
            // Real forests offer +tree and delete; the unassigned-domains pseudo forest (CanSelect=false) offers neither.
            var forestIndex = forest.ForestIndex;
            var addTreeCommand = forest.CanSelect && _onAddTree is not null
                ? new RelayCommand(() => _onAddTree(forestIndex))
                : null;
            var deleteForestCommand = forest.CanSelect && _onDelete is not null
                ? new RelayCommand(() => _onDelete(BuilderForestDomainResourceKind.Forest, forestIndex))
                : null;
            var node = new BuilderCanvasNodeViewModel(
                forest.NodeId,
                isForest: true,
                forest.Label,
                subtext: "Forest",
                tooltip: forest.Label,
                forest.IsSelected,
                isAccent: forest.IsSelected,
                hasMissingParent: false,
                NodeWidth,
                NodeHeight,
                position.X,
                position.Y,
                command,
                addChildCommand: null,
                addTreeCommand: addTreeCommand,
                deleteCommand: deleteForestCommand);
            nodes.Add(node);
            lookup[node.NodeId] = node;

            foreach (var root in forest.RootNodes)
            {
                AddDomainNode(root, autoLayout, nodes, lookup);
            }
        }

        var edges = new List<BuilderCanvasEdgeViewModel>();
        foreach (var edge in projection.Edges)
        {
            if (!lookup.TryGetValue(edge.SourceNodeId, out var source) ||
                !lookup.TryGetValue(edge.TargetNodeId, out var target))
            {
                continue;
            }

            var (x1, y1) = BuilderCanvasGeometry.EdgePoint(source.X, source.Y, source.Width, source.Height, target.CenterX, target.CenterY);
            var (x2, y2) = BuilderCanvasGeometry.EdgePoint(target.X, target.Y, target.Width, target.Height, source.CenterX, source.CenterY);
            edges.Add(new BuilderCanvasEdgeViewModel(
                edge.EdgeId,
                edge.SourceNodeId,
                edge.TargetNodeId,
                edge.EdgeKind,
                isForestRoot: string.Equals(edge.EdgeKind, "ForestDomainRoot", StringComparison.Ordinal),
                x1,
                y1,
                x2,
                y2));
        }

        _nodeLookup = lookup;
        Nodes = new ObservableCollection<BuilderCanvasNodeViewModel>(nodes);
        Edges = new ObservableCollection<BuilderCanvasEdgeViewModel>(edges);
        HasNodes = nodes.Count > 0;
        UpdateExtent();
    }

    private void AddDomainNode(
        TemplatesBuilderDomainTopologyNodeProjection domain,
        IReadOnlyDictionary<string, BuilderCanvasNodePosition> autoLayout,
        List<BuilderCanvasNodeViewModel> nodes,
        Dictionary<string, BuilderCanvasNodeViewModel> lookup)
    {
        var position = ResolvePosition(domain.NodeId, autoLayout);
        var subtext = domain.HasMissingParent ? "Missing parent reference" : domain.RelationLabel;
        var domainIndex = domain.DomainIndex;
        var addChildCommand = _onAddChildDomain is not null
            ? new RelayCommand(() => _onAddChildDomain(domainIndex))
            : null;
        var deleteCommand = _onDelete is not null
            ? new RelayCommand(() => _onDelete(BuilderForestDomainResourceKind.Domain, domainIndex))
            : null;
        var node = new BuilderCanvasNodeViewModel(
            domain.NodeId,
            isForest: false,
            domain.Label,
            subtext,
            tooltip: $"{domain.RelationLabel}: {domain.Label}",
            domain.IsSelected,
            isAccent: domain.IsSelected || domain.IsRootDomain,
            domain.HasMissingParent,
            NodeWidth,
            NodeHeight,
            position.X,
            position.Y,
            new RelayCommand(() => _onSelect(BuilderForestDomainResourceKind.Domain, domainIndex)),
            addChildCommand: addChildCommand,
            addTreeCommand: null,
            deleteCommand: deleteCommand);
        nodes.Add(node);
        lookup[node.NodeId] = node;

        foreach (var child in domain.Children)
        {
            AddDomainNode(child, autoLayout, nodes, lookup);
        }
    }

    private BuilderCanvasNodePosition ResolvePosition(
        string nodeId,
        IReadOnlyDictionary<string, BuilderCanvasNodePosition> autoLayout)
    {
        if (_pinned.TryGetValue(nodeId, out var pinned))
        {
            return pinned;
        }

        return autoLayout.TryGetValue(nodeId, out var auto)
            ? auto
            : new BuilderCanvasNodePosition(Margin, Margin);
    }

    private void RecomputeEdges()
    {
        foreach (var edge in Edges)
        {
            if (!_nodeLookup.TryGetValue(edge.SourceNodeId, out var source) ||
                !_nodeLookup.TryGetValue(edge.TargetNodeId, out var target))
            {
                continue;
            }

            var (x1, y1) = BuilderCanvasGeometry.EdgePoint(source.X, source.Y, source.Width, source.Height, target.CenterX, target.CenterY);
            var (x2, y2) = BuilderCanvasGeometry.EdgePoint(target.X, target.Y, target.Width, target.Height, source.CenterX, source.CenterY);
            edge.X1 = x1;
            edge.Y1 = y1;
            edge.X2 = x2;
            edge.Y2 = y2;
        }
    }

    private void UpdateExtent()
    {
        var width = MinCanvasWidth;
        var height = MinCanvasHeight;
        foreach (var node in Nodes)
        {
            width = Math.Max(width, node.X + node.Width + Margin);
            height = Math.Max(height, node.Y + node.Height + Margin);
        }

        CanvasWidth = width;
        CanvasHeight = height;
    }
}
