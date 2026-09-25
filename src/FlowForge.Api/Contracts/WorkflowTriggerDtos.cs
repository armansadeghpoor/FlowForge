using System.Text.Json;

namespace FlowForge.Api.Contracts;

/// <summary>
/// Represents a workflow trigger creation request.
/// </summary>
public sealed record CreateWorkflowTriggerRequest
{
    public required Guid Id { get; init; }

    public required Guid WorkflowDefinitionId { get; init; }

    public required string DefinitionVersion { get; init; }

    public required string Type { get; init; }

    public bool Enabled { get; init; } = true;

    public required IReadOnlyDictionary<string, JsonElement> Configuration { get; init; }
}

/// <summary>
/// Represents a workflow trigger response.
/// </summary>
public sealed record WorkflowTriggerDto
{
    public required Guid Id { get; init; }

    public required Guid WorkflowDefinitionId { get; init; }

    public required string DefinitionVersion { get; init; }

    public required string Type { get; init; }

    public required bool Enabled { get; init; }

    public required IReadOnlyDictionary<string, JsonElement> Configuration { get; init; }
}

/// <summary>
/// Represents a created trigger-based workflow execution.
/// </summary>
public sealed record TriggerExecutionDto
{
    public required Guid WorkflowExecutionId { get; init; }

    public required Guid TriggerId { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTime RequestedAt { get; init; }
}
