using System.Text.Json;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Domain.Triggers;

/// <summary>
/// Describes an external event that may start a workflow trigger in the future.
/// </summary>
public sealed record WorkflowEventTrigger
{
    /// <summary>
    /// Gets the event trigger identifier.
    /// </summary>
    public required WorkflowEventTriggerId Id { get; init; }

    /// <summary>
    /// Gets the referenced workflow trigger identifier.
    /// </summary>
    public required WorkflowTriggerId WorkflowTriggerId { get; init; }

    /// <summary>
    /// Gets the case-sensitive event type name.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Gets optional JSON-compatible event filter metadata.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? Filter { get; init; }

    /// <summary>
    /// Gets a value indicating whether the event trigger is enabled.
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// Gets the event trigger creation timestamp.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}
