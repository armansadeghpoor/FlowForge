using FlowForge.Abstractions.Schedules;
using FlowForge.Application.Schedules;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Schedules;

namespace FlowForge.Application.Tests;

public sealed class ScheduleServiceTests
{
    [Fact]
    public async Task CreateAsync_ValidSchedule_IsPersisted()
    {
        var schedule = CreateSchedule();
        var store = new FakeScheduleStore();
        var service = new WorkflowScheduleService(store);

        var result = await service.CreateAsync(schedule, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(schedule, result.Value);
        Assert.Same(schedule, store.SavedSchedule);
    }

    [Fact]
    public async Task CreateAsync_InvalidSchedule_IsRejectedWithoutPersistence()
    {
        var store = new FakeScheduleStore();
        var service = new WorkflowScheduleService(store);
        var schedule = CreateSchedule() with
        {
            Id = default,
            WorkflowTriggerId = default,
            Type = (ScheduleType)999,
            Expression = " ",
            TimeZone = string.Empty,
            CreatedAt = default
        };

        var result = await service.CreateAsync(schedule, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(store.SavedSchedule);
        Assert.Equal(6, result.Errors.Count);
    }

    [Fact]
    public async Task GetAsync_ExistingSchedule_IsReturned()
    {
        var schedule = CreateSchedule();
        var service = new WorkflowScheduleService(
            new FakeScheduleStore { ScheduleToReturn = schedule });

        var result = await service.GetAsync(schedule.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(schedule, result.Value);
    }

    [Fact]
    public async Task CreateAsync_StoreFailure_ReturnsApplicationError()
    {
        var store = new FakeScheduleStore { SaveException = new IOException("provider detail") };
        var service = new WorkflowScheduleService(store);

        var result = await service.CreateAsync(
            CreateSchedule(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("SchedulePersistenceFailed", error.Code);
        Assert.DoesNotContain("provider detail", error.Message);
    }

    private static WorkflowSchedule CreateSchedule() =>
        new()
        {
            Id = new WorkflowScheduleId(Guid.NewGuid()),
            WorkflowTriggerId = new WorkflowTriggerId(Guid.NewGuid()),
            Type = ScheduleType.Cron,
            Expression = "0 0 * * *",
            TimeZone = "UTC",
            Enabled = true,
            CreatedAt = new DateTime(2026, 9, 24, 16, 0, 0, DateTimeKind.Utc)
        };

    private sealed class FakeScheduleStore : IWorkflowScheduleStore
    {
        public WorkflowSchedule? SavedSchedule { get; private set; }

        public WorkflowSchedule? ScheduleToReturn { get; init; }

        public Exception? SaveException { get; init; }

        public Task SaveAsync(
            WorkflowSchedule schedule,
            CancellationToken cancellationToken)
        {
            if (SaveException is not null)
            {
                throw SaveException;
            }

            SavedSchedule = schedule;
            return Task.CompletedTask;
        }

        public Task<WorkflowSchedule?> GetAsync(
            WorkflowScheduleId id,
            CancellationToken cancellationToken) =>
            Task.FromResult(ScheduleToReturn);

        public Task<IReadOnlyList<WorkflowSchedule>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkflowSchedule>>(
                ScheduleToReturn is null ? [] : [ScheduleToReturn]);
    }
}
