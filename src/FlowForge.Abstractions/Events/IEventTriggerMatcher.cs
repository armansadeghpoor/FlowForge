using FlowForge.Core.Domain.Triggers;

namespace FlowForge.Abstractions.Events;

/// <summary>
/// Finds workflow event triggers matching an external event.
/// </summary>
public interface IEventTriggerMatcher
{
    /// <summary>
    /// Finds triggers whose event type exactly matches the supplied context.
    /// </summary>
    Task<IReadOnlyList<WorkflowEventTrigger>> FindMatchesAsync(
        WorkflowEventContext context,
        CancellationToken cancellationToken);
}
