using FlowForge.Abstractions.Nodes;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Graph;

namespace FlowForge.Engine.Execution;

/// <summary>
/// Executes validated workflow definitions in dependency-ordered layers.
/// </summary>
public sealed class WorkflowExecutor
{
    private readonly INodeRunnerRegistry _nodeRunnerRegistry;

    /// <summary>
    /// Initializes a new workflow executor.
    /// </summary>
    /// <param name="nodeRunnerRegistry">The registry used to resolve node runners.</param>
    public WorkflowExecutor(INodeRunnerRegistry nodeRunnerRegistry)
    {
        ArgumentNullException.ThrowIfNull(nodeRunnerRegistry);
        _nodeRunnerRegistry = nodeRunnerRegistry;
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
            Nodes = Array.Empty<NodeExecutionState>()
        };
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
                return Complete(
                    execution,
                    WorkflowExecutionStatus.Failed,
                    nodeStates);
            }
        }

        return Complete(
            execution,
            WorkflowExecutionStatus.Succeeded,
            nodeStates);
    }

    private async Task<NodeExecutionState> ExecuteNodeAsync(
        NodeDefinition node,
        WorkflowExecutionId executionId,
        CancellationToken cancellationToken)
    {
        var runner = _nodeRunnerRegistry.Get(node.Type);

        if (runner is null)
        {
            return new NodeExecutionState
            {
                Id = new NodeExecutionId(Guid.NewGuid()),
                NodeId = node.Id,
                Status = NodeExecutionStatus.Failed,
                RetryCount = 0,
                StartedAt = null,
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = $"No node runner is registered for node type '{node.Type}'."
            };
        }

        var startedAt = DateTime.UtcNow;
        var result = await runner.ExecuteAsync(
            new NodeExecutionContext
            {
                Node = node,
                ExecutionId = executionId
            },
            cancellationToken);

        return new NodeExecutionState
        {
            Id = new NodeExecutionId(Guid.NewGuid()),
            NodeId = node.Id,
            Status = result.Success
                ? NodeExecutionStatus.Succeeded
                : NodeExecutionStatus.Failed,
            RetryCount = 0,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = result.Success ? null : result.ErrorMessage
        };
    }

    private static WorkflowExecution Complete(
        WorkflowExecution execution,
        WorkflowExecutionStatus status,
        IEnumerable<NodeExecutionState> nodeStates) =>
        execution with
        {
            Status = status,
            CompletedAt = DateTime.UtcNow,
            Nodes = Array.AsReadOnly(nodeStates.ToArray())
        };
}
