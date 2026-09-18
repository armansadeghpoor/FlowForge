using FlowForge.Abstractions.Execution;
using FlowForge.Abstractions.Nodes;
using FlowForge.Abstractions.State;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Failures;
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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);

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
            WorkflowId = workflow.Id,
            Status = WorkflowExecutionStatus.Running,
            CreatedAt = startedAt,
            StartedAt = startedAt,
            CompletedAt = null,
            OwnerId = null,
            LastHeartbeatAt = null,
            Nodes = Array.Empty<NodeExecutionState>()
        };
        await _stateStore.CreateExecutionAsync(execution, cancellationToken);

        var nodesById = workflow.Nodes.ToDictionary(node => node.Id);
        var nodeStates = new List<NodeExecutionState>();

        foreach (var layer in TopologicalSorter.Sort(graph))
        {
            var layerTasks = layer
                .Select(nodeId => ExecuteNodeAsync(
                    nodesById[nodeId],
                    execution.Id,
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
                    cancellationToken);
            }
        }

        return await CompleteAsync(
            execution,
            WorkflowExecutionStatus.Succeeded,
            nodeStates,
            cancellationToken);
    }

    private async Task<NodeExecutionState> ExecuteNodeAsync(
        NodeDefinition node,
        WorkflowExecutionId executionId,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var context = new NodeExecutionContext
        {
            NodeDefinition = node,
            WorkflowExecutionId = executionId,
            NodeExecutionId = new NodeExecutionId(Guid.NewGuid()),
            AttemptNumber = 1
        };
        var nodeExecution = new NodeExecutionState
        {
            Id = context.NodeExecutionId,
            NodeId = node.Id,
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

        var runner = _nodeRunnerRegistry.Get(node.Type);
        if (runner is null)
        {
            var failedNodeExecution = nodeExecution with
            {
                Status = NodeExecutionStatus.Failed,
                CompletedAt = DateTime.UtcNow,
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

        var completedNodeExecution = nodeExecution with
        {
            Status = result.Success
                ? NodeExecutionStatus.Succeeded
                : NodeExecutionStatus.Failed,
            CompletedAt = DateTime.UtcNow,
            Output = result.Output,
            Failure = result.Failure
        };
        await _stateStore.SaveNodeExecutionAsync(
            executionId,
            completedNodeExecution,
            cancellationToken);

        return completedNodeExecution;
    }

    private async Task<WorkflowExecution> CompleteAsync(
        WorkflowExecution execution,
        WorkflowExecutionStatus status,
        IEnumerable<NodeExecutionState> nodeStates,
        CancellationToken cancellationToken)
    {
        var completedAt = DateTime.UtcNow;
        await _stateStore.UpdateWorkflowStatusAsync(
            execution.Id,
            status,
            execution.StartedAt,
            completedAt,
            cancellationToken);

        return execution with
        {
            Status = status,
            CompletedAt = completedAt,
            Nodes = Array.AsReadOnly(nodeStates.ToArray())
        };
    }
}
