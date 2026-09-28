using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Sharing;

namespace FlowForge.Abstractions.Sharing;

/// <summary>
/// Defines provider-independent immutable workflow sharing persistence.
/// </summary>
public interface IWorkflowSharingStore
{
    /// <summary>Saves sharing metadata for a workflow definition version.</summary>
    Task SaveAsync(
        WorkflowSharing sharing,
        CancellationToken cancellationToken);

    /// <summary>Gets sharing metadata for an exact workflow definition version.</summary>
    Task<WorkflowSharing?> GetAsync(
        WorkflowDefinitionId workflowDefinitionId,
        string definitionVersion,
        CancellationToken cancellationToken);
}
