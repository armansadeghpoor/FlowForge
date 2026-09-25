using FlowForge.Abstractions.Engine;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Engine.Execution;

/// <summary>
/// Provides the workflow execution boundary.
/// </summary>
public sealed class WorkflowEngine : IWorkflowEngine
{
    private readonly WorkflowExecutor _workflowExecutor;

    /// <summary>
    /// Initializes a new workflow engine.
    /// </summary>
    /// <param name="workflowExecutor">The workflow executor.</param>
    public WorkflowEngine(WorkflowExecutor workflowExecutor)
    {
        ArgumentNullException.ThrowIfNull(workflowExecutor);
        _workflowExecutor = workflowExecutor;
    }

    /// <inheritdoc />
    public Task<WorkflowExecution> ExecuteAsync(
        WorkflowDefinition workflow,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            workflow,
            new WorkflowExecutionRequest
            {
                ExecutionRequestId = new ExecutionRequestId(Guid.NewGuid()),
                CorrelationId = new ExecutionCorrelationId(Guid.NewGuid())
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<WorkflowExecution> ExecuteAsync(
        WorkflowDefinition workflow,
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken) =>
        _workflowExecutor.ExecuteAsync(workflow, request, cancellationToken);
}
