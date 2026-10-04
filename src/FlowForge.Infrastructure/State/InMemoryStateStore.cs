using System.Collections.Concurrent;
using FlowForge.Abstractions.Queries;
using FlowForge.Abstractions.State;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.History;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Infrastructure.State;

/// <summary>Stores workflow state and history as immutable in-memory snapshots.</summary>
public sealed class InMemoryStateStore : IStateStore, IExecutionSnapshotQuery
{
    private readonly ConcurrentDictionary<WorkflowExecutionId, ExecutionSlot> _executions = new();

    /// <inheritdoc />
    public Task ExecuteLifecycleAsync(
        WorkflowExecutionId executionId,
        Func<IExecutionLifecycleTransaction, CancellationToken, Task> transition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transition);
        return MutateAsync(executionId, transaction => transition(transaction, cancellationToken), cancellationToken);
    }

    private async Task MutateAsync(
        WorkflowExecutionId id,
        Func<LifecycleTransaction, Task> mutation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var slot = _executions.GetOrAdd(id, static _ => new ExecutionSlot());
        await slot.Gate.WaitAsync(cancellationToken);
        var transaction = new LifecycleTransaction(id, slot.Snapshot);
        try
        {
            await mutation(transaction);
            cancellationToken.ThrowIfCancellationRequested();
            // Publish the complete state/history pair in one reference assignment.
            slot.Snapshot = transaction.Snapshot;
        }
        finally
        {
            transaction.Close();
            slot.Gate.Release();
        }
    }

    /// <inheritdoc />
    public Task CreateExecutionAsync(WorkflowExecution execution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execution);
        return ExecuteLifecycleAsync(execution.Id,
            (transaction, token) => transaction.CreateExecutionAsync(execution, token), cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateWorkflowStatusAsync(
        WorkflowExecutionId id, WorkflowExecutionStatus status, DateTime? startedAt,
        DateTime? completedAt, CancellationToken cancellationToken) =>
        ExecuteLifecycleAsync(id,
            (transaction, token) => transaction.UpdateWorkflowStatusAsync(status, startedAt, completedAt, token),
            cancellationToken);

    /// <inheritdoc />
    public Task SaveNodeExecutionAsync(
        WorkflowExecutionId executionId, NodeExecutionState nodeExecution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nodeExecution);
        return ExecuteLifecycleAsync(executionId,
            (transaction, token) => transaction.SaveNodeExecutionAsync(nodeExecution, token), cancellationToken);
    }

    /// <inheritdoc />
    public Task AppendExecutionHistoryAsync(ExecutionHistoryEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return ExecuteLifecycleAsync(entry.WorkflowExecutionId,
            (transaction, token) => transaction.AppendExecutionHistoryAsync(entry, token), cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateHeartbeatAsync(
        WorkflowExecutionId id, DateTime lastHeartbeatAt, CancellationToken cancellationToken) =>
        MutateAsync(id, transaction =>
        {
            transaction.SetExecution(transaction.GetExecution() with { LastHeartbeatAt = lastHeartbeatAt });
            return Task.CompletedTask;
        }, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> TryClaimExecutionAsync(
        WorkflowExecutionId id, string ownerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        var claimed = false;
        await MutateAsync(id, transaction =>
        {
            var execution = transaction.GetExecution();
            if (execution.Status == WorkflowExecutionStatus.Running && execution.OwnerId is null)
            {
                transaction.SetExecution(execution with { OwnerId = ownerId });
                claimed = true;
            }

            return Task.CompletedTask;
        }, cancellationToken);
        return claimed;
    }

    /// <inheritdoc />
    public Task<WorkflowExecution?> GetExecutionAsync(WorkflowExecutionId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var execution = _executions.TryGetValue(id, out var slot) ? slot.Snapshot.Execution : null;
        return Task.FromResult(execution is null ? null : Snapshot(execution));
    }

    /// <inheritdoc />
    public async Task<NodeExecutionState?> GetNodeExecutionAsync(
        WorkflowExecutionId executionId, NodeExecutionId nodeExecutionId, CancellationToken cancellationToken)
    {
        var execution = await GetExecutionAsync(executionId, cancellationToken);
        return execution?.Nodes.FirstOrDefault(node => node.Id == nodeExecutionId) is { } node ? node with { } : null;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ExecutionHistoryEntry>> GetExecutionHistoryAsync(
        WorkflowExecutionId executionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var history = _executions.TryGetValue(executionId, out var slot)
            ? slot.Snapshot.History : Array.Empty<ExecutionHistoryEntry>();
        // OrderBy is stable, retaining append order when timestamps are equal.
        return Task.FromResult<IReadOnlyList<ExecutionHistoryEntry>>(
            Array.AsReadOnly(history.OrderBy(entry => entry.Timestamp).Select(Snapshot).ToArray()));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowExecution>> FindStaleExecutionsAsync(
        DateTime threshold, CancellationToken cancellationToken) =>
        ReadExecutions(executions => executions.Where(execution =>
            execution.Status == WorkflowExecutionStatus.Running &&
            (execution.LastHeartbeatAt is null || execution.LastHeartbeatAt < threshold)), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowExecution>> ListExecutionsAsync(CancellationToken cancellationToken) =>
        ReadExecutions(executions => executions.OrderBy(execution => execution.StartedAt ?? DateTime.MaxValue)
            .ThenBy(execution => execution.Id.Value), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowExecution>> FindExecutionsByCorrelationIdAsync(
        ExecutionCorrelationId correlationId, CancellationToken cancellationToken) =>
        ReadExecutions(executions => executions.Where(execution => execution.CorrelationId == correlationId)
            .OrderBy(execution => execution.CreatedAt).ThenBy(execution => execution.Id.Value), cancellationToken);

    private Task<IReadOnlyList<WorkflowExecution>> ReadExecutions(
        Func<IEnumerable<WorkflowExecution>, IEnumerable<WorkflowExecution>> query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var executions = _executions.Values.Select(slot => slot.Snapshot.Execution).OfType<WorkflowExecution>();
        return Task.FromResult<IReadOnlyList<WorkflowExecution>>(
            Array.AsReadOnly(query(executions).Select(Snapshot).ToArray()));
    }

    private static WorkflowExecution Snapshot(WorkflowExecution execution) =>
        execution with { Nodes = Array.AsReadOnly(execution.Nodes.ToArray()) };

    private static ExecutionHistoryEntry Snapshot(ExecutionHistoryEntry entry) =>
        entry with { Metadata = entry.Metadata is { } metadata ? metadata.Clone() : null };

    private sealed record ExecutionSnapshot(WorkflowExecution? Execution, ExecutionHistoryEntry[] History);

    private sealed class ExecutionSlot
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public volatile ExecutionSnapshot Snapshot = new(null, []);
    }

    private sealed class LifecycleTransaction(WorkflowExecutionId id, ExecutionSnapshot snapshot)
        : IExecutionLifecycleTransaction
    {
        private bool _active = true;
        public ExecutionSnapshot Snapshot { get; private set; } = snapshot;

        public void Close() => _active = false;

        private void Check(CancellationToken token)
        {
            if (!_active)
            {
                throw new InvalidOperationException("The lifecycle transaction has ended.");
            }
            token.ThrowIfCancellationRequested();
        }

        public WorkflowExecution GetExecution() => Snapshot.Execution ??
            throw new KeyNotFoundException($"Workflow execution '{id.Value}' was not found.");

        public void SetExecution(WorkflowExecution execution) => Snapshot = Snapshot with { Execution = execution };

        public Task CreateExecutionAsync(WorkflowExecution execution, CancellationToken cancellationToken)
        {
            Check(cancellationToken);
            ArgumentNullException.ThrowIfNull(execution);
            if (execution.Id != id)
            {
                throw new ArgumentException("Execution does not belong to this transaction.", nameof(execution));
            }
            if (Snapshot.Execution is not null)
            {
                throw new InvalidOperationException($"Workflow execution '{id.Value}' already exists.");
            }
            SetExecution(InMemoryStateStore.Snapshot(execution));
            return Task.CompletedTask;
        }

        public Task UpdateWorkflowStatusAsync(WorkflowExecutionStatus status, DateTime? startedAt,
            DateTime? completedAt, CancellationToken cancellationToken)
        {
            Check(cancellationToken);
            SetExecution(GetExecution() with { Status = status, StartedAt = startedAt, CompletedAt = completedAt });
            return Task.CompletedTask;
        }

        public Task SaveNodeExecutionAsync(NodeExecutionState nodeExecution, CancellationToken cancellationToken)
        {
            Check(cancellationToken);
            ArgumentNullException.ThrowIfNull(nodeExecution);
            var execution = GetExecution();
            var nodes = execution.Nodes.ToList();
            var index = nodes.FindIndex(node => node.Id == nodeExecution.Id);
            if (index >= 0)
            {
                nodes[index] = nodeExecution with { };
            }
            else
            {
                nodes.Add(nodeExecution with { });
            }
            SetExecution(execution with { Nodes = Array.AsReadOnly(nodes.ToArray()) });
            return Task.CompletedTask;
        }

        public Task AppendExecutionHistoryAsync(ExecutionHistoryEntry entry, CancellationToken cancellationToken)
        {
            Check(cancellationToken);
            ArgumentNullException.ThrowIfNull(entry);
            if (entry.WorkflowExecutionId != id)
            {
                throw new ArgumentException("History does not belong to this transaction.", nameof(entry));
            }
            _ = GetExecution();
            var cloned = InMemoryStateStore.Snapshot(entry);
            Snapshot = Snapshot with { History = [.. Snapshot.History, cloned] };
            return Task.CompletedTask;
        }
    }
}
