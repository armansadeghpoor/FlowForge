using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Schedules;
using FlowForge.Engine.Scheduling;

namespace FlowForge.Core.Tests.Scheduling;

public sealed class ScheduleEvaluatorTests
{
    [Fact]
    public void GetNextOccurrence_Interval_ReturnsNextBoundary()
    {
        var createdAt = new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);
        var schedule = CreateSchedule(ScheduleType.Interval, "00:05:00", createdAt);

        var occurrence = new ScheduleEvaluator().GetNextOccurrence(
            schedule,
            createdAt.AddMinutes(7));

        Assert.Equal(createdAt.AddMinutes(10), occurrence);
    }

    [Fact]
    public void GetNextOccurrence_FutureOneTime_ReturnsConfiguredTimestamp()
    {
        var createdAt = new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);
        var expected = createdAt.AddHours(1);
        var schedule = CreateSchedule(
            ScheduleType.OneTime,
            expected.ToString("O"),
            createdAt);

        var occurrence = new ScheduleEvaluator().GetNextOccurrence(
            schedule,
            createdAt);

        Assert.Equal(expected, occurrence);
    }

    [Fact]
    public void GetNextOccurrence_InvalidInterval_ThrowsArgumentException()
    {
        var schedule = CreateSchedule(
            ScheduleType.Interval,
            "not-an-interval",
            DateTime.UtcNow);

        Assert.Throws<ArgumentException>(() =>
            new ScheduleEvaluator().GetNextOccurrence(schedule, DateTime.UtcNow));
    }

    private static WorkflowSchedule CreateSchedule(
        ScheduleType type,
        string expression,
        DateTime createdAt) =>
        new()
        {
            Id = new WorkflowScheduleId(Guid.NewGuid()),
            WorkflowTriggerId = new WorkflowTriggerId(Guid.NewGuid()),
            Type = type,
            Expression = expression,
            TimeZone = "UTC",
            Enabled = true,
            CreatedAt = createdAt
        };
}
