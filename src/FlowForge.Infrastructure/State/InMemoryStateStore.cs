using System.Collections.Concurrent;
using FlowForge.Abstractions.State;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.History;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Infrastructure.State;

/// <summary>
/// Stores workflow and node execution state in memory.
/// </summary>
public sealed class InMemoryStateStore : IStateStore
{
    private readonly ConcurrentDictionary<WorkflowExecutionId, WorkflowExecution> _executions = new();
    private readonly ConcurrentDictionary<
        WorkflowExecutionId,
        ConcurrentQueue<ExecutionHistoryEntry>> _history = new();

    /// <inheritdoc />
    public Task CreateExecutionAsync(
        WorkflowExecution execution,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execution);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_executions.TryAdd(execution.Id, Snapshot(execution)))
        {
            throw new InvalidOperationException(
                $"Workflow execution '{execution.Id.Value}' already exists.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateWorkflowStatusAsync(
        WorkflowExecutionId id,
        WorkflowExecutionStatus status,
        DateTime? startedAt,
        DateTime? completedAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while (_executions.TryGetValue(id, out var current))
        {
            var updated = current with
            {
                Status = status,
                StartedAt = startedAt,
                CompletedAt = completedAt
            };

            if (_executions.TryUpdate(id, updated, current))
            {
                return Task.CompletedTask;
            }
        }

        throw new KeyNotFoundException(
            $"Workflow execution '{id.Value}' was not found.");
    }

    /// <inheritdoc />
    public Task UpdateHeartbeatAsync(
        WorkflowExecutionId id,
        DateTime lastHeartbeatAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while (_executions.TryGetValue(id, out var current))
        {
            var updated = current with { LastHeartbeatAt = lastHeartbeatAt };
            if (_executions.TryUpdate(id, updated, current))
            {
                return Task.CompletedTask;
            }
        }

        throw new KeyNotFoundException(
            $"Workflow execution '{id.Value}' was not found.");
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowExecution>> FindStaleExecutionsAsync(
        DateTime threshold,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<WorkflowExecution> executions = Array.AsReadOnly(
            _executions.Values
                .Where(execution =>
                    execution.Status == WorkflowExecutionStatus.Running &&
                    (execution.LastHeartbeatAt is null ||
                     execution.LastHeartbeatAt < threshold))
                .Select(Snapshot)
                .ToArray());

        return Task.FromResult(executions);
    }

    /// <inheritdoc />
    public Task<bool> TryClaimExecutionAsync(
        WorkflowExecutionId id,
        string ownerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        cancellationToken.ThrowIfCancellationRequested();

        while (_executions.TryGetValue(id, out var current))
        {
            if (current.Status != WorkflowExecutionStatus.Running || current.OwnerId is not null)
            {
                return Task.FromResult(false);
            }

            var claimed = current with { OwnerId = ownerId };
            if (_executions.TryUpdate(id, claimed, current))
            {
                return Task.FromResult(true);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        throw new KeyNotFoundException(
            $"Workflow execution '{id.Value}' was not found.");
    }

    /// <inheritdoc />
    public Task AppendExecutionHistoryAsync(
        ExecutionHistoryEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_executions.ContainsKey(entry.WorkflowExecutionId))
        {
            throw new KeyNotFoundException(
                $"Workflow execution '{entry.WorkflowExecutionId.Value}' was not found.");
        }

        var history = _history.GetOrAdd(
            entry.WorkflowExecutionId,
            static _ => new ConcurrentQueue<ExecutionHistoryEntry>());
        history.Enqueue(Snapshot(entry));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ExecutionHistoryEntry>> GetExecutionHistoryAsync(
        WorkflowExecutionId executionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_history.TryGetValue(executionId, out var history))
        {
            return Task.FromResult<IReadOnlyList<ExecutionHistoryEntry>>(
                Array.Empty<ExecutionHistoryEntry>());
        }

        IReadOnlyList<ExecutionHistoryEntry> entries = Array.AsReadOnly(
            history
                .ToArray()
                .Select((entry, appendOrder) => new
                {
                    Entry = Snapshot(entry),
                    AppendOrder = appendOrder
                })
                .OrderBy(item => item.Entry.Timestamp)
                .ThenBy(item => item.AppendOrder)
                .Select(item => item.Entry)
                .ToArray());

        return Task.FromResult(entries);
    }

    /// <inheritdoc />
    public Task SaveNodeExecutionAsync(
        WorkflowExecutionId executionId,
        NodeExecutionState nodeExecution,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nodeExecution);
        cancellationToken.ThrowIfCancellationRequested();

        while (_executions.TryGetValue(executionId, out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nodes = current.Nodes.ToList();
            var index = nodes.FindIndex(node => node.Id == nodeExecution.Id);
            var snapshot = nodeExecution with { };
            if (index >= 0)
            {
                nodes[index] = snapshot;
            }
            else
            {
                nodes.Add(snapshot);
            }

            var updated = current with { Nodes = Array.AsReadOnly(nodes.ToArray()) };
            if (_executions.TryUpdate(executionId, updated, current))
            {
                return Task.CompletedTask;
            }
        }

        throw new KeyNotFoundException(
            $"Workflow execution '{executionId.Value}' was not found.");
    }

    /// <inheritdoc />
    public Task<WorkflowExecution?> GetExecutionAsync(
        WorkflowExecutionId id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var execution = _executions.TryGetValue(id, out var stored)
            ? Snapshot(stored)
            : null;

        return Task.FromResult(execution);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowExecution>> FindExecutionsByCorrelationIdAsync(
        ExecutionCorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<WorkflowExecution> executions = Array.AsReadOnly(
            _executions.Values
                .Where(execution => execution.CorrelationId == correlationId)
                .OrderBy(execution => execution.CreatedAt)
                .ThenBy(execution => execution.Id.Value)
                .Select(Snapshot)
                .ToArray());
        return Task.FromResult(executions);
    }

    /// <inheritdoc />
    public Task<NodeExecutionState?> GetNodeExecutionAsync(
        WorkflowExecutionId executionId,
        NodeExecutionId nodeExecutionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stored = _executions.TryGetValue(executionId, out var execution)
            ? execution.Nodes.FirstOrDefault(node => node.Id == nodeExecutionId)
            : null;
        var nodeExecution = stored is null ? null : stored with { };

        return Task.FromResult(nodeExecution);
    }

    private static WorkflowExecution Snapshot(WorkflowExecution execution) =>
        execution with
        {
            Nodes = Array.AsReadOnly(execution.Nodes.ToArray())
        };

    private static ExecutionHistoryEntry Snapshot(ExecutionHistoryEntry entry) =>
        entry with
        {
            Metadata = entry.Metadata is { } metadata ? metadata.Clone() : null
        };
}
