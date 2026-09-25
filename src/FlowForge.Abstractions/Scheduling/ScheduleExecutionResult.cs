using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Scheduling;

/// <summary>
/// Represents the result of dispatching one due schedule occurrence.
/// </summary>
public sealed record ScheduleExecutionResult
{
    /// <summary>
    /// Gets the schedule identifier.
    /// </summary>
    public required WorkflowScheduleId ScheduleId { get; init; }

    /// <summary>
    /// Gets the workflow trigger identifier.
    /// </summary>
    public required WorkflowTriggerId TriggerId { get; init; }

    /// <summary>
    /// Gets the evaluated UTC occurrence.
    /// </summary>
    public required DateTime OccurrenceUtc { get; init; }

    /// <summary>
    /// Gets a value indicating whether trigger execution succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the created workflow execution identifier when successful.
    /// </summary>
    public required WorkflowExecutionId? WorkflowExecutionId { get; init; }
}
