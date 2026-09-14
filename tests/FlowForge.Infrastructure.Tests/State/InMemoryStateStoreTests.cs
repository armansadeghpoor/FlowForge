using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Infrastructure.State;

namespace FlowForge.Infrastructure.Tests.State;

public sealed class InMemoryStateStoreTests
{
    [Fact]
    public async Task CreateExecutionAsync_StoresRetrievableSnapshot()
    {
        var store = new InMemoryStateStore();
        var execution = Execution();

        await store.CreateExecutionAsync(execution, CancellationToken.None);
        var retrieved = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(execution.Id, retrieved.Id);
        Assert.Equal(execution.WorkflowId, retrieved.WorkflowId);
        Assert.Equal(execution.Status, retrieved.Status);
        Assert.Equal(execution.CreatedAt, retrieved.CreatedAt);
        Assert.Equal(execution.Nodes, retrieved.Nodes);
        Assert.NotSame(execution, retrieved);
        Assert.NotSame(execution.Nodes, retrieved.Nodes);
    }

    [Fact]
    public async Task CreateExecutionAsync_DuplicateId_ThrowsInvalidOperationException()
    {
        var store = new InMemoryStateStore();
        var execution = Execution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.CreateExecutionAsync(execution with { }, CancellationToken.None));

        Assert.Contains(execution.Id.Value.ToString(), exception.Message);
    }

    [Fact]
    public async Task UpdateWorkflowStatusAsync_ReplacesStatusAndLifecycleTimestamps()
    {
        var store = new InMemoryStateStore();
        var execution = Execution();
        var startedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
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
        Assert.Equal(WorkflowExecutionStatus.Pending, execution.Status);
        Assert.Null(execution.StartedAt);
        Assert.Null(execution.CompletedAt);
    }

    [Fact]
    public async Task SaveNodeExecutionAsync_StoresAndReplacesRetrievableSnapshot()
    {
        var store = new InMemoryStateStore();
        var execution = Execution();
        var nodeExecution = NodeExecution();

        await store.SaveNodeExecutionAsync(execution.Id, nodeExecution, CancellationToken.None);
        await store.SaveNodeExecutionAsync(
            execution.Id,
            nodeExecution with
            {
                Status = NodeExecutionStatus.Succeeded,
                CompletedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
            },
            CancellationToken.None);
        var retrieved = await store.GetNodeExecutionAsync(
            execution.Id,
            nodeExecution.Id,
            CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(NodeExecutionStatus.Succeeded, retrieved.Status);
        Assert.NotNull(retrieved.CompletedAt);
        Assert.NotSame(nodeExecution, retrieved);
    }

    [Fact]
    public async Task GetExecutionAsync_MissingExecution_ReturnsNull()
    {
        var store = new InMemoryStateStore();

        var retrieved = await store.GetExecutionAsync(
            new WorkflowExecutionId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetNodeExecutionAsync_MissingNode_ReturnsNull()
    {
        var store = new InMemoryStateStore();

        var retrieved = await store.GetNodeExecutionAsync(
            new WorkflowExecutionId(Guid.NewGuid()),
            new NodeExecutionId(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(retrieved);
    }

    private static WorkflowExecution Execution() =>
        new()
        {
            Id = new WorkflowExecutionId(Guid.NewGuid()),
            WorkflowId = new WorkflowId(Guid.NewGuid()),
            Status = WorkflowExecutionStatus.Pending,
            CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            StartedAt = null,
            CompletedAt = null,
            Nodes = Array.Empty<NodeExecutionState>()
        };

    private static NodeExecutionState NodeExecution() =>
        new()
        {
            Id = new NodeExecutionId(Guid.NewGuid()),
            NodeId = new NodeId(Guid.NewGuid()),
            Status = NodeExecutionStatus.Running,
            RetryCount = 0,
            StartedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            CompletedAt = null,
            Output = null,
            ErrorMessage = null
        };
}
