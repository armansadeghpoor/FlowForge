using System.Text.Json;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Events;

/// <summary>
/// Represents an external event submitted for workflow trigger dispatch.
/// </summary>
public sealed record WorkflowEventContext
{
    /// <summary>
    /// Gets the case-sensitive event type name.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Gets the JSON-compatible event payload.
    /// </summary>
    public required JsonElement Payload { get; init; }

    /// <summary>
    /// Gets the time at which the event occurred.
    /// </summary>
    public required DateTime OccurredAt { get; init; }

    /// <summary>
    /// Gets the event correlation identifier.
    /// </summary>
    public required ExecutionCorrelationId CorrelationId { get; init; }
}
