using FlowForge.Abstractions.State;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Failures;
using FlowForge.Core.Domain.History;

namespace FlowForge.Infrastructure.Tests.State.Conformance;

public abstract partial class StateStoreConformanceTests
{
    public static IEnumerable<object[]> LifecycleEvents =>
        new[]
        {
            ExecutionHistoryEventType.NodeStarted, ExecutionHistoryEventType.NodeCompleted,
            ExecutionHistoryEventType.NodeFailed, ExecutionHistoryEventType.NodeCancelled,
            ExecutionHistoryEventType.WorkflowCompleted, ExecutionHistoryEventType.WorkflowFailed,
            ExecutionHistoryEventType.WorkflowCancelled
        }.Select(eventType => new object[] { eventType });

    public static IEnumerable<object[]> LifecycleRollbackCases =>
        LifecycleEvents.SelectMany(row => new[] { false, true }
            .Select(historyFirst => new[] { row[0], (object)historyFirst }));

    [SkippableTheory]
    [MemberData(nameof(LifecycleEvents))]
    public async Task Lifecycle_CommitsStateAndHistory(ExecutionHistoryEventType eventType)
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var node = TransitionNode(eventType);
        var history = TransitionHistory(execution, node, eventType);
        await store.CreateExecutionAsync(execution, CancellationToken.None);

        await store.ExecuteLifecycleAsync(execution.Id, async (transaction, token) =>
        {
            await WriteTransitionState(transaction, eventType, node, token);
            await transaction.AppendExecutionHistoryAsync(history, token);
        }, CancellationToken.None);

        var actual = await store.GetExecutionAsync(execution.Id, CancellationToken.None);
        Assert.NotNull(actual);
        if (IsNodeEvent(eventType))
            Assert.Equal(node, Assert.Single(actual.Nodes));
        else
        {
            Assert.Equal(WorkflowStatus(eventType), actual.Status);
            Assert.Equal(Timestamp.AddMinutes(1), actual.CompletedAt);
        }
        Assert.Equal(history.Id, Assert.Single(await store.GetExecutionHistoryAsync(
            execution.Id, CancellationToken.None)).Id);
    }

    [SkippableTheory]
    [MemberData(nameof(LifecycleRollbackCases))]
    public async Task Lifecycle_EitherWriteFails_RollsBackBoth(
        ExecutionHistoryEventType eventType, bool historyFirst)
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var originalNode = CreateNodeExecution();
        var node = TransitionNode(eventType) with { Id = originalNode.Id };
        var history = TransitionHistory(execution, node, eventType);
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        await store.SaveNodeExecutionAsync(execution.Id, originalNode, CancellationToken.None);
        var expected = new LifecycleWriteException();

        // Inject a failure after the second participating write has staged its changes.
        // This verifies rollback even when both writes have executed successfully so far.
        var actual = await Assert.ThrowsAsync<LifecycleWriteException>(() =>
            store.ExecuteLifecycleAsync(execution.Id, async (transaction, token) =>
            {
                if (historyFirst)
                {
                    await transaction.AppendExecutionHistoryAsync(history, token);
                    await WriteTransitionState(transaction, eventType, node, token);
                }
                else
                {
                    await WriteTransitionState(transaction, eventType, node, token);
                    await transaction.AppendExecutionHistoryAsync(history, token);
                }

                throw expected;
            }, CancellationToken.None));

        Assert.Same(expected, actual);
        var stored = await store.GetExecutionAsync(execution.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(execution.Status, stored.Status);
        Assert.Equal(execution.CompletedAt, stored.CompletedAt);
        Assert.Equal(originalNode, Assert.Single(stored.Nodes));
        Assert.Empty(await store.GetExecutionHistoryAsync(execution.Id, CancellationToken.None));
    }

    [SkippableFact]
    public async Task Lifecycle_CreatesWorkflowWithBothInitialEvents()
    {
        var store = CreateStore();
        var execution = CreateExecution() with { Status = WorkflowExecutionStatus.Running, StartedAt = Timestamp };
        await store.ExecuteLifecycleAsync(execution.Id, async (transaction, token) =>
        {
            await transaction.CreateExecutionAsync(execution, token);
            await transaction.AppendExecutionHistoryAsync(
                CreateHistoryEntry(execution, ExecutionHistoryEventType.WorkflowCreated, Timestamp), token);
            await transaction.AppendExecutionHistoryAsync(
                CreateHistoryEntry(execution, ExecutionHistoryEventType.WorkflowStarted, Timestamp), token);
        }, CancellationToken.None);

        Assert.Equal(WorkflowExecutionStatus.Running,
            (await store.GetExecutionAsync(execution.Id, CancellationToken.None))?.Status);
        Assert.Equal(new[] { ExecutionHistoryEventType.WorkflowCreated, ExecutionHistoryEventType.WorkflowStarted },
            (await store.GetExecutionHistoryAsync(execution.Id, CancellationToken.None)).Select(entry => entry.EventType));
    }

    [SkippableFact]
    public async Task Lifecycle_FailedCreation_PublishesNeitherWorkflowNorHistory()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        await Assert.ThrowsAsync<LifecycleWriteException>(() => store.ExecuteLifecycleAsync(
            execution.Id, async (transaction, token) =>
            {
                await transaction.CreateExecutionAsync(execution, token);
                await transaction.AppendExecutionHistoryAsync(
                    CreateHistoryEntry(execution, ExecutionHistoryEventType.WorkflowCreated, Timestamp), token);
                throw new LifecycleWriteException();
            }, CancellationToken.None));
        Assert.Null(await store.GetExecutionAsync(execution.Id, CancellationToken.None));
        Assert.Empty(await store.GetExecutionHistoryAsync(execution.Id, CancellationToken.None));
    }

    [SkippableFact]
    public async Task Lifecycle_ConcurrentNodeTransitions_PreserveEveryCommittedPair()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        var nodes = Enumerable.Range(0, 16).Select(_ => TransitionNode(ExecutionHistoryEventType.NodeFailed)).ToArray();
        await Parallel.ForEachAsync(nodes, async (node, cancellationToken) =>
        {
            await store.ExecuteLifecycleAsync(execution.Id, async (transaction, token) =>
            {
                await transaction.SaveNodeExecutionAsync(node, token);
                await transaction.AppendExecutionHistoryAsync(
                    TransitionHistory(execution, node, ExecutionHistoryEventType.NodeFailed), token);
            }, cancellationToken);
        });
        var aggregate = await store.GetExecutionAsync(execution.Id, CancellationToken.None);
        var history = await store.GetExecutionHistoryAsync(execution.Id, CancellationToken.None);
        Assert.NotNull(aggregate);
        Assert.Equal(nodes.Length, aggregate.Nodes.Count);
        Assert.Equal(nodes.Length, history.Count);
        Assert.All(nodes, node =>
        {
            Assert.Contains(node, aggregate.Nodes);
            Assert.Single(history, entry => entry.NodeExecutionId == node.Id);
        });
    }

    [SkippableFact]
    public async Task Lifecycle_ConcurrentFailure_DoesNotRollbackSuccessfulSibling()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        var nodes = Enumerable.Range(0, 16).Select(_ => TransitionNode(ExecutionHistoryEventType.NodeCompleted)).ToArray();
        await Parallel.ForEachAsync(Enumerable.Range(0, nodes.Length), async (index, cancellationToken) =>
        {
            var exception = await Record.ExceptionAsync(() => store.ExecuteLifecycleAsync(
                execution.Id, async (transaction, token) =>
                {
                    await transaction.SaveNodeExecutionAsync(nodes[index], token);
                    await transaction.AppendExecutionHistoryAsync(
                        TransitionHistory(execution, nodes[index], ExecutionHistoryEventType.NodeCompleted), token);
                    if (index % 2 == 0)
                    {
                        throw new LifecycleWriteException();
                    }
                }, cancellationToken));
            if (index % 2 == 0)
            {
                Assert.IsType<LifecycleWriteException>(exception);
            }
            else
            {
                Assert.Null(exception);
            }
        });

        var aggregate = await store.GetExecutionAsync(execution.Id, CancellationToken.None);
        var history = await store.GetExecutionHistoryAsync(execution.Id, CancellationToken.None);
        Assert.NotNull(aggregate);
        Assert.Equal(nodes.Length / 2, aggregate.Nodes.Count);
        Assert.Equal(nodes.Length / 2, history.Count);
        for (var index = 0; index < nodes.Length; index++)
        {
            Assert.Equal(index % 2 != 0, aggregate.Nodes.Any(node => node.Id == nodes[index].Id));
            Assert.Equal(index % 2 != 0, history.Any(entry => entry.NodeExecutionId == nodes[index].Id));
        }
    }

    [SkippableFact]
    public async Task Lifecycle_CancellationBeforeCommit_RollsBack()
    {
        var store = CreateStore();
        var execution = CreateExecution();
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        using var source = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ExecuteLifecycleAsync(
            execution.Id, async (transaction, token) =>
            {
                await transaction.UpdateWorkflowStatusAsync(WorkflowExecutionStatus.Cancelled, Timestamp, Timestamp, token);
                await transaction.AppendExecutionHistoryAsync(
                    CreateHistoryEntry(execution, ExecutionHistoryEventType.WorkflowCancelled, Timestamp), token);
                source.Cancel();
            }, source.Token));
        Assert.Equal(execution.Status, (await store.GetExecutionAsync(execution.Id, CancellationToken.None))?.Status);
        Assert.Empty(await store.GetExecutionHistoryAsync(execution.Id, CancellationToken.None));
    }

    protected static bool IsNodeEvent(ExecutionHistoryEventType type) => type is
        ExecutionHistoryEventType.NodeStarted or ExecutionHistoryEventType.NodeCompleted or
        ExecutionHistoryEventType.NodeFailed or ExecutionHistoryEventType.NodeCancelled;

    private static WorkflowExecutionStatus WorkflowStatus(ExecutionHistoryEventType type) => type switch
    {
        ExecutionHistoryEventType.WorkflowCompleted => WorkflowExecutionStatus.Succeeded,
        ExecutionHistoryEventType.WorkflowFailed => WorkflowExecutionStatus.Failed,
        _ => WorkflowExecutionStatus.Cancelled
    };

    protected static NodeExecutionState TransitionNode(ExecutionHistoryEventType type) => CreateNodeExecution() with
    {
        Status = type switch
        {
            ExecutionHistoryEventType.NodeStarted => NodeExecutionStatus.Running,
            ExecutionHistoryEventType.NodeCompleted => NodeExecutionStatus.Succeeded,
            ExecutionHistoryEventType.NodeFailed => NodeExecutionStatus.Failed,
            _ => NodeExecutionStatus.Cancelled
        },
        CompletedAt = type == ExecutionHistoryEventType.NodeStarted ? null : Timestamp.AddMinutes(1),
        Failure = type is ExecutionHistoryEventType.NodeFailed or ExecutionHistoryEventType.NodeCancelled
            ? new NodeFailure { Category = NodeFailureCategory.Cancelled, Message = "Safe failure." } : null
    };

    protected static ExecutionHistoryEntry TransitionHistory(
        WorkflowExecution execution, NodeExecutionState node, ExecutionHistoryEventType type) =>
        CreateHistoryEntry(execution, type, Timestamp.AddMinutes(1), IsNodeEvent(type) ? node.Id : null);

    protected static Task WriteTransitionState(IExecutionLifecycleTransaction transaction,
        ExecutionHistoryEventType type, NodeExecutionState node, CancellationToken token) =>
        IsNodeEvent(type) ? transaction.SaveNodeExecutionAsync(node, token) :
            transaction.UpdateWorkflowStatusAsync(WorkflowStatus(type), Timestamp, Timestamp.AddMinutes(1), token);

    private sealed class LifecycleWriteException : Exception;
}
