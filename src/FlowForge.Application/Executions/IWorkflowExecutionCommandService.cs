using FlowForge.Abstractions.Triggers;
using FlowForge.Application.Common;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Executions;

/// <summary>
/// Defines workflow execution command use cases.
/// </summary>
public interface IWorkflowExecutionCommandService
{
    /// <summary>
    /// Executes a workflow trigger.
    /// </summary>
    Task<ApplicationResult<WorkflowExecutionId>> ExecuteTriggerAsync(
        WorkflowTriggerExecutionContext context,
        CancellationToken cancellationToken);
}
