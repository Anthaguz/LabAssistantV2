using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// Immutable adjacency view of a <see cref="V2PlanBuildResult"/> used by the scheduler, plus fail-closed validation.
/// An edge <c>From -&gt; To</c> means "To depends on From": To becomes ready only once From has completed.
/// </summary>
internal sealed class V2PlanGraph
{
    private readonly IReadOnlyDictionary<string, V2PlanNode> _nodesById;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _successors;
    private readonly IReadOnlyDictionary<string, int> _inDegree;
    private readonly IReadOnlyList<string> _edgeErrors;

    private V2PlanGraph(
        IReadOnlyList<V2PlanNode> nodes,
        IReadOnlyDictionary<string, V2PlanNode> nodesById,
        IReadOnlyDictionary<string, IReadOnlyList<string>> successors,
        IReadOnlyDictionary<string, int> inDegree,
        IReadOnlyList<string> edgeErrors)
    {
        Nodes = nodes;
        _nodesById = nodesById;
        _successors = successors;
        _inDegree = inDegree;
        _edgeErrors = edgeErrors;
    }

    /// <summary>All nodes in the plan, in planner order.</summary>
    public IReadOnlyList<V2PlanNode> Nodes { get; }

    /// <summary>Builds the graph from a plan, deduplicating edges that repeat with different reason codes.</summary>
    public static V2PlanGraph Build(V2PlanBuildResult plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var nodes = plan.Nodes.ToList();
        var nodesById = new Dictionary<string, V2PlanNode>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            nodesById[node.NodeId] = node;
        }

        var successors = nodes.ToDictionary(node => node.NodeId, _ => new List<string>(), StringComparer.Ordinal);
        var inDegree = nodes.ToDictionary(node => node.NodeId, _ => 0, StringComparer.Ordinal);
        var edgeErrors = new List<string>();
        var seenEdges = new HashSet<(string From, string To)>();

        foreach (var dependency in plan.Dependencies)
        {
            if (!nodesById.ContainsKey(dependency.FromNodeId) || !nodesById.ContainsKey(dependency.ToNodeId))
            {
                edgeErrors.Add(
                    $"Dependency references unknown node id(s): from '{dependency.FromNodeId}' to '{dependency.ToNodeId}'.");
                continue;
            }

            if (string.Equals(dependency.FromNodeId, dependency.ToNodeId, StringComparison.Ordinal))
            {
                edgeErrors.Add($"Node '{dependency.FromNodeId}' declares a self-dependency.");
                continue;
            }

            if (!seenEdges.Add((dependency.FromNodeId, dependency.ToNodeId)))
            {
                continue;
            }

            successors[dependency.FromNodeId].Add(dependency.ToNodeId);
            inDegree[dependency.ToNodeId] += 1;
        }

        var frozenSuccessors = successors.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.Ordinal);

        return new V2PlanGraph(nodes, nodesById, frozenSuccessors, inDegree, edgeErrors);
    }

    /// <summary>Nodes that depend on <paramref name="nodeId"/> (its outgoing edges).</summary>
    public IReadOnlyList<string> Successors(string nodeId) =>
        _successors.TryGetValue(nodeId, out var list) ? list : Array.Empty<string>();

    /// <summary>Number of dependencies <paramref name="nodeId"/> is waiting on.</summary>
    public int InDegree(string nodeId) => _inDegree.TryGetValue(nodeId, out var value) ? value : 0;

    /// <summary>Returns the node for <paramref name="nodeId"/>.</summary>
    public V2PlanNode NodeFor(string nodeId) => _nodesById[nodeId];

    /// <summary>
    /// Fail-closed validation. Aggregates every problem and throws <see cref="V2SchedulerValidationException"/> when any
    /// is found: dangling edges, self-dependencies, an unregistered node kind, a dependency cycle, or a node unreachable
    /// from any entry (in-degree zero) node.
    /// </summary>
    public void Validate(IV2NodeExecutorRegistry executors)
    {
        ArgumentNullException.ThrowIfNull(executors);

        var errors = new List<string>(_edgeErrors);

        foreach (var kind in Nodes.Select(node => node.Kind).Distinct())
        {
            if (!executors.IsRegistered(kind))
            {
                errors.Add($"No executor is registered for node kind '{kind}'.");
            }
        }

        if (HasCycle(out var cycleNode))
        {
            errors.Add($"The plan graph contains a dependency cycle involving node '{cycleNode}'.");
        }
        else
        {
            foreach (var unreachable in FindUnreachableFromEntries())
            {
                errors.Add($"Node '{unreachable}' is not reachable from any entry node.");
            }
        }

        if (errors.Count > 0)
        {
            throw new V2SchedulerValidationException(errors);
        }
    }

    private bool HasCycle(out string? offendingNode)
    {
        // Kahn's algorithm: if we cannot topologically drain every node, the remainder forms at least one cycle.
        var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var node in Nodes)
        {
            remaining[node.NodeId] = InDegree(node.NodeId);
        }

        var queue = new Queue<string>(remaining.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var processed = 0;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            processed++;
            foreach (var successor in Successors(current))
            {
                if (--remaining[successor] == 0)
                {
                    queue.Enqueue(successor);
                }
            }
        }

        if (processed == Nodes.Count)
        {
            offendingNode = null;
            return false;
        }

        offendingNode = remaining
            .Where(pair => pair.Value > 0)
            .Select(pair => pair.Key)
            .OrderBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault();
        return true;
    }

    private IReadOnlyList<string> FindUnreachableFromEntries()
    {
        var entries = Nodes.Where(node => InDegree(node.NodeId) == 0).Select(node => node.NodeId);
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>(entries);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!reachable.Add(current))
            {
                continue;
            }

            foreach (var successor in Successors(current))
            {
                stack.Push(successor);
            }
        }

        return Nodes
            .Where(node => !reachable.Contains(node.NodeId))
            .Select(node => node.NodeId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }
}
