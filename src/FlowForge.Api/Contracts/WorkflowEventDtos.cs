using System.Text.Json;

namespace FlowForge.Api.Contracts;

/// <summary>
/// Represents an external event dispatch request.
/// </summary>
public sealed record DispatchWorkflowEventRequest
{
    public required string EventType { get; init; }

    public required JsonElement Payload { get; init; }
}

/// <summary>
/// Represents the outcome of an external event dispatch.
/// </summary>
public sealed record WorkflowEventDispatchDto
{
    public required string EventType { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTime OccurredAt { get; init; }

    public required IReadOnlyList<EventExecutionResultDto> Executions { get; init; }
}

/// <summary>
/// Represents one event-trigger execution result.
/// </summary>
public sealed record EventExecutionResultDto
{
    public required Guid EventTriggerId { get; init; }

    public required Guid TriggerId { get; init; }

    public required bool Success { get; init; }

    public required Guid? WorkflowExecutionId { get; init; }
}
