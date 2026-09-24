using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Domain.Schedules;

/// <summary>
/// Describes when a workflow trigger may fire in the future.
/// </summary>
public sealed record WorkflowSchedule
{
    /// <summary>
    /// Gets the schedule identifier.
    /// </summary>
    public required WorkflowScheduleId Id { get; init; }

    /// <summary>
    /// Gets the referenced workflow trigger identifier.
    /// </summary>
    public required WorkflowTriggerId WorkflowTriggerId { get; init; }

    /// <summary>
    /// Gets the schedule type.
    /// </summary>
    public required ScheduleType Type { get; init; }

    /// <summary>
    /// Gets the opaque schedule expression.
    /// </summary>
    public required string Expression { get; init; }

    /// <summary>
    /// Gets the schedule time-zone identifier.
    /// </summary>
    public required string TimeZone { get; init; }

    /// <summary>
    /// Gets a value indicating whether the schedule is enabled.
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// Gets the schedule creation timestamp.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}
