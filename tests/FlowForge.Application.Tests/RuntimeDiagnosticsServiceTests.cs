using FlowForge.Abstractions.Queries;
using FlowForge.Application.Diagnostics;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.History;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Tests;

public sealed class RuntimeDiagnosticsServiceTests
{
    [Fact]
    public async Task GetExecutionMetricsAsync_EmptySystem_ReturnsZeroSnapshot()
    {
        var service = CreateService([]);

        var result = await service.GetExecutionMetricsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.TotalExecutions);
        Assert.Equal(0, result.Value.RunningExecutions);
        Assert.Equal(0, result.Value.CompletedExecutions);
        Assert.Equal(0, result.Value.FailedExecutions);
        Assert.Null(result.Value.AverageDuration);
        Assert.Null(result.Value.LastExecutionTimestamp);
    }

    [Fact]
    public async Task GetExecutionMetricsAsync_CompletedExecutions_CalculatesDurationAndLatestStart()
    {
        var firstStart = Timestamp;
        var secondStart = Timestamp.AddMinutes(30);
        var service = CreateService(
        [
            CreateSummary(
                WorkflowExecutionStatus.Succeeded,
                firstStart,
                firstStart.AddMinutes(10)),
            CreateSummary(
                WorkflowExecutionStatus.Succeeded,
                secondStart,
                secondStart.AddMinutes(20))
        ]);

        var result = await service.GetExecutionMetricsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalExecutions);
        Assert.Equal(2, result.Value.CompletedExecutions);
        Assert.Equal(TimeSpan.FromMinutes(15), result.Value.AverageDuration);
        Assert.Equal(secondStart, result.Value.LastExecutionTimestamp);
    }

    [Fact]
    public async Task GetExecutionMetricsAsync_FailedExecution_CountsFailure()
    {
        var service = CreateService(
        [
            CreateSummary(
                WorkflowExecutionStatus.Failed,
                Timestamp,
                Timestamp.AddMinutes(4))
        ]);

        var result = await service.GetExecutionMetricsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.FailedExecutions);
        Assert.Equal(0, result.Value.CompletedExecutions);
        Assert.Equal(TimeSpan.FromMinutes(4), result.Value.AverageDuration);
    }

    [Fact]
    public async Task GetExecutionMetricsAsync_RunningExecution_CountsRunningWithoutDuration()
    {
        var service = CreateService(
        [
            CreateSummary(WorkflowExecutionStatus.Running, Timestamp, null)
        ]);

        var result = await service.GetExecutionMetricsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.RunningExecutions);
        Assert.Equal(Timestamp, result.Value.LastExecutionTimestamp);
        Assert.Null(result.Value.AverageDuration);
    }

    private static RuntimeDiagnosticsService CreateService(
        IReadOnlyList<ExecutionSummary> summaries) =>
        new(new ExecutionQueryServiceStub(summaries));

    private static ExecutionSummary CreateSummary(
        WorkflowExecutionStatus status,
        DateTime? startedAt,
        DateTime? completedAt) =>
        new()
        {
            WorkflowExecutionId = new WorkflowExecutionId(Guid.NewGuid()),
            CorrelationId = new ExecutionCorrelationId(Guid.NewGuid()),
            Status = status,
            DefinitionVersion = "test-v1",
            StartedAt = startedAt,
            CompletedAt = completedAt,
            OwnerId = null,
            LastHeartbeatAt = null,
            NodeExecutionCounts = new NodeExecutionCounts
            {
                Total = 0,
                Pending = 0,
                Running = 0,
                Succeeded = 0,
                Failed = 0,
                Cancelled = 0
            }
        };

    private sealed class ExecutionQueryServiceStub(
        IReadOnlyList<ExecutionSummary> summaries) : IExecutionQueryService
    {
        public Task<ExecutionSummary?> GetSummaryAsync(
            WorkflowExecutionId executionId,
            CancellationToken cancellationToken) => Task.FromResult<ExecutionSummary?>(null);

        public Task<IReadOnlyList<ExecutionSummary>> ListSummariesAsync(
            CancellationToken cancellationToken) => Task.FromResult(summaries);

        public Task<IReadOnlyList<ExecutionSummary>> FindExecutionsByCorrelationIdAsync(
            ExecutionCorrelationId correlationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExecutionSummary>>([]);

        public Task<IReadOnlyList<ExecutionHistoryEntry>> GetTimelineAsync(
            WorkflowExecutionId executionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExecutionHistoryEntry>>([]);
    }

    private static DateTime Timestamp { get; } =
        new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
}
