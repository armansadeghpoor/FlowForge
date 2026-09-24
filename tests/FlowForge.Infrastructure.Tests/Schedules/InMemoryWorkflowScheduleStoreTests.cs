using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Schedules;
using FlowForge.Infrastructure.Schedules;

namespace FlowForge.Infrastructure.Tests.Schedules;

public sealed class InMemoryWorkflowScheduleStoreTests
{
    [Fact]
    public async Task SaveAsync_Schedule_CanBeRetrieved()
    {
        var store = new InMemoryWorkflowScheduleStore();
        var schedule = CreateSchedule();

        await store.SaveAsync(schedule, CancellationToken.None);
        var retrieved = await store.GetAsync(schedule.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(schedule, retrieved);
        Assert.NotSame(schedule, retrieved);
    }

    [Fact]
    public async Task SaveAsync_MultipleSchedules_Coexist()
    {
        var store = new InMemoryWorkflowScheduleStore();
        var triggerId = new WorkflowTriggerId(Guid.NewGuid());
        var cron = CreateSchedule(triggerId, ScheduleType.Cron, "0 0 * * *");
        var interval = CreateSchedule(triggerId, ScheduleType.Interval, "00:05:00");
        var oneTime = CreateSchedule(
            triggerId,
            ScheduleType.OneTime,
            "2026-10-01T09:00:00Z");

        await Task.WhenAll(
            store.SaveAsync(cron, CancellationToken.None),
            store.SaveAsync(interval, CancellationToken.None),
            store.SaveAsync(oneTime, CancellationToken.None));
        var schedules = await store.ListAsync(CancellationToken.None);

        Assert.Equal(3, schedules.Count);
        Assert.Contains(schedules, schedule => schedule.Id == cron.Id);
        Assert.Contains(schedules, schedule => schedule.Id == interval.Id);
        Assert.Contains(schedules, schedule => schedule.Id == oneTime.Id);
    }

    [Fact]
    public async Task SaveAsync_IsolatesStoredAndReturnedSnapshots()
    {
        var store = new InMemoryWorkflowScheduleStore();
        var schedule = CreateSchedule();
        await store.SaveAsync(schedule, CancellationToken.None);

        var firstRead = await store.GetAsync(schedule.Id, CancellationToken.None);
        var secondRead = await store.GetAsync(schedule.Id, CancellationToken.None);

        Assert.NotNull(firstRead);
        Assert.NotNull(secondRead);
        Assert.Equal(schedule, firstRead);
        Assert.NotSame(schedule, firstRead);
        Assert.NotSame(firstRead, secondRead);
    }

    [Fact]
    public async Task SaveAsync_InvalidScheduleMetadata_IsRejected()
    {
        var store = new InMemoryWorkflowScheduleStore();
        var schedule = CreateSchedule();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(schedule with { Id = default }, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(
                schedule with { WorkflowTriggerId = default },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.SaveAsync(
                schedule with { Type = (ScheduleType)999 },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(
                schedule with { Expression = " " },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(
                schedule with { TimeZone = string.Empty },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(
                schedule with { CreatedAt = default },
                CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_PreservesTriggerReference()
    {
        var store = new InMemoryWorkflowScheduleStore();
        var triggerId = new WorkflowTriggerId(Guid.NewGuid());
        var schedule = CreateSchedule(triggerId);

        await store.SaveAsync(schedule, CancellationToken.None);
        var retrieved = await store.GetAsync(schedule.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(triggerId, retrieved.WorkflowTriggerId);
    }

    [Fact]
    public async Task SaveAsync_DuplicateScheduleId_IsRejected()
    {
        var store = new InMemoryWorkflowScheduleStore();
        var schedule = CreateSchedule();
        await store.SaveAsync(schedule, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveAsync(schedule, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_MissingSchedule_ReturnsNull()
    {
        var store = new InMemoryWorkflowScheduleStore();

        var schedule = await store.GetAsync(
            new WorkflowScheduleId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(schedule);
    }

    private static WorkflowSchedule CreateSchedule(
        WorkflowTriggerId? triggerId = null,
        ScheduleType type = ScheduleType.Cron,
        string expression = "0 0 * * *") =>
        new()
        {
            Id = new WorkflowScheduleId(Guid.NewGuid()),
            WorkflowTriggerId = triggerId ?? new WorkflowTriggerId(Guid.NewGuid()),
            Type = type,
            Expression = expression,
            TimeZone = "UTC",
            Enabled = true,
            CreatedAt = new DateTime(2026, 9, 24, 13, 0, 0, DateTimeKind.Utc)
        };
}
