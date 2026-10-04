using System.Text.Json;
using FlowForge.Core.Domain.Enums;

namespace FlowForge.Infrastructure.Tests.State;

public sealed partial class InMemoryStateStoreTests
{
    [Theory]
    [MemberData(nameof(LifecycleEvents))]
    public async Task Lifecycle_HistorySnapshotFails_DoesNotPublishState(ExecutionHistoryEventType eventType)
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var node = TransitionNode(eventType);
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        var document = JsonDocument.Parse("{}");
        var history = TransitionHistory(execution, node, eventType) with { Metadata = document.RootElement };
        document.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => store.ExecuteLifecycleAsync(
            execution.Id, async (transaction, token) =>
            {
                await WriteTransitionState(transaction, eventType, node, token);
                await transaction.AppendExecutionHistoryAsync(history, token);
            }, CancellationToken.None));

        var stored = await store.GetExecutionAsync(execution.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(execution.Status, stored.Status);
        Assert.Empty(stored.Nodes);
        Assert.Empty(await store.GetExecutionHistoryAsync(execution.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Lifecycle_StateWriteFails_DoesNotPublishHistory()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var node = CreateNodeExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.ExecuteLifecycleAsync(
            execution.Id, async (transaction, token) =>
            {
                await transaction.AppendExecutionHistoryAsync(
                    TransitionHistory(execution, node, ExecutionHistoryEventType.NodeStarted), token);
                await transaction.SaveNodeExecutionAsync(null!, token);
            }, CancellationToken.None));
        Assert.Empty(await store.GetExecutionHistoryAsync(execution.Id, CancellationToken.None));
        Assert.Empty((await store.GetExecutionAsync(execution.Id, CancellationToken.None))!.Nodes);
    }

    [Fact]
    public async Task Lifecycle_UncommittedWritesAreInvisible_AndOtherExecutionsCanProgress()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var other = CreateExecution();
        var node = CreateNodeExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        var staged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transitionTask = store.ExecuteLifecycleAsync(execution.Id, async (transaction, token) =>
        {
            await transaction.SaveNodeExecutionAsync(node, token);
            await transaction.AppendExecutionHistoryAsync(
                TransitionHistory(execution, node, ExecutionHistoryEventType.NodeStarted), token);
            staged.SetResult();
            await release.Task;
        }, CancellationToken.None);
        try
        {
            await staged.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Empty((await store.GetExecutionAsync(execution.Id, CancellationToken.None))!.Nodes);
            Assert.Empty(await store.GetExecutionHistoryAsync(execution.Id, CancellationToken.None));
            await store.CreateExecutionAsync(other, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            release.TrySetResult();
            await transitionTask;
        }

        Assert.Equal(node, Assert.Single((await store.GetExecutionAsync(execution.Id, CancellationToken.None))!.Nodes));
        Assert.Single(await store.GetExecutionHistoryAsync(execution.Id, CancellationToken.None));
    }
}
