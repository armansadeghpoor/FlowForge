using System.Collections.Concurrent;
using FlowForge.Abstractions.State;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Infrastructure.State;

/// <summary>
/// Stores workflow and node execution state in memory.
/// </summary>
public sealed class InMemoryStateStore : IStateStore
{
    private readonly ConcurrentDictionary<WorkflowExecutionId, WorkflowExecution> _executions = new();
    private readonly ConcurrentDictionary<(WorkflowExecutionId, NodeExecutionId), NodeExecutionState>
        _nodeExecutions = new();

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
    public Task SaveNodeExecutionAsync(
        WorkflowExecutionId executionId,
        NodeExecutionState nodeExecution,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nodeExecution);
        cancellationToken.ThrowIfCancellationRequested();

        _nodeExecutions[(executionId, nodeExecution.Id)] = nodeExecution with { };

        return Task.CompletedTask;
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
    public Task<NodeExecutionState?> GetNodeExecutionAsync(
        WorkflowExecutionId executionId,
        NodeExecutionId nodeExecutionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var nodeExecution = _nodeExecutions.TryGetValue((executionId, nodeExecutionId), out var stored)
            ? stored with { }
            : null;

        return Task.FromResult(nodeExecution);
    }

    private static WorkflowExecution Snapshot(WorkflowExecution execution) =>
        execution with
        {
            Nodes = Array.AsReadOnly(execution.Nodes.ToArray())
        };
}
