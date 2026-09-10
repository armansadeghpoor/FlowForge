using System.Diagnostics.CodeAnalysis;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Graph;

/// <summary>
/// Represents the read-only dependency structure of a validated workflow definition.
/// </summary>
public sealed class WorkflowGraph
{
    private readonly IReadOnlyDictionary<NodeId, IReadOnlyList<NodeId>> _dependencies;
    private readonly IReadOnlyDictionary<NodeId, IReadOnlyList<NodeId>> _dependents;

    private WorkflowGraph(WorkflowDefinition workflow)
    {
        var nodeIds = workflow.Nodes.Select(node => node.Id).ToArray();
        NodeIds = Array.AsReadOnly(nodeIds);

        var dependencies = nodeIds.ToDictionary(nodeId => nodeId, _ => new List<NodeId>());
        var dependents = nodeIds.ToDictionary(nodeId => nodeId, _ => new List<NodeId>());

        foreach (var edge in workflow.Edges)
        {
            dependencies[edge.To].Add(edge.From);
            dependents[edge.From].Add(edge.To);
        }

        _dependencies = dependencies.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<NodeId>)pair.Value.AsReadOnly());
        _dependents = dependents.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<NodeId>)pair.Value.AsReadOnly());
    }

    /// <summary>
    /// Gets all graph node identifiers in workflow definition order.
    /// </summary>
    public IReadOnlyList<NodeId> NodeIds { get; }

    /// <summary>
    /// Attempts to create a graph from a workflow definition.
    /// </summary>
    /// <param name="workflow">The workflow definition to validate and represent.</param>
    /// <param name="graph">The created graph when validation succeeds.</param>
    /// <param name="validationResult">The graph validation result.</param>
    /// <returns><see langword="true"/> when the graph was created; otherwise <see langword="false"/>.</returns>
    public static bool TryCreate(
        WorkflowDefinition workflow,
        [NotNullWhen(true)] out WorkflowGraph? graph,
        out GraphValidationResult validationResult)
    {
        validationResult = new WorkflowGraphValidator().Validate(workflow);

        if (!validationResult.IsValid)
        {
            graph = null;
            return false;
        }

        graph = new WorkflowGraph(workflow);
        return true;
    }

    /// <summary>
    /// Gets the direct dependencies of a node.
    /// </summary>
    /// <param name="nodeId">The node identifier.</param>
    /// <returns>The node's direct dependencies.</returns>
    /// <exception cref="KeyNotFoundException">The node does not belong to this graph.</exception>
    public IReadOnlyList<NodeId> GetDependencies(NodeId nodeId) =>
        _dependencies.TryGetValue(nodeId, out var dependencies)
            ? dependencies
            : throw new KeyNotFoundException($"Node '{nodeId.Value}' does not belong to this graph.");

    /// <summary>
    /// Gets the direct dependents of a node.
    /// </summary>
    /// <param name="nodeId">The node identifier.</param>
    /// <returns>The node's direct dependents.</returns>
    /// <exception cref="KeyNotFoundException">The node does not belong to this graph.</exception>
    public IReadOnlyList<NodeId> GetDependents(NodeId nodeId) =>
        _dependents.TryGetValue(nodeId, out var dependents)
            ? dependents
            : throw new KeyNotFoundException($"Node '{nodeId.Value}' does not belong to this graph.");
}
