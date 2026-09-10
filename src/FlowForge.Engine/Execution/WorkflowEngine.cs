using FlowForge.Abstractions.Engine;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Executions;

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
        _workflowExecutor.ExecuteAsync(workflow, cancellationToken);
}
