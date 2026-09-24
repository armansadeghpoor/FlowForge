using FlowForge.Application.Common;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Definitions;

/// <summary>
/// Defines workflow definition management use cases.
/// </summary>
public interface IWorkflowDefinitionService
{
    /// <summary>
    /// Validates and creates a workflow definition version.
    /// </summary>
    Task<ApplicationResult<WorkflowDefinition>> CreateAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a workflow definition version.
    /// </summary>
    Task<ApplicationResult<WorkflowDefinition?>> GetAsync(
        WorkflowDefinitionId id,
        string version,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists all workflow definition versions.
    /// </summary>
    Task<ApplicationResult<IReadOnlyList<WorkflowDefinition>>> ListAsync(
        CancellationToken cancellationToken);
}
