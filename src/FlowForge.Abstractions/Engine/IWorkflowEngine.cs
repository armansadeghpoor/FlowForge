using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Executions;

namespace FlowForge.Abstractions.Engine;

/// <summary>
/// Defines the contract for executing workflows.
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Executes the supplied workflow definition.
    /// </summary>
    /// <param name="workflow">The workflow definition to execute.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The resulting workflow execution snapshot.</returns>
    Task<WorkflowExecution> ExecuteAsync(
        WorkflowDefinition workflow,
        CancellationToken cancellationToken);
}
