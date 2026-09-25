using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Triggers;

/// <summary>
/// Defines the runtime boundary for executing workflow triggers.
/// </summary>
public interface IWorkflowTriggerExecutor
{
    /// <summary>
    /// Executes the trigger identified by the supplied context.
    /// </summary>
    /// <param name="context">The trigger execution context.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The created workflow execution identifier.</returns>
    Task<WorkflowExecutionId> ExecuteAsync(
        WorkflowTriggerExecutionContext context,
        CancellationToken cancellationToken);
}
