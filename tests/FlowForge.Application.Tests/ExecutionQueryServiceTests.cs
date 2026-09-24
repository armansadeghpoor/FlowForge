using FlowForge.Abstractions.Queries;
using FlowForge.Application.Queries;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.History;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Tests;

public sealed class ExecutionQueryServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_ExistingExecution_ReturnsSummary()
    {
        var summary = CreateSummary();
        var service = new WorkflowExecutionQueryService(
            new FakeExecutionQueryService { Summary = summary });

        var result = await service.GetSummaryAsync(
            summary.WorkflowExecutionId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(summary, result.Value);
    }

    [Fact]
    public async Task GetSummaryAsync_MissingExecution_ReturnsNotFoundError()
    {
        var service = new WorkflowExecutionQueryService(
            new FakeExecutionQueryService());

        var result = await service.GetSummaryAsync(
            new WorkflowExecutionId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ExecutionNotFound", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task GetTimelineAsync_ExistingExecution_ReturnsTimeline()
    {
        var executionId = new WorkflowExecutionId(Guid.NewGuid());
        IReadOnlyList<ExecutionHistoryEntry> timeline =
        [
            new ExecutionHistoryEntry
            {
                Id = new ExecutionHistoryId(Guid.NewGuid()),
                WorkflowExecutionId = executionId,
                NodeExecutionId = null,
                EventType = ExecutionHistoryEventType.WorkflowCreated,
                Timestamp = new DateTime(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc),
                Metadata = null
            }
        ];
        var service = new WorkflowExecutionQueryService(
            new FakeExecutionQueryService { Timeline = timeline });

        var result = await service.GetTimelineAsync(
            executionId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(timeline, result.Value);
    }

    private static ExecutionSummary CreateSummary() =>
        new()
        {
            WorkflowExecutionId = new WorkflowExecutionId(Guid.NewGuid()),
            Status = WorkflowExecutionStatus.Succeeded,
            DefinitionVersion = "v1",
            StartedAt = new DateTime(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 9, 24, 18, 1, 0, DateTimeKind.Utc),
            OwnerId = null,
            LastHeartbeatAt = null,
            NodeExecutionCounts = new NodeExecutionCounts
            {
                Total = 1,
                Pending = 0,
                Running = 0,
                Succeeded = 1,
                Failed = 0,
                Cancelled = 0
            }
        };

    private sealed class FakeExecutionQueryService : IExecutionQueryService
    {
        public ExecutionSummary? Summary { get; init; }

        public IReadOnlyList<ExecutionHistoryEntry> Timeline { get; init; } =
            Array.Empty<ExecutionHistoryEntry>();

        public Task<ExecutionSummary?> GetSummaryAsync(
            WorkflowExecutionId executionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Summary);

        public Task<IReadOnlyList<ExecutionHistoryEntry>> GetTimelineAsync(
            WorkflowExecutionId executionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Timeline);
    }
}
