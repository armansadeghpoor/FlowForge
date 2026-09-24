using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;

namespace FlowForge.Abstractions.Triggers;

/// <summary>
/// Defines provider-independent storage operations for workflow event triggers.
/// </summary>
public interface IWorkflowEventTriggerStore
{
    /// <summary>
    /// Saves an immutable workflow event trigger.
    /// </summary>
    /// <param name="eventTrigger">The event trigger to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="ArgumentException">Required event trigger metadata is invalid.</exception>
    /// <exception cref="InvalidOperationException">
    /// The event trigger identifier already exists.
    /// </exception>
    Task SaveAsync(
        WorkflowEventTrigger eventTrigger,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets an event trigger by its identifier.
    /// </summary>
    /// <param name="id">The event trigger identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The event trigger, or <see langword="null"/> when it is not found.</returns>
    Task<WorkflowEventTrigger?> GetAsync(
        WorkflowEventTriggerId id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists all stored workflow event triggers.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The stored workflow event triggers.</returns>
    Task<IReadOnlyList<WorkflowEventTrigger>> ListAsync(
        CancellationToken cancellationToken);
}
