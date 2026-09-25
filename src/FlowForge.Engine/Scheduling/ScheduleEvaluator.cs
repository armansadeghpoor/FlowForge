using System.Globalization;
using FlowForge.Abstractions.Scheduling;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Schedules;

namespace FlowForge.Engine.Scheduling;

/// <summary>
/// Evaluates interval and one-time workflow schedules.
/// </summary>
public sealed class ScheduleEvaluator : IScheduleEvaluator
{
    /// <inheritdoc />
    public DateTime? GetNextOccurrence(
        WorkflowSchedule schedule,
        DateTime fromUtc)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        if (fromUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "The schedule evaluation timestamp must be UTC.",
                nameof(fromUtc));
        }

        if (schedule.CreatedAt == default)
        {
            throw new ArgumentException(
                "The schedule creation timestamp is required.",
                nameof(schedule));
        }

        return schedule.Type switch
        {
            ScheduleType.Interval => GetNextIntervalOccurrence(schedule, fromUtc),
            ScheduleType.OneTime => GetOneTimeOccurrence(schedule, fromUtc),
            ScheduleType.Cron => throw new NotSupportedException(
                "Cron schedule evaluation is not supported without a cron parser."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(schedule),
                schedule.Type,
                "The workflow schedule type is not defined.")
        };
    }

    private static DateTime GetNextIntervalOccurrence(
        WorkflowSchedule schedule,
        DateTime fromUtc)
    {
        if (!TimeSpan.TryParseExact(
                schedule.Expression,
                "c",
                CultureInfo.InvariantCulture,
                out var interval) ||
            interval <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Interval schedules require a positive constant-format TimeSpan expression.",
                nameof(schedule));
        }

        var createdAtUtc = NormalizeUtc(schedule.CreatedAt);
        var firstOccurrence = checked(createdAtUtc + interval);
        if (fromUtc < firstOccurrence)
        {
            return firstOccurrence;
        }

        var elapsedTicks = fromUtc.Ticks - firstOccurrence.Ticks;
        var elapsedIntervals = elapsedTicks / interval.Ticks;
        var nextTicks = checked(
            firstOccurrence.Ticks + ((elapsedIntervals + 1) * interval.Ticks));
        return new DateTime(nextTicks, DateTimeKind.Utc);
    }

    private static DateTime? GetOneTimeOccurrence(
        WorkflowSchedule schedule,
        DateTime fromUtc)
    {
        if (!DateTimeOffset.TryParse(
                schedule.Expression,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            throw new ArgumentException(
                "One-time schedules require a valid timestamp expression.",
                nameof(schedule));
        }

        var occurrenceUtc = parsed.UtcDateTime;
        if (occurrenceUtc < NormalizeUtc(schedule.CreatedAt))
        {
            throw new ArgumentException(
                "A one-time occurrence cannot precede schedule creation.",
                nameof(schedule));
        }

        return occurrenceUtc > fromUtc ? occurrenceUtc : null;
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
