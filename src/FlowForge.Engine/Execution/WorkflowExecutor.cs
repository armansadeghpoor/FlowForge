using System.Text.Json;
using FlowForge.Abstractions.Engine;
using FlowForge.Abstractions.Execution;
using FlowForge.Abstractions.Nodes;
using FlowForge.Abstractions.State;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Failures;
using FlowForge.Core.Domain.History;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Graph;

namespace FlowForge.Engine.Execution;

/// <summary>
/// Executes validated workflow definitions in dependency-ordered layers.
/// </summary>
public sealed class WorkflowExecutor
{
    private readonly INodeRunnerRegistry _nodeRunnerRegistry;
    private readonly IStateStore _stateStore;
    private readonly ExecutionPipeline _executionPipeline;

    /// <summary>
    /// Initializes a new workflow executor.
    /// </summary>
    /// <param name="nodeRunnerRegistry">The registry used to resolve node runners.</param>
    /// <param name="stateStore">The store used to persist execution state.</param>
    /// <param name="executionPipeline">The pipeline used to execute node runners.</param>
    public WorkflowExecutor(
        INodeRunnerRegistry nodeRunnerRegistry,
        IStateStore stateStore,
        ExecutionPipeline executionPipeline)
    {
        ArgumentNullException.ThrowIfNull(nodeRunnerRegistry);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(executionPipeline);
        _nodeRunnerRegistry = nodeRunnerRegistry;
        _stateStore = stateStore;
        _executionPipeline = executionPipeline;
    }

    /// <summary>
    /// Executes a workflow definition and returns its terminal execution snapshot.
    /// </summary>
    /// <param name="workflow">The workflow definition to execute.</param>
    /// <param name="cancellationToken">A token used to cancel node execution.</param>
    /// <returns>The resulting workflow execution snapshot.</returns>
    public async Task<WorkflowExecution> ExecuteAsync(
        WorkflowDefinition workflow,
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(request);

        if (!WorkflowGraph.TryCreate(workflow, out var graph, out var validationResult))
        {
            var validationDetails = string.Join(
                Environment.NewLine,
                validationResult.Errors.Select(error => $"{error.ErrorType}: {error.Message}"));

            throw new InvalidOperationException(
                $"Workflow graph validation failed:{Environment.NewLine}{validationDetails}");
        }

        var startedAt = DateTime.UtcNow;
        var execution = new WorkflowExecution
        {
            Id = new WorkflowExecutionId(Guid.NewGuid()),
            WorkflowId = new WorkflowId(workflow.Id.Value),
            CorrelationId = request.CorrelationId,
            DefinitionVersion = workflow.Version,
            Status = WorkflowExecutionStatus.Running,
            CreatedAt = startedAt,
            StartedAt = startedAt,
            CompletedAt = null,
            OwnerId = null,
            LastHeartbeatAt = null,
            Nodes = Array.Empty<NodeExecutionState>()
        };
        await _stateStore.CreateExecutionAsync(execution, cancellationToken);
        await AppendHistoryAsync(
            execution.Id,
            null,
            ExecutionHistoryEventType.WorkflowCreated,
            execution.CreatedAt,
            request,
            cancellationToken);
        await AppendHistoryAsync(
            execution.Id,
            null,
            ExecutionHistoryEventType.WorkflowStarted,
            startedAt,
            request,
            cancellationToken);

        var nodesById = workflow.Nodes.ToDictionary(node => node.Id);
        var nodeStates = new List<NodeExecutionState>();

        foreach (var layer in TopologicalSorter.Sort(graph))
        {
            var layerTasks = layer
                .Select(nodeId => ExecuteNodeAsync(
                    nodesById[nodeId],
                    execution.Id,
                    request,
                    cancellationToken))
                .ToArray();
            var layerStates = await Task.WhenAll(layerTasks);

            nodeStates.AddRange(layerStates);

            if (layerStates.Any(state => state.Status == NodeExecutionStatus.Failed))
            {
                return await CompleteAsync(
                    execution,
                    WorkflowExecutionStatus.Failed,
                    nodeStates,
                    request,
                    cancellationToken);
            }
        }

        return await CompleteAsync(
            execution,
            WorkflowExecutionStatus.Succeeded,
            nodeStates,
            request,
            cancellationToken);
    }

    private async Task<NodeExecutionState> ExecuteNodeAsync(
        NodeDefinition node,
        WorkflowExecutionId executionId,
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var context = new NodeExecutionContext
        {
            NodeDefinition = node,
            WorkflowExecutionId = executionId,
            CorrelationId = request.CorrelationId,
            NodeExecutionId = new NodeExecutionId(Guid.NewGuid()),
            AttemptNumber = 1
        };
        var nodeExecution = new NodeExecutionState
        {
            Id = context.NodeExecutionId,
            NodeId = node.Id,
            CorrelationId = request.CorrelationId,
            Status = NodeExecutionStatus.Running,
            RetryCount = 0,
            AttemptNumber = context.AttemptNumber,
            StartedAt = startedAt,
            CompletedAt = null,
            Output = null,
            Failure = null
        };
        await _stateStore.SaveNodeExecutionAsync(
            executionId,
            nodeExecution,
            cancellationToken);
        await AppendHistoryAsync(
            executionId,
            nodeExecution.Id,
            ExecutionHistoryEventType.NodeStarted,
            startedAt,
            request,
            cancellationToken);

        var runner = _nodeRunnerRegistry.Get(node.Type);
        if (runner is null)
        {
            var missingRunnerCompletedAt = DateTime.UtcNow;
            var failedNodeExecution = nodeExecution with
            {
                Status = NodeExecutionStatus.Failed,
                CompletedAt = missingRunnerCompletedAt,
                Failure = new NodeFailure
                {
                    Category = NodeFailureCategory.Configuration,
                    Message = $"No node runner is registered for node type '{node.Type}'."
                }
            };
            await _stateStore.SaveNodeExecutionAsync(
                executionId,
                failedNodeExecution,
                cancellationToken);
            await AppendHistoryAsync(
                executionId,
                failedNodeExecution.Id,
                ExecutionHistoryEventType.NodeFailed,
                missingRunnerCompletedAt,
                request,
                cancellationToken);

            return failedNodeExecution;
        }

        var result = await _executionPipeline.ExecuteAsync(
            context,
            async (currentContext, token) =>
            {
                if (nodeExecution.AttemptNumber != currentContext.AttemptNumber)
                {
                    nodeExecution = nodeExecution with
                    {
                        AttemptNumber = currentContext.AttemptNumber,
                        RetryCount = currentContext.AttemptNumber - 1
                    };
                    await _stateStore.SaveNodeExecutionAsync(executionId, nodeExecution, token);
                }

                return await runner.ExecuteAsync(currentContext, token);
            },
            cancellationToken);

        var completedAt = DateTime.UtcNow;
        var completedNodeExecution = nodeExecution with
        {
            Status = result.Success
                ? NodeExecutionStatus.Succeeded
                : NodeExecutionStatus.Failed,
            CompletedAt = completedAt,
            Output = result.Output,
            Failure = result.Failure
        };
        await _stateStore.SaveNodeExecutionAsync(
            executionId,
            completedNodeExecution,
            cancellationToken);
        await AppendHistoryAsync(
            executionId,
            completedNodeExecution.Id,
            result.Success
                ? ExecutionHistoryEventType.NodeCompleted
                : ExecutionHistoryEventType.NodeFailed,
            completedAt,
            request,
            cancellationToken);

        return completedNodeExecution;
    }

    private async Task<WorkflowExecution> CompleteAsync(
        WorkflowExecution execution,
        WorkflowExecutionStatus status,
        IEnumerable<NodeExecutionState> nodeStates,
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var completedAt = DateTime.UtcNow;
        await _stateStore.UpdateWorkflowStatusAsync(
            execution.Id,
            status,
            execution.StartedAt,
            completedAt,
            cancellationToken);
        await AppendHistoryAsync(
            execution.Id,
            null,
            status switch
            {
                WorkflowExecutionStatus.Succeeded =>
                    ExecutionHistoryEventType.WorkflowCompleted,
                WorkflowExecutionStatus.Failed =>
                    ExecutionHistoryEventType.WorkflowFailed,
                _ => throw new InvalidOperationException(
                    $"Cannot record completion history for workflow status '{status}'.")
            },
            completedAt,
            request,
            cancellationToken);

        return execution with
        {
            Status = status,
            CompletedAt = completedAt,
            Nodes = Array.AsReadOnly(nodeStates.ToArray())
        };
    }

    private Task AppendHistoryAsync(
        WorkflowExecutionId workflowExecutionId,
        NodeExecutionId? nodeExecutionId,
        ExecutionHistoryEventType eventType,
        DateTime timestamp,
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken) =>
        _stateStore.AppendExecutionHistoryAsync(
            new ExecutionHistoryEntry
            {
                Id = new ExecutionHistoryId(Guid.NewGuid()),
                WorkflowExecutionId = workflowExecutionId,
                NodeExecutionId = nodeExecutionId,
                EventType = eventType,
                Timestamp = timestamp,
                Metadata = JsonSerializer.SerializeToElement(new
                {
                    correlationId = request.CorrelationId.Value,
                    executionRequestId = request.ExecutionRequestId.Value
                })
            },
            cancellationToken);
}
