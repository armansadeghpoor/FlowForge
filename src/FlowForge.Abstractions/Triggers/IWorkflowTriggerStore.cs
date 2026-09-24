using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;

namespace FlowForge.Abstractions.Triggers;

/// <summary>
/// Defines provider-independent storage operations for workflow triggers.
/// </summary>
public interface IWorkflowTriggerStore
{
    /// <summary>
    /// Saves an immutable workflow trigger.
    /// </summary>
    /// <param name="trigger">The trigger to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="ArgumentException">Required trigger metadata is invalid.</exception>
    /// <exception cref="InvalidOperationException">The trigger identifier already exists.</exception>
    Task SaveAsync(
        WorkflowTrigger trigger,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a trigger by its identifier.
    /// </summary>
    /// <param name="id">The trigger identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The trigger, or <see langword="null"/> when it is not found.</returns>
    Task<WorkflowTrigger?> GetAsync(
        WorkflowTriggerId id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists all stored workflow triggers.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The stored workflow triggers.</returns>
    Task<IReadOnlyList<WorkflowTrigger>> ListAsync(
        CancellationToken cancellationToken);
}
