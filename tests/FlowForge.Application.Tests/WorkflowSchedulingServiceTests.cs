using FlowForge.Abstractions.Scheduling;
using FlowForge.Application.Scheduling;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Tests;

public sealed class WorkflowSchedulingServiceTests
{
    [Fact]
    public async Task RunDueSchedulesAsync_Success_ReturnsSchedulerResults()
    {
        IReadOnlyList<ScheduleExecutionResult> expected =
        [
            new ScheduleExecutionResult
            {
                ScheduleId = new WorkflowScheduleId(Guid.NewGuid()),
                TriggerId = new WorkflowTriggerId(Guid.NewGuid()),
                OccurrenceUtc = DateTime.UtcNow,
                Success = true,
                WorkflowExecutionId = new WorkflowExecutionId(Guid.NewGuid())
            }
        ];
        var service = new WorkflowSchedulingService(
            new SchedulerStub { Results = expected });

        var result = await service.RunDueSchedulesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task RunDueSchedulesAsync_SchedulerFailure_ReturnsApplicationError()
    {
        var service = new WorkflowSchedulingService(
            new SchedulerStub { Exception = new IOException("provider detail") });

        var result = await service.RunDueSchedulesAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("SchedulingFailed", error.Code);
        Assert.DoesNotContain("provider detail", error.Message);
    }

    private sealed class SchedulerStub : IWorkflowScheduler
    {
        public IReadOnlyList<ScheduleExecutionResult> Results { get; init; } = [];

        public Exception? Exception { get; init; }

        public Task<IReadOnlyList<ScheduleExecutionResult>> RunDueSchedulesAsync(
            CancellationToken cancellationToken) =>
            Exception is null
                ? Task.FromResult(Results)
                : Task.FromException<IReadOnlyList<ScheduleExecutionResult>>(Exception);
    }
}
