using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Domain.Definitions;

/// <summary>
/// Represents the directed acyclic graph definition of a workflow.
/// </summary>
public sealed record WorkflowDefinition
{
    public required WorkflowDefinitionId Id { get; init; }

    /// <summary>
    /// Gets the tenant that owns this workflow definition.
    /// </summary>
    public required TenantId OwnerTenantId { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// Gets the immutable version identifier of this workflow definition.
    /// </summary>
    public required string Version { get; init; }

    public required string? Description { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required IReadOnlyList<NodeDefinition> Nodes { get; init; }

    public required IReadOnlyList<EdgeDefinition> Edges { get; init; }
}
