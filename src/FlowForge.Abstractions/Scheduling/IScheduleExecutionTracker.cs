using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Scheduling;

/// <summary>
/// Tracks claimed schedule occurrences to prevent duplicate local execution.
/// </summary>
public interface IScheduleExecutionTracker
{
    /// <summary>
    /// Gets the latest occurrence tracked for a schedule.
    /// </summary>
    Task<DateTime?> GetLastTrackedOccurrenceAsync(
        WorkflowScheduleId scheduleId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically tracks an occurrence when it has not already been tracked.
    /// </summary>
    Task<bool> TryTrackAsync(
        WorkflowScheduleId scheduleId,
        DateTime occurrenceUtc,
        CancellationToken cancellationToken);
}
