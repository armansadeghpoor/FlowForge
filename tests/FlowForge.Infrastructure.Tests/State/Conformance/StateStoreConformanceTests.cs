using FlowForge.Abstractions.State;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Failures;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Values;

namespace FlowForge.Infrastructure.Tests.State.Conformance;

public abstract class StateStoreConformanceTests
{
    protected abstract IStateStore CreateStore();

    [SkippableFact]
    public async Task CreateExecutionAsync_StoresRetrievableExecution()
    {
        var store = CreateStore();
        var execution = CreateExecution();

        await store.CreateExecutionAsync(execution, CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(execution.Id, retrieved.Id);
        Assert.Equal(execution.WorkflowId, retrieved.WorkflowId);
        Assert.Equal(execution.Status, retrieved.Status);
        Assert.Equal(execution.CreatedAt, retrieved.CreatedAt);
        Assert.Equal(execution.StartedAt, retrieved.StartedAt);
        Assert.Equal(execution.CompletedAt, retrieved.CompletedAt);
        Assert.Equal(execution.Nodes, retrieved.Nodes);
    }

    [SkippableFact]
    public async Task CreateExecutionAsync_DuplicateId_ThrowsInvalidOperationException()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.CreateExecutionAsync(execution with { }, CancellationToken.None));

        Assert.Contains(execution.Id.Value.ToString(), exception.Message);
    }

    [SkippableFact]
    public async Task GetExecutionAsync_IncludesSavedNodeExecutions()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var firstNode = CreateNodeExecution();
        var secondNode = CreateNodeExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        await store.SaveNodeExecutionAsync(execution.Id, firstNode, CancellationToken.None);
        await store.SaveNodeExecutionAsync(execution.Id, secondNode, CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved.Nodes.Count);
        Assert.Contains(firstNode, retrieved.Nodes);
        Assert.Contains(secondNode, retrieved.Nodes);
    }

    [SkippableFact]
    public async Task SaveNodeExecutionAsync_SameId_ReplacesStoredNodeExecution()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var nodeExecution = CreateNodeExecution();
        var replacement = nodeExecution with
        {
            Status = NodeExecutionStatus.Succeeded,
            CompletedAt = Timestamp.AddMinutes(1),
            Output = new NodeOutput { Value = "replacement output" }
        };
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        await store.SaveNodeExecutionAsync(execution.Id, nodeExecution, CancellationToken.None);

        await store.SaveNodeExecutionAsync(execution.Id, replacement, CancellationToken.None);
        var retrieved = await store.GetNodeExecutionAsync(
            execution.Id,
            nodeExecution.Id,
            CancellationToken.None);
        var aggregate = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.Equal(replacement, retrieved);
        Assert.NotNull(aggregate);
        Assert.Equal(replacement, Assert.Single(aggregate.Nodes));
    }

    [SkippableFact]
    public async Task SaveNodeExecutionAsync_PreservesFailureCategoryAndMessage()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var nodeExecution = CreateNodeExecution() with
        {
            Status = NodeExecutionStatus.Failed,
            CompletedAt = Timestamp.AddMinutes(1),
            Failure = new NodeFailure
            {
                Category = NodeFailureCategory.External,
                Message = "External service unavailable."
            }
        };
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        await store.SaveNodeExecutionAsync(execution.Id, nodeExecution, CancellationToken.None);
        var retrieved = await store.GetNodeExecutionAsync(
            execution.Id,
            nodeExecution.Id,
            CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(NodeFailureCategory.External, retrieved.Failure?.Category);
        Assert.Equal("External service unavailable.", retrieved.Failure?.Message);
    }

    [SkippableFact]
    public async Task SaveNodeExecutionAsync_PreservesAttemptNumber()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var nodeExecution = CreateNodeExecution() with
        {
            RetryCount = 2,
            AttemptNumber = 3
        };
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        await store.SaveNodeExecutionAsync(execution.Id, nodeExecution, CancellationToken.None);
        var retrieved = await store.GetNodeExecutionAsync(
            execution.Id,
            nodeExecution.Id,
            CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(3, retrieved.AttemptNumber);
        Assert.Equal(2, retrieved.RetryCount);
    }

    [SkippableFact]
    public async Task SaveNodeExecutionAsync_MissingWorkflow_ThrowsKeyNotFoundException()
    {
        var store = CreateStore();
        var executionId = new WorkflowExecutionId(Guid.NewGuid());
        var nodeExecution = CreateNodeExecution();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => store.SaveNodeExecutionAsync(
                executionId,
                nodeExecution,
                CancellationToken.None));

        Assert.Null(await store.GetNodeExecutionAsync(
            executionId,
            nodeExecution.Id,
            CancellationToken.None));
    }

    [SkippableFact]
    public async Task GetExecutionAsync_MissingWorkflow_ReturnsNull()
    {
        var store = CreateStore();

        var retrieved = await store.GetExecutionAsync(
            new WorkflowExecutionId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(retrieved);
    }

    [SkippableFact]
    public async Task GetNodeExecutionAsync_MissingNode_ReturnsNull()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        var retrieved = await store.GetNodeExecutionAsync(
            execution.Id,
            new NodeExecutionId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(retrieved);
    }

    [SkippableFact]
    public async Task UpdateWorkflowStatusAsync_ReplacesStatusAndLifecycleTimestamps()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var startedAt = Timestamp.AddMinutes(1);
        var completedAt = startedAt.AddMinutes(2);
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        await store.UpdateWorkflowStatusAsync(
            execution.Id,
            WorkflowExecutionStatus.Succeeded,
            startedAt,
            completedAt,
            CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(WorkflowExecutionStatus.Succeeded, retrieved.Status);
        Assert.Equal(startedAt, retrieved.StartedAt);
        Assert.Equal(completedAt, retrieved.CompletedAt);
    }

    protected static WorkflowExecution CreateExecution() =>
        new()
        {
            Id = new WorkflowExecutionId(Guid.NewGuid()),
            WorkflowId = new WorkflowId(Guid.NewGuid()),
            Status = WorkflowExecutionStatus.Pending,
            CreatedAt = Timestamp,
            StartedAt = null,
            CompletedAt = null,
            Nodes = Array.Empty<NodeExecutionState>()
        };

    protected static NodeExecutionState CreateNodeExecution() =>
        new()
        {
            Id = new NodeExecutionId(Guid.NewGuid()),
            NodeId = new NodeId(Guid.NewGuid()),
            Status = NodeExecutionStatus.Running,
            RetryCount = 0,
            AttemptNumber = 1,
            StartedAt = Timestamp,
            CompletedAt = null,
            Output = new NodeOutput { Value = "node output" },
            Failure = null
        };

    private static DateTime Timestamp { get; } =
        new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
}
