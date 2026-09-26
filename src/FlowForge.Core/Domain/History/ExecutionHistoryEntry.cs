using System.Text.Json;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Domain.History;

/// <summary>
/// Represents an immutable workflow runtime transition history entry.
/// </summary>
public sealed record ExecutionHistoryEntry
{
    public required ExecutionHistoryId Id { get; init; }

    public required WorkflowExecutionId WorkflowExecutionId { get; init; }

    public required NodeExecutionId? NodeExecutionId { get; init; }

    public required ExecutionHistoryEventType EventType { get; init; }

    public required DateTime Timestamp { get; init; }

    public required JsonElement? Metadata { get; init; }
}
