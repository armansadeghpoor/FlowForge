using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Definitions;

/// <summary>
/// Defines provider-independent storage operations for managed workflow definitions.
/// </summary>
public interface IWorkflowDefinitionStore
{
    /// <summary>
    /// Saves an immutable workflow definition version.
    /// </summary>
    /// <param name="definition">The workflow definition to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">
    /// The same definition identifier and version already exist.
    /// </exception>
    Task SaveAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a workflow definition by its identifier and version.
    /// </summary>
    /// <param name="id">The workflow definition identifier.</param>
    /// <param name="version">The definition version.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The definition, or <see langword="null"/> when it is not found.</returns>
    Task<WorkflowDefinition?> GetAsync(
        WorkflowDefinitionId id,
        string version,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists all stored workflow definition versions.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The stored workflow definitions.</returns>
    Task<IReadOnlyList<WorkflowDefinition>> ListAsync(
        CancellationToken cancellationToken);
}
