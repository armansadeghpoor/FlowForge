using FlowForge.Abstractions.Queries;
using FlowForge.Abstractions.State;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.History;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Engine.Queries;

/// <summary>
/// Composes execution visibility models from persisted snapshots and history.
/// </summary>
public sealed class ExecutionQueryService : IExecutionQueryService
{
    private readonly IStateStore _stateStore;

    /// <summary>
    /// Initializes a new execution query service.
    /// </summary>
    /// <param name="stateStore">The state store used to read execution data.</param>
    public ExecutionQueryService(IStateStore stateStore)
    {
        ArgumentNullException.ThrowIfNull(stateStore);
        _stateStore = stateStore;
    }

    /// <inheritdoc />
    public async Task<ExecutionSummary?> GetSummaryAsync(
        WorkflowExecutionId executionId,
        CancellationToken cancellationToken)
    {
        var execution = await _stateStore.GetExecutionAsync(
            executionId,
            cancellationToken);
        if (execution is null)
        {
            return null;
        }

        return CreateSummary(execution);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionSummary>> FindExecutionsByCorrelationIdAsync(
        ExecutionCorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var executions = await _stateStore.FindExecutionsByCorrelationIdAsync(
            correlationId,
            cancellationToken);
        return Array.AsReadOnly(executions.Select(CreateSummary).ToArray());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ExecutionHistoryEntry>> GetTimelineAsync(
        WorkflowExecutionId executionId,
        CancellationToken cancellationToken) =>
        _stateStore.GetExecutionHistoryAsync(executionId, cancellationToken);

    private static ExecutionSummary CreateSummary(
        FlowForge.Core.Domain.Executions.WorkflowExecution execution) =>
        new()
        {
            WorkflowExecutionId = execution.Id,
            CorrelationId = execution.CorrelationId,
            Status = execution.Status,
            DefinitionVersion = execution.DefinitionVersion,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            OwnerId = execution.OwnerId,
            LastHeartbeatAt = execution.LastHeartbeatAt,
            NodeExecutionCounts = new NodeExecutionCounts
            {
                Total = execution.Nodes.Count,
                Pending = execution.Nodes.Count(
                    node => node.Status == NodeExecutionStatus.Pending),
                Running = execution.Nodes.Count(
                    node => node.Status == NodeExecutionStatus.Running),
                Succeeded = execution.Nodes.Count(
                    node => node.Status == NodeExecutionStatus.Succeeded),
                Failed = execution.Nodes.Count(
                    node => node.Status == NodeExecutionStatus.Failed),
                Cancelled = execution.Nodes.Count(
                    node => node.Status == NodeExecutionStatus.Cancelled)
            }
        };
}
