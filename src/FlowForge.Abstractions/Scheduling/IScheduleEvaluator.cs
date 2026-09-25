using FlowForge.Core.Domain.Schedules;

namespace FlowForge.Abstractions.Scheduling;

/// <summary>
/// Evaluates workflow schedule occurrences.
/// </summary>
public interface IScheduleEvaluator
{
    /// <summary>
    /// Gets the first occurrence strictly after the supplied UTC timestamp.
    /// </summary>
    /// <param name="schedule">The schedule to evaluate.</param>
    /// <param name="fromUtc">The exclusive UTC lower bound.</param>
    /// <returns>The next UTC occurrence, or <see langword="null"/> when none remains.</returns>
    DateTime? GetNextOccurrence(
        WorkflowSchedule schedule,
        DateTime fromUtc);
}
