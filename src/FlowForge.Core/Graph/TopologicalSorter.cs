using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Graph;

/// <summary>
/// Produces deterministic topological execution layers for workflow graphs.
/// </summary>
public static class TopologicalSorter
{
    /// <summary>
    /// Sorts a workflow graph into deterministic execution layers using Kahn's algorithm.
    /// </summary>
    /// <param name="graph">The validated workflow graph to sort.</param>
    /// <returns>Read-only execution layers ordered by dependency.</returns>
    /// <exception cref="InvalidOperationException">The graph contains a directed cycle.</exception>
    public static IReadOnlyList<IReadOnlyList<NodeId>> Sort(WorkflowGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var definitionOrder = graph.NodeIds
            .Select((nodeId, index) => (nodeId, index))
            .ToDictionary(pair => pair.nodeId, pair => pair.index);
        var remainingDependencyCounts = graph.NodeIds.ToDictionary(
            nodeId => nodeId,
            nodeId => graph.GetDependencies(nodeId).Count);
        var currentLayer = graph.NodeIds
            .Where(nodeId => remainingDependencyCounts[nodeId] == 0)
            .ToList();
        var layers = new List<IReadOnlyList<NodeId>>();
        var processedNodeCount = 0;

        while (currentLayer.Count > 0)
        {
            layers.Add(currentLayer.AsReadOnly());
            processedNodeCount += currentLayer.Count;
            var nextLayer = new List<NodeId>();

            foreach (var nodeId in currentLayer)
            {
                foreach (var dependent in graph.GetDependents(nodeId))
                {
                    remainingDependencyCounts[dependent]--;

                    if (remainingDependencyCounts[dependent] == 0)
                    {
                        nextLayer.Add(dependent);
                    }
                }
            }

            nextLayer.Sort((left, right) =>
                definitionOrder[left].CompareTo(definitionOrder[right]));
            currentLayer = nextLayer;
        }

        if (processedNodeCount != graph.NodeIds.Count)
        {
            throw new InvalidOperationException(
                "A topological ordering cannot be produced for a graph containing a directed cycle.");
        }

        return layers.AsReadOnly();
    }
}
