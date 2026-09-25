using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace FlowForge.Api.Contracts;

/// <summary>
/// Represents a workflow definition creation request.
/// </summary>
public sealed record CreateWorkflowDefinitionRequest
{
    public required Guid Id { get; init; }

    public required Guid OwnerTenantId { get; init; }

    [Required]
    public required string Name { get; init; }

    [Required]
    public required string Version { get; init; }

    public string? Description { get; init; }

    public required DateTime CreatedAt { get; init; }

    [Required]
    public required IReadOnlyList<WorkflowNodeDto> Nodes { get; init; }

    [Required]
    public required IReadOnlyList<WorkflowEdgeDto> Edges { get; init; }
}

/// <summary>
/// Represents a workflow definition response.
/// </summary>
public sealed record WorkflowDefinitionDto
{
    public required Guid Id { get; init; }

    public required Guid OwnerTenantId { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public string? Description { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required IReadOnlyList<WorkflowNodeDto> Nodes { get; init; }

    public required IReadOnlyList<WorkflowEdgeDto> Edges { get; init; }
}

/// <summary>
/// Represents a workflow node at the API boundary.
/// </summary>
public sealed record WorkflowNodeDto
{
    public required Guid Id { get; init; }

    [Required]
    public required string Type { get; init; }

    [Required]
    public required IReadOnlyDictionary<string, JsonElement> Configuration { get; init; }
}

/// <summary>
/// Represents a workflow dependency edge at the API boundary.
/// </summary>
public sealed record WorkflowEdgeDto
{
    public required Guid From { get; init; }

    public required Guid To { get; init; }
}
