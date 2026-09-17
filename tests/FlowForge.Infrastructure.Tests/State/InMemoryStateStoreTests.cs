using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Failures;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Values;
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

        await store.CreateExecutionAsync(execution, CancellationToken.None);
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
        Assert.NotNull(retrieved.Output);
        Assert.Equal(nodeExecution.Output, retrieved.Output);
        Assert.Equal("node output", retrieved.Output.Value);
        Assert.NotSame(nodeExecution, retrieved);
        var aggregate = await store.GetExecutionAsync(execution.Id, CancellationToken.None);
        Assert.NotNull(aggregate);
        Assert.Equal(retrieved, Assert.Single(aggregate.Nodes));
    }

    [Fact]
    public async Task SaveNodeExecutionAsync_MissingWorkflow_ThrowsWithoutSavingNode()
    {
        var store = new InMemoryStateStore();
        var execution = Execution();
        var node = NodeExecution();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => store.SaveNodeExecutionAsync(execution.Id, node, CancellationToken.None));

        Assert.Null(await store.GetExecutionAsync(execution.Id, CancellationToken.None));
        Assert.Null(await store.GetNodeExecutionAsync(execution.Id, node.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetExecutionAsync_IncludesInitialAndSavedNodesWithFullState()
    {
        var store = new InMemoryStateStore();
        var initial = NodeExecution();
        var execution = Execution() with { Nodes = new[] { initial } };
        var failed = NodeExecution() with
        {
            Status = NodeExecutionStatus.Failed,
            AttemptNumber = 3,
            RetryCount = 2,
            CompletedAt = new DateTime(2026, 1, 2, 3, 5, 5, DateTimeKind.Utc),
            Failure = new NodeFailure { Category = NodeFailureCategory.External, Message = "Unavailable." }
        };
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        await store.SaveNodeExecutionAsync(execution.Id, failed, CancellationToken.None);
        await store.UpdateWorkflowStatusAsync(execution.Id, WorkflowExecutionStatus.Failed,
            failed.StartedAt, failed.CompletedAt, CancellationToken.None);

        var aggregate = await store.GetExecutionAsync(execution.Id, CancellationToken.None);
        var saved = await store.GetNodeExecutionAsync(execution.Id, failed.Id, CancellationToken.None);

        Assert.NotNull(aggregate);
        Assert.Equal(2, aggregate.Nodes.Count);
        Assert.Contains(initial, aggregate.Nodes);
        Assert.Contains(failed, aggregate.Nodes);
        Assert.Equal(failed, saved);
        Assert.Equal(initial, await store.GetNodeExecutionAsync(execution.Id, initial.Id, CancellationToken.None));
        Assert.NotNull(saved);
        Assert.Equal(NodeFailureCategory.External, saved.Failure?.Category);
        Assert.Equal("Unavailable.", saved.Failure?.Message);
        Assert.Equal(3, saved.AttemptNumber);
        Assert.Equal(2, saved.RetryCount);
        Assert.Equal(failed.Output, saved.Output);
        Assert.Equal(failed.StartedAt, saved.StartedAt);
        Assert.Equal(failed.CompletedAt, saved.CompletedAt);
        Assert.Single(execution.Nodes);
    }

    [Fact]
    public async Task SaveNodeExecutionAsync_IsolatesWorkflowsAndPreviouslyReadSnapshots()
    {
        var store = new InMemoryStateStore();
        var first = Execution();
        var second = Execution();
        var node = NodeExecution();
        await store.CreateExecutionAsync(first, CancellationToken.None);
        await store.CreateExecutionAsync(second, CancellationToken.None);
        await store.SaveNodeExecutionAsync(first.Id, node, CancellationToken.None);
        var before = await store.GetExecutionAsync(first.Id, CancellationToken.None);
        var updated = node with { AttemptNumber = 2, RetryCount = 1 };

        await store.SaveNodeExecutionAsync(first.Id, updated, CancellationToken.None);

        Assert.NotNull(before);
        Assert.Equal(node, Assert.Single(before.Nodes));
        var after = await store.GetExecutionAsync(first.Id, CancellationToken.None);
        Assert.NotNull(after);
        Assert.Equal(updated, Assert.Single(after.Nodes));
        var other = await store.GetExecutionAsync(second.Id, CancellationToken.None);
        Assert.NotNull(other);
        Assert.Empty(other.Nodes);
        Assert.Null(await store.GetNodeExecutionAsync(second.Id, node.Id, CancellationToken.None));
    }

    [Fact]
    public async Task SaveNodeExecutionAsync_ConcurrentSaves_PreserveAllNodesAndWorkflowStatus()
    {
        var store = new InMemoryStateStore();
        var execution = Execution();
        var nodes = Enumerable.Range(0, 32).Select(_ => NodeExecution()).ToArray();
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        await Parallel.ForEachAsync(nodes, async (node, token) =>
        {
            await store.SaveNodeExecutionAsync(execution.Id, node, token);
            await store.UpdateWorkflowStatusAsync(execution.Id, WorkflowExecutionStatus.Running,
                execution.CreatedAt, null, token);
        });

        var aggregate = await store.GetExecutionAsync(execution.Id, CancellationToken.None);
        Assert.NotNull(aggregate);
        Assert.Equal(WorkflowExecutionStatus.Running, aggregate.Status);
        Assert.Equal(nodes.Length, aggregate.Nodes.Count);
        Assert.All(nodes, node => Assert.Contains(node, aggregate.Nodes));
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
            AttemptNumber = 1,
            StartedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            CompletedAt = null,
            Output = new NodeOutput { Value = "node output" },
            Failure = null
        };
}
