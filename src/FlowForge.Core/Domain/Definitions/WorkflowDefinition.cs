using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Domain.Definitions;

/// <summary>
/// Represents the directed acyclic graph definition of a workflow.
/// </summary>
public sealed record WorkflowDefinition
{
    public required WorkflowId Id { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<NodeDefinition> Nodes { get; init; }

    public required IReadOnlyList<EdgeDefinition> Edges { get; init; }
}
