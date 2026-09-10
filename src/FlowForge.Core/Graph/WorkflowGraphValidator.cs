using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Graph;

/// <summary>
/// Validates the structural integrity of workflow graph definitions.
/// </summary>
public sealed class WorkflowGraphValidator
{
    /// <summary>
    /// Validates a workflow definition as a directed acyclic graph.
    /// </summary>
    /// <param name="workflow">The workflow definition to validate.</param>
    /// <returns>The graph validation result.</returns>
    public GraphValidationResult Validate(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var errors = new List<GraphValidationError>();
        var nodeIds = new HashSet<NodeId>();
        var duplicateNodeIds = new HashSet<NodeId>();

        foreach (var node in workflow.Nodes)
        {
            if (!nodeIds.Add(node.Id))
            {
                duplicateNodeIds.Add(node.Id);
            }
        }

        if (nodeIds.Count == 0)
        {
            errors.Add(new GraphValidationError(
                GraphValidationErrorType.EmptyWorkflow,
                "A workflow must contain at least one node."));
        }

        foreach (var duplicateNodeId in duplicateNodeIds)
        {
            errors.Add(new GraphValidationError(
                GraphValidationErrorType.DuplicateNodeId,
                $"Node '{duplicateNodeId.Value}' is defined more than once.",
                NodeId: duplicateNodeId));
        }

        var uniqueEdges = new HashSet<(NodeId From, NodeId To)>();
        var graphEdges = new List<(NodeId From, NodeId To)>();

        foreach (var edge in workflow.Edges)
        {
            var fromExists = nodeIds.Contains(edge.From);
            var toExists = nodeIds.Contains(edge.To);

            if (!fromExists)
            {
                errors.Add(new GraphValidationError(
                    GraphValidationErrorType.MissingFromNode,
                    $"Edge source node '{edge.From.Value}' does not exist.",
                    From: edge.From,
                    To: edge.To));
            }

            if (!toExists)
            {
                errors.Add(new GraphValidationError(
                    GraphValidationErrorType.MissingToNode,
                    $"Edge destination node '{edge.To.Value}' does not exist.",
                    From: edge.From,
                    To: edge.To));
            }

            var isUniqueEdge = uniqueEdges.Add((edge.From, edge.To));

            if (!isUniqueEdge)
            {
                errors.Add(new GraphValidationError(
                    GraphValidationErrorType.DuplicateEdge,
                    $"Edge '{edge.From.Value}' to '{edge.To.Value}' is defined more than once.",
                    From: edge.From,
                    To: edge.To));
            }

            if (edge.From == edge.To)
            {
                errors.Add(new GraphValidationError(
                    GraphValidationErrorType.SelfReferencingEdge,
                    $"Node '{edge.From.Value}' cannot depend on itself.",
                    NodeId: edge.From,
                    From: edge.From,
                    To: edge.To));
            }

            if (fromExists && toExists && edge.From != edge.To && isUniqueEdge)
            {
                graphEdges.Add((edge.From, edge.To));
            }
        }

        if (ContainsCycle(nodeIds, graphEdges))
        {
            errors.Add(new GraphValidationError(
                GraphValidationErrorType.CycleDetected,
                "The workflow contains a directed cycle."));
        }

        return new GraphValidationResult(errors);
    }

    private static bool ContainsCycle(
        IReadOnlySet<NodeId> nodeIds,
        IEnumerable<(NodeId From, NodeId To)> edges)
    {
        var dependents = nodeIds.ToDictionary(nodeId => nodeId, _ => new List<NodeId>());
        var incomingEdgeCounts = nodeIds.ToDictionary(nodeId => nodeId, _ => 0);

        foreach (var (from, to) in edges.Distinct())
        {
            dependents[from].Add(to);
            incomingEdgeCounts[to]++;
        }

        var nodesWithoutDependencies = new Queue<NodeId>(
            nodeIds.Where(nodeId => incomingEdgeCounts[nodeId] == 0));
        var processedNodeCount = 0;

        while (nodesWithoutDependencies.TryDequeue(out var nodeId))
        {
            processedNodeCount++;

            foreach (var dependent in dependents[nodeId])
            {
                incomingEdgeCounts[dependent]--;

                if (incomingEdgeCounts[dependent] == 0)
                {
                    nodesWithoutDependencies.Enqueue(dependent);
                }
            }
        }

        return processedNodeCount != nodeIds.Count;
    }
}
