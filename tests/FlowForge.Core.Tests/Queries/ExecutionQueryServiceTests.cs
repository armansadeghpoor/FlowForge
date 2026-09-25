using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.History;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Engine.Queries;
using FlowForge.Infrastructure.State;

namespace FlowForge.Core.Tests.Queries;

public sealed class ExecutionQueryServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_IncludesLifecycleInformation()
    {
        var startedAt = Timestamp;
        var completedAt = Timestamp.AddMinutes(2);
        var execution = CreateExecution() with
        {
            Status = WorkflowExecutionStatus.Succeeded,
            StartedAt = startedAt,
            CompletedAt = completedAt
        };
        var service = await CreateServiceAsync(execution);

        var summary = await service.GetSummaryAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal(execution.Id, summary.WorkflowExecutionId);
        Assert.Equal(execution.CorrelationId, summary.CorrelationId);
        Assert.Equal(WorkflowExecutionStatus.Succeeded, summary.Status);
        Assert.Equal(startedAt, summary.StartedAt);
        Assert.Equal(completedAt, summary.CompletedAt);
    }

    [Fact]
    public async Task GetSummaryAsync_IncludesDefinitionVersion()
    {
        var execution = CreateExecution() with { DefinitionVersion = "definition-v42" };
        var service = await CreateServiceAsync(execution);

        var summary = await service.GetSummaryAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal("definition-v42", summary.DefinitionVersion);
    }

    [Fact]
    public async Task GetSummaryAsync_IncludesNodeExecutionCounts()
    {
        var execution = CreateExecution(
            CreateNode(NodeExecutionStatus.Pending),
            CreateNode(NodeExecutionStatus.Running),
            CreateNode(NodeExecutionStatus.Succeeded),
            CreateNode(NodeExecutionStatus.Succeeded),
            CreateNode(NodeExecutionStatus.Failed),
            CreateNode(NodeExecutionStatus.Cancelled));
        var service = await CreateServiceAsync(execution);

        var summary = await service.GetSummaryAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal(6, summary.NodeExecutionCounts.Total);
        Assert.Equal(1, summary.NodeExecutionCounts.Pending);
        Assert.Equal(1, summary.NodeExecutionCounts.Running);
        Assert.Equal(2, summary.NodeExecutionCounts.Succeeded);
        Assert.Equal(1, summary.NodeExecutionCounts.Failed);
        Assert.Equal(1, summary.NodeExecutionCounts.Cancelled);
    }

    [Fact]
    public async Task GetSummaryAsync_IncludesRecoveryMetadata()
    {
        var heartbeatAt = Timestamp.AddMinutes(1);
        var execution = CreateExecution() with
        {
            OwnerId = "worker-observer",
            LastHeartbeatAt = heartbeatAt
        };
        var service = await CreateServiceAsync(execution);

        var summary = await service.GetSummaryAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal("worker-observer", summary.OwnerId);
        Assert.Equal(heartbeatAt, summary.LastHeartbeatAt);
    }

    [Fact]
    public async Task GetTimelineAsync_ReturnsOrderedHistory()
    {
        var execution = CreateExecution();
        var stateStore = new InMemoryStateStore();
        var service = new ExecutionQueryService(stateStore, stateStore);
        var completed = CreateHistory(
            execution,
            ExecutionHistoryEventType.WorkflowCompleted,
            Timestamp.AddMinutes(1));
        var created = CreateHistory(
            execution,
            ExecutionHistoryEventType.WorkflowCreated,
            Timestamp);
        var started = CreateHistory(
            execution,
            ExecutionHistoryEventType.WorkflowStarted,
            Timestamp);
        await stateStore.CreateExecutionAsync(execution, CancellationToken.None);
        await stateStore.AppendExecutionHistoryAsync(completed, CancellationToken.None);
        await stateStore.AppendExecutionHistoryAsync(created, CancellationToken.None);
        await stateStore.AppendExecutionHistoryAsync(started, CancellationToken.None);

        var timeline = await service.GetTimelineAsync(execution.Id, CancellationToken.None);

        Assert.Equal(
            [created.Id, started.Id, completed.Id],
            timeline.Select(entry => entry.Id));
    }

    [Fact]
    public async Task GetTimelineAsync_NoHistory_ReturnsEmptyCollection()
    {
        var execution = CreateExecution();
        var service = await CreateServiceAsync(execution);

        var timeline = await service.GetTimelineAsync(execution.Id, CancellationToken.None);

        Assert.Empty(timeline);
    }

    [Fact]
    public async Task ListSummariesAsync_ReturnsAllStoredExecutions()
    {
        var first = CreateExecution() with
        {
            Status = WorkflowExecutionStatus.Succeeded,
            CompletedAt = Timestamp.AddMinutes(1)
        };
        var second = CreateExecution() with
        {
            Status = WorkflowExecutionStatus.Running,
            StartedAt = Timestamp.AddMinutes(2)
        };
        var stateStore = new InMemoryStateStore();
        await stateStore.CreateExecutionAsync(first, CancellationToken.None);
        await stateStore.CreateExecutionAsync(second, CancellationToken.None);
        var service = new ExecutionQueryService(stateStore, stateStore);

        var summaries = await service.ListSummariesAsync(CancellationToken.None);

        Assert.Equal(2, summaries.Count);
        Assert.Contains(summaries, summary =>
            summary.WorkflowExecutionId == first.Id &&
            summary.Status == WorkflowExecutionStatus.Succeeded);
        Assert.Contains(summaries, summary =>
            summary.WorkflowExecutionId == second.Id &&
            summary.Status == WorkflowExecutionStatus.Running);
    }

    [Fact]
    public async Task FindExecutionsByCorrelationIdAsync_ExistingCorrelation_ReturnsMatchingSummary()
    {
        var execution = CreateExecution();
        var service = await CreateServiceAsync(execution);

        var summaries = await service.FindExecutionsByCorrelationIdAsync(
            execution.CorrelationId,
            CancellationToken.None);

        var summary = Assert.Single(summaries);
        Assert.Equal(execution.Id, summary.WorkflowExecutionId);
        Assert.Equal(execution.CorrelationId, summary.CorrelationId);
    }

    [Fact]
    public async Task FindExecutionsByCorrelationIdAsync_MissingCorrelation_ReturnsEmptyCollection()
    {
        var execution = CreateExecution();
        var service = await CreateServiceAsync(execution);

        var summaries = await service.FindExecutionsByCorrelationIdAsync(
            new ExecutionCorrelationId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Empty(summaries);
    }

    private static async Task<ExecutionQueryService> CreateServiceAsync(
        WorkflowExecution execution)
    {
        var stateStore = new InMemoryStateStore();
        await stateStore.CreateExecutionAsync(execution, CancellationToken.None);
        return new ExecutionQueryService(stateStore, stateStore);
    }

    private static WorkflowExecution CreateExecution(
        params NodeExecutionState[] nodes) =>
        new()
        {
            Id = new WorkflowExecutionId(Guid.NewGuid()),
            WorkflowId = new WorkflowId(Guid.NewGuid()),
            CorrelationId = new ExecutionCorrelationId(Guid.NewGuid()),
            DefinitionVersion = "definition-v1",
            Status = WorkflowExecutionStatus.Running,
            CreatedAt = Timestamp,
            StartedAt = Timestamp,
            CompletedAt = null,
            OwnerId = null,
            LastHeartbeatAt = null,
            Nodes = Array.AsReadOnly(nodes)
        };

    private static NodeExecutionState CreateNode(NodeExecutionStatus status) =>
        new()
        {
            Id = new NodeExecutionId(Guid.NewGuid()),
            NodeId = new NodeId(Guid.NewGuid()),
            Status = status,
            RetryCount = 0,
            AttemptNumber = 1,
            StartedAt = status == NodeExecutionStatus.Pending ? null : Timestamp,
            CompletedAt = status is NodeExecutionStatus.Succeeded
                or NodeExecutionStatus.Failed
                or NodeExecutionStatus.Cancelled
                    ? Timestamp.AddMinutes(1)
                    : null,
            Output = null,
            Failure = null
        };

    private static ExecutionHistoryEntry CreateHistory(
        WorkflowExecution execution,
        ExecutionHistoryEventType eventType,
        DateTime timestamp) =>
        new()
        {
            Id = new ExecutionHistoryId(Guid.NewGuid()),
            WorkflowExecutionId = execution.Id,
            NodeExecutionId = null,
            EventType = eventType,
            Timestamp = timestamp,
            Metadata = null
        };

    private static DateTime Timestamp { get; } =
        new(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);
}
