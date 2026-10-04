using FlowForge.Core.Domain.Enums;
using Npgsql;

namespace FlowForge.Infrastructure.Tests.State;

public sealed partial class PostgreSqlStateStoreTests
{
    [SkippableTheory]
    [MemberData(nameof(LifecycleEvents))]
    public async Task Lifecycle_HistoryConstraintFailure_RollsBackState(ExecutionHistoryEventType eventType)
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var originalNode = CreateNodeExecution();
        var node = TransitionNode(eventType) with { Id = originalNode.Id };
        var entry = TransitionHistory(execution, node, eventType);
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        await store.SaveNodeExecutionAsync(execution.Id, originalNode, CancellationToken.None);
        await store.AppendExecutionHistoryAsync(entry, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            store.ExecuteLifecycleAsync(execution.Id, async (transaction, token) =>
            {
                await WriteTransitionState(transaction, eventType, node, token);
                // Existing history identity forces a real database write failure.
                await transaction.AppendExecutionHistoryAsync(entry, token);
            }, CancellationToken.None));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        var aggregate = await store.GetExecutionAsync(execution.Id, CancellationToken.None);
        Assert.NotNull(aggregate);
        Assert.Equal(execution.Status, aggregate.Status);
        Assert.Equal(execution.CompletedAt, aggregate.CompletedAt);
        Assert.Equal(originalNode, Assert.Single(aggregate.Nodes));
        Assert.Equal(entry.Id, Assert.Single(await store.GetExecutionHistoryAsync(
            execution.Id, CancellationToken.None)).Id);
    }

    [SkippableTheory]
    [MemberData(nameof(LifecycleEvents))]
    public async Task Lifecycle_StateConstraintFailure_RollsBackHistory(ExecutionHistoryEventType eventType)
    {
        var store = CreateStore();
        var execution = CreateExecution();
        var originalNode = CreateNodeExecution();
        var node = TransitionNode(eventType) with { Id = originalNode.Id };
        await store.CreateExecutionAsync(execution, CancellationToken.None);
        await store.SaveNodeExecutionAsync(execution.Id, originalNode, CancellationToken.None);
        var table = IsNodeEvent(eventType) ? "node_executions" : "workflow_executions";
        await using var connection = new NpgsqlConnection(_storeConnectionString);
        await connection.OpenAsync();
        // Only this test's isolated schema is affected. Existing snapshots remain valid/readable.
        await using var constraint = new NpgsqlCommand(
            $"ALTER TABLE {table} ADD CONSTRAINT reject_lifecycle_state CHECK (status = 'Pending') NOT VALID;",
            connection);
        await constraint.ExecuteNonQueryAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            store.ExecuteLifecycleAsync(execution.Id, async (transaction, token) =>
            {
                await transaction.AppendExecutionHistoryAsync(TransitionHistory(execution, node, eventType), token);
                await WriteTransitionState(transaction, eventType, node, token);
            }, CancellationToken.None));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        var aggregate = await store.GetExecutionAsync(execution.Id, CancellationToken.None);
        Assert.NotNull(aggregate);
        Assert.Equal(execution.Status, aggregate.Status);
        Assert.Equal(originalNode, Assert.Single(aggregate.Nodes));
        Assert.Empty(await store.GetExecutionHistoryAsync(execution.Id, CancellationToken.None));
    }
}
