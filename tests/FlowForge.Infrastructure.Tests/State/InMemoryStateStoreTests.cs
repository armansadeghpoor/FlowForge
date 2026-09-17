using FlowForge.Abstractions.State;
using FlowForge.Core.Domain.Enums;
using FlowForge.Infrastructure.State;
using FlowForge.Infrastructure.Tests.State.Conformance;

namespace FlowForge.Infrastructure.Tests.State;

public sealed class InMemoryStateStoreTests : StateStoreConformanceTests
{
    protected override IStateStore CreateStore() => new InMemoryStateStore();

    [Fact]
    public async Task SaveNodeExecutionAsync_IsolatesPreviouslyReadSnapshots()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var nodeExecution = CreateNodeExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        await store.SaveNodeExecutionAsync(execution.Id, nodeExecution, CancellationToken.None);
        var before = await store.GetExecutionAsync(execution.Id, CancellationToken.None);
        var replacement = nodeExecution with { AttemptNumber = 2, RetryCount = 1 };

        await store.SaveNodeExecutionAsync(execution.Id, replacement, CancellationToken.None);

        Assert.NotNull(before);
        Assert.Equal(nodeExecution, Assert.Single(before.Nodes));
        var after = await store.GetExecutionAsync(execution.Id, CancellationToken.None);
        Assert.NotNull(after);
        Assert.Equal(replacement, Assert.Single(after.Nodes));
    }

    [Fact]
    public async Task ConcurrentNodeAndWorkflowUpdates_PreserveAllState()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var nodes = Enumerable.Range(0, 32).Select(_ => CreateNodeExecution()).ToArray();
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        await Parallel.ForEachAsync(nodes, async (node, cancellationToken) =>
        {
            await store.SaveNodeExecutionAsync(execution.Id, node, cancellationToken);
            await store.UpdateWorkflowStatusAsync(
                execution.Id,
                WorkflowExecutionStatus.Running,
                execution.CreatedAt,
                null,
                cancellationToken);
        });

        var aggregate = await store.GetExecutionAsync(execution.Id, CancellationToken.None);

        Assert.NotNull(aggregate);
        Assert.Equal(WorkflowExecutionStatus.Running, aggregate.Status);
        Assert.Equal(nodes.Length, aggregate.Nodes.Count);
        Assert.All(nodes, node => Assert.Contains(node, aggregate.Nodes));
    }
}
