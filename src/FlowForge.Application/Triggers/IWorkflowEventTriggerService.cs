using FlowForge.Application.Common;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;

namespace FlowForge.Application.Triggers;

/// <summary>
/// Defines workflow event trigger management use cases.
/// </summary>
public interface IWorkflowEventTriggerService
{
    /// <summary>
    /// Validates and creates a workflow event trigger.
    /// </summary>
    Task<ApplicationResult<WorkflowEventTrigger>> CreateAsync(
        WorkflowEventTrigger eventTrigger,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a workflow event trigger.
    /// </summary>
    Task<ApplicationResult<WorkflowEventTrigger?>> GetAsync(
        WorkflowEventTriggerId id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists all workflow event triggers.
    /// </summary>
    Task<ApplicationResult<IReadOnlyList<WorkflowEventTrigger>>> ListAsync(
        CancellationToken cancellationToken);
}
