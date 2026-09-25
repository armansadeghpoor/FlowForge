using FlowForge.Abstractions.Schedules;
using FlowForge.Abstractions.Triggers;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Schedules;
using FlowForge.Engine.Scheduling;
using FlowForge.Infrastructure.Scheduling;

namespace FlowForge.Core.Tests.Scheduling;

public sealed class WorkflowSchedulerTests
{
    private static readonly DateTime NowUtc =
        new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RunDueSchedulesAsync_DueSchedule_ExecutesTrigger()
    {
        var schedule = CreateSchedule(
            ScheduleType.OneTime,
            NowUtc.AddMinutes(-1).ToString("O"),
            NowUtc.AddMinutes(-10));
        var executor = new TriggerExecutorStub();
        var scheduler = CreateScheduler(schedule, executor);

        var results = await scheduler.RunDueSchedulesAsync(CancellationToken.None);

        var result = Assert.Single(results);
        Assert.True(result.Success);
        Assert.Equal(executor.ExecutionId, result.WorkflowExecutionId);
        Assert.Equal(schedule.WorkflowTriggerId, executor.Context!.TriggerId);
        Assert.Equal(TriggerType.Timer, executor.Context.TriggerType);
        Assert.NotEqual(default, executor.Context.ExecutionRequestId);
    }

    [Fact]
    public async Task RunDueSchedulesAsync_DisabledSchedule_IsIgnored()
    {
        var schedule = CreateSchedule(
            ScheduleType.OneTime,
            NowUtc.AddMinutes(-1).ToString("O"),
            NowUtc.AddMinutes(-10)) with { Enabled = false };
        var executor = new TriggerExecutorStub();
        var scheduler = CreateScheduler(schedule, executor);

        var results = await scheduler.RunDueSchedulesAsync(CancellationToken.None);

        Assert.Empty(results);
        Assert.Null(executor.Context);
    }

    [Fact]
    public async Task RunDueSchedulesAsync_FutureSchedule_IsIgnored()
    {
        var schedule = CreateSchedule(
            ScheduleType.OneTime,
            NowUtc.AddMinutes(1).ToString("O"),
            NowUtc.AddMinutes(-10));
        var executor = new TriggerExecutorStub();
        var scheduler = CreateScheduler(schedule, executor);

        var results = await scheduler.RunDueSchedulesAsync(CancellationToken.None);

        Assert.Empty(results);
        Assert.Null(executor.Context);
    }

    [Fact]
    public async Task RunDueSchedulesAsync_TriggerFailure_ReturnsFailedResult()
    {
        var schedule = CreateSchedule(
            ScheduleType.OneTime,
            NowUtc.AddMinutes(-1).ToString("O"),
            NowUtc.AddMinutes(-10));
        var executor = new TriggerExecutorStub
        {
            Exception = new InvalidOperationException("trigger failed")
        };
        var scheduler = CreateScheduler(schedule, executor);

        var results = await scheduler.RunDueSchedulesAsync(CancellationToken.None);

        var result = Assert.Single(results);
        Assert.False(result.Success);
        Assert.Null(result.WorkflowExecutionId);
    }

    [Fact]
    public async Task RunDueSchedulesAsync_SameOneTimeOccurrence_ExecutesOnlyOnce()
    {
        var schedule = CreateSchedule(
            ScheduleType.OneTime,
            NowUtc.AddMinutes(-1).ToString("O"),
            NowUtc.AddMinutes(-10));
        var executor = new TriggerExecutorStub();
        var scheduler = CreateScheduler(schedule, executor);

        var first = await scheduler.RunDueSchedulesAsync(CancellationToken.None);
        var second = await scheduler.RunDueSchedulesAsync(CancellationToken.None);

        Assert.Single(first);
        Assert.Empty(second);
        Assert.Equal(1, executor.ExecutionCount);
    }

    private static WorkflowScheduler CreateScheduler(
        WorkflowSchedule schedule,
        TriggerExecutorStub executor) =>
        new(
            new ScheduleStoreStub([schedule]),
            new ScheduleEvaluator(),
            new InMemoryScheduleExecutionTracker(),
            executor,
            new FixedTimeProvider(NowUtc));

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

    private sealed class ScheduleStoreStub(IReadOnlyList<WorkflowSchedule> schedules)
        : IWorkflowScheduleStore
    {
        public Task SaveAsync(WorkflowSchedule schedule, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkflowSchedule?> GetAsync(
            WorkflowScheduleId id,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<WorkflowSchedule>> ListAsync(
            CancellationToken cancellationToken) => Task.FromResult(schedules);
    }

    private sealed class TriggerExecutorStub : IWorkflowTriggerExecutor
    {
        public WorkflowExecutionId ExecutionId { get; } = new(Guid.NewGuid());

        public Exception? Exception { get; init; }

        public WorkflowTriggerExecutionContext? Context { get; private set; }

        public int ExecutionCount { get; private set; }

        public Task<WorkflowExecutionId> ExecuteAsync(
            WorkflowTriggerExecutionContext context,
            CancellationToken cancellationToken)
        {
            Context = context;
            ExecutionCount++;
            return Exception is null
                ? Task.FromResult(ExecutionId)
                : Task.FromException<WorkflowExecutionId>(Exception);
        }
    }

    private sealed class FixedTimeProvider(DateTime nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(nowUtc);
    }
}
