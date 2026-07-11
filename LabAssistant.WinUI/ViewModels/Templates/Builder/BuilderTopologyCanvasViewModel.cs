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
    private readonly Action<BuilderForestDomainResourceKind, int>? _onManageMachines;
    private readonly Dictionary<string, BuilderCanvasNodePosition> _pinned = new(StringComparer.Ordinal);
    private Dictionary<string, BuilderCanvasNodeViewModel> _nodeLookup = new(StringComparer.Ordinal);
    private List<ForestFrameGroup> _frameGroups = [];

    internal BuilderTopologyCanvasViewModel(
        TemplatesBuilderDirectoryTopologyProjection projection,
        Action<BuilderForestDomainResourceKind, int> onSelect,
        Action<int>? onAddChildDomain = null,
        Action<int>? onAddTree = null,
        Action<BuilderForestDomainResourceKind, int>? onDelete = null,
        Action<BuilderForestDomainResourceKind, int>? onManageMachines = null)
    {
        _onSelect = onSelect;
        _onAddChildDomain = onAddChildDomain;
        _onAddTree = onAddTree;
        _onDelete = onDelete;
        _onManageMachines = onManageMachines;
        BuildFrom(projection);
    }

    [ObservableProperty]
    private ObservableCollection<BuilderCanvasNodeViewModel> _nodes = [];

    [ObservableProperty]
    private ObservableCollection<BuilderCanvasEdgeViewModel> _edges = [];

    /// <summary>
    /// The forest frames: enclosing group boxes drawn behind the domain nodes and sized to hug the domains of
    /// each forest. A forest is a frame here, not a node, so it never appears in <see cref="Nodes"/>.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<BuilderCanvasForestFrameViewModel> _frames = [];

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
        RecomputeFrames();
        UpdateExtent();
    }

    private void BuildFrom(TemplatesBuilderDirectoryTopologyProjection projection)
    {
        var autoLayout = BuilderTopologyCanvasLayout.Compute(projection, NodeWidth, NodeHeight, GapX, GapY, Margin, Margin);
        var nodes = new List<BuilderCanvasNodeViewModel>();
        var lookup = new Dictionary<string, BuilderCanvasNodeViewModel>(StringComparer.Ordinal);

        var frameGroups = new List<ForestFrameGroup>();
        foreach (var forest in projection.Forests)
        {
            // Domains are the nodes; add them first so the frame can be sized around their positions.
            foreach (var root in forest.RootNodes)
            {
                AddDomainNode(root, autoLayout, nodes, lookup);
            }

            var memberNodeIds = new List<string>();
            foreach (var root in forest.RootNodes)
            {
                CollectDomainNodeIds(root, memberNodeIds);
            }

            // A forest with no domains has nothing to enclose, so it renders no frame (it reappears the moment
            // it gains a domain).
            if (memberNodeIds.Count == 0)
            {
                continue;
            }

            // Real forests offer selection, +tree, and delete; the unassigned-domains pseudo forest
            // (CanSelect=false) is a grouping frame only and offers none of them.
            var forestIndex = forest.ForestIndex;
            var selectCommand = forest.CanSelect
                ? new RelayCommand(() => _onSelect(BuilderForestDomainResourceKind.Forest, forestIndex))
                : null;
            var addTreeCommand = forest.CanSelect && _onAddTree is not null
                ? new RelayCommand(() => _onAddTree(forestIndex))
                : null;
            var deleteForestCommand = forest.CanSelect && _onDelete is not null
                ? new RelayCommand(() => _onDelete(BuilderForestDomainResourceKind.Forest, forestIndex))
                : null;
            var frame = new BuilderCanvasForestFrameViewModel(
                forest.NodeId,
                forest.Label,
                forest.IsSelected,
                forest.CanSelect,
                selectCommand,
                addTreeCommand,
                deleteForestCommand);
            frameGroups.Add(new ForestFrameGroup(frame, memberNodeIds));
        }

        // The Standalone container is a peer box of the forests. It only exists when the draft has standalone
        // machines (the projector returns null otherwise). It shares the container shape but styles gray/dashed
        // via IsStandalone, selects the Standalone kind, and zooms to its Level 2 machine list when managed.
        if (projection.Standalone is { } standalone)
        {
            var standalonePosition = ResolvePosition(standalone.NodeId, autoLayout);
            var manageStandaloneCommand = _onManageMachines is not null
                ? new RelayCommand(() => _onManageMachines(BuilderForestDomainResourceKind.Standalone, 0))
                : null;
            var machineWord = standalone.MachineCount == 1 ? "machine" : "machines";
            var standaloneNode = new BuilderCanvasNodeViewModel(
                standalone.NodeId,
                isForest: true,
                standalone.Label,
                subtext: $"{standalone.MachineCount} {machineWord}",
                tooltip: "Standalone machines (no domain)",
                standalone.IsSelected,
                isAccent: standalone.IsSelected,
                hasMissingParent: false,
                NodeWidth,
                NodeHeight,
                standalonePosition.X,
                standalonePosition.Y,
                new RelayCommand(() => _onSelect(BuilderForestDomainResourceKind.Standalone, 0)),
                addChildCommand: null,
                addTreeCommand: null,
                deleteCommand: null,
                isStandalone: true,
                manageMachinesCommand: manageStandaloneCommand);
            nodes.Add(standaloneNode);
            lookup[standaloneNode.NodeId] = standaloneNode;
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
        _frameGroups = frameGroups;
        Nodes = new ObservableCollection<BuilderCanvasNodeViewModel>(nodes);
        Edges = new ObservableCollection<BuilderCanvasEdgeViewModel>(edges);
        Frames = new ObservableCollection<BuilderCanvasForestFrameViewModel>(frameGroups.Select(group => group.Frame));
        RecomputeFrames();
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
        var relationText = domain.HasMissingParent ? "Missing parent reference" : domain.RelationLabel;
        // Surface the domain's auto-allocated switch subnet next to the relation so the CIDR is visible on the
        // node without opening the machine list. The subnet is empty until the reconciler has homed the domain.
        var subtext = string.IsNullOrWhiteSpace(domain.Subnet)
            ? relationText
            : $"{relationText} \u00b7 {domain.Subnet}";
        var domainIndex = domain.DomainIndex;
        var addChildCommand = _onAddChildDomain is not null
            ? new RelayCommand(() => _onAddChildDomain(domainIndex))
            : null;
        var deleteCommand = _onDelete is not null
            ? new RelayCommand(() => _onDelete(BuilderForestDomainResourceKind.Domain, domainIndex))
            : null;
        var manageMachinesCommand = _onManageMachines is not null
            ? new RelayCommand(() => _onManageMachines(BuilderForestDomainResourceKind.Domain, domainIndex))
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
            deleteCommand: deleteCommand,
            isStandalone: false,
            manageMachinesCommand: manageMachinesCommand);
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

    /// <summary>
    /// Recomputes every forest frame to hug the current positions of its member domain nodes (plus the frame
    /// padding). Called after a rebuild and after any node drag so a frame always tracks the domains it encloses.
    /// </summary>
    private void RecomputeFrames()
    {
        foreach (var group in _frameGroups)
        {
            if (TryComputeMemberBounds(group.MemberNodeIds, out var minX, out var minY, out var maxX, out var maxY))
            {
                group.Frame.X = minX - BuilderCanvasMetrics.FramePadding;
                group.Frame.Y = minY - BuilderCanvasMetrics.FramePadding;
                group.Frame.Width = (maxX - minX) + (BuilderCanvasMetrics.FramePadding * 2);
                group.Frame.Height = (maxY - minY) + (BuilderCanvasMetrics.FramePadding * 2);
            }
        }
    }

    private bool TryComputeMemberBounds(
        IReadOnlyList<string> memberNodeIds,
        out double minX,
        out double minY,
        out double maxX,
        out double maxY)
    {
        minX = double.PositiveInfinity;
        minY = double.PositiveInfinity;
        maxX = double.NegativeInfinity;
        maxY = double.NegativeInfinity;
        var any = false;
        foreach (var nodeId in memberNodeIds)
        {
            if (!_nodeLookup.TryGetValue(nodeId, out var node))
            {
                continue;
            }

            any = true;
            minX = Math.Min(minX, node.X);
            minY = Math.Min(minY, node.Y);
            maxX = Math.Max(maxX, node.X + node.Width);
            maxY = Math.Max(maxY, node.Y + node.Height);
        }

        return any;
    }

    private static void CollectDomainNodeIds(TemplatesBuilderDomainTopologyNodeProjection domain, List<string> into)
    {
        into.Add(domain.NodeId);
        foreach (var child in domain.Children)
        {
            CollectDomainNodeIds(child, into);
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

        // Frames extend a padding beyond their domains, so include them or a frame's right/bottom edge could
        // fall off the scrollable extent.
        foreach (var frame in Frames)
        {
            width = Math.Max(width, frame.X + frame.Width + Margin);
            height = Math.Max(height, frame.Y + frame.Height + Margin);
        }

        CanvasWidth = width;
        CanvasHeight = height;
    }

    /// <summary>Pairs a forest frame with the ids of the domain nodes it encloses, so it can be re-sized on drag.</summary>
    private readonly record struct ForestFrameGroup(
        BuilderCanvasForestFrameViewModel Frame,
        IReadOnlyList<string> MemberNodeIds);
}
