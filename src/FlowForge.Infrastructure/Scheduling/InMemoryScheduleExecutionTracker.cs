using System.Collections.Concurrent;
using FlowForge.Abstractions.Scheduling;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Infrastructure.Scheduling;

/// <summary>
/// Tracks schedule occurrences within the current process.
/// </summary>
public sealed class InMemoryScheduleExecutionTracker : IScheduleExecutionTracker
{
    private readonly ConcurrentDictionary<WorkflowScheduleId, DateTime> _occurrences = new();

    /// <inheritdoc />
    public Task<DateTime?> GetLastTrackedOccurrenceAsync(
        WorkflowScheduleId scheduleId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTime? occurrence = _occurrences.TryGetValue(scheduleId, out var tracked)
            ? tracked
            : null;
        return Task.FromResult(occurrence);
    }

    /// <inheritdoc />
    public Task<bool> TryTrackAsync(
        WorkflowScheduleId scheduleId,
        DateTime occurrenceUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while (true)
        {
            if (!_occurrences.TryGetValue(scheduleId, out var current))
            {
                if (_occurrences.TryAdd(scheduleId, occurrenceUtc))
                {
                    return Task.FromResult(true);
                }

                continue;
            }

            if (current >= occurrenceUtc)
            {
                return Task.FromResult(false);
            }

            if (_occurrences.TryUpdate(scheduleId, occurrenceUtc, current))
            {
                return Task.FromResult(true);
            }
        }
    }
}
