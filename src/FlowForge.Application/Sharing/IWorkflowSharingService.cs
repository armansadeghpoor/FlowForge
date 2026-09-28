using FlowForge.Application.Common;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Sharing;

namespace FlowForge.Application.Sharing;

/// <summary>
/// Defines workflow sharing management and visibility use cases.
/// </summary>
public interface IWorkflowSharingService
{
    /// <summary>Creates immutable sharing metadata.</summary>
    Task<ApplicationResult<WorkflowSharing>> CreateAsync(
        WorkflowSharing sharing,
        CancellationToken cancellationToken);

    /// <summary>Gets sharing metadata when visible to the current operation.</summary>
    Task<ApplicationResult<WorkflowSharing?>> GetAsync(
        WorkflowDefinitionId workflowDefinitionId,
        string definitionVersion,
        CancellationToken cancellationToken);

    /// <summary>Resolves whether the current operation may view a workflow.</summary>
    Task<ApplicationResult<bool>> ResolveVisibilityAsync(
        WorkflowDefinitionId workflowDefinitionId,
        string definitionVersion,
        CancellationToken cancellationToken);
}
