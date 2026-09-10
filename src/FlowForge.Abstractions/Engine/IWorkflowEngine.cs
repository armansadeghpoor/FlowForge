using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Executions;

namespace FlowForge.Abstractions.Engine;

/// <summary>
/// Defines the contract for starting workflow executions.
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Starts an execution of the supplied workflow definition.
    /// </summary>
    /// <param name="workflow">The workflow definition to execute.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The created workflow execution.</returns>
    Task<WorkflowExecution> StartAsync(
        WorkflowDefinition workflow,
        CancellationToken cancellationToken);
}
