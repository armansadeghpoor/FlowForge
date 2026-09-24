using FlowForge.Application.Common;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;

namespace FlowForge.Application.Triggers;

/// <summary>
/// Defines workflow trigger management use cases.
/// </summary>
public interface IWorkflowTriggerService
{
    /// <summary>
    /// Validates and creates a workflow trigger.
    /// </summary>
    Task<ApplicationResult<WorkflowTrigger>> CreateAsync(
        WorkflowTrigger trigger,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a workflow trigger.
    /// </summary>
    Task<ApplicationResult<WorkflowTrigger?>> GetAsync(
        WorkflowTriggerId id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists all workflow triggers.
    /// </summary>
    Task<ApplicationResult<IReadOnlyList<WorkflowTrigger>>> ListAsync(
        CancellationToken cancellationToken);
}
