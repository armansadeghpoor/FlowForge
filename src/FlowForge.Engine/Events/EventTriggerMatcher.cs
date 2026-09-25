using FlowForge.Abstractions.Events;
using FlowForge.Abstractions.Triggers;
using FlowForge.Core.Domain.Triggers;

namespace FlowForge.Engine.Events;

/// <summary>
/// Matches stored workflow event triggers by exact event type.
/// </summary>
public sealed class EventTriggerMatcher : IEventTriggerMatcher
{
    private readonly IWorkflowEventTriggerStore _eventTriggerStore;

    /// <summary>
    /// Initializes an event trigger matcher.
    /// </summary>
    public EventTriggerMatcher(IWorkflowEventTriggerStore eventTriggerStore)
    {
        ArgumentNullException.ThrowIfNull(eventTriggerStore);
        _eventTriggerStore = eventTriggerStore;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkflowEventTrigger>> FindMatchesAsync(
        WorkflowEventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var eventTriggers = await _eventTriggerStore.ListAsync(cancellationToken);
        return Array.AsReadOnly(eventTriggers
            .Where(eventTrigger => string.Equals(
                eventTrigger.EventType,
                context.EventType,
                StringComparison.Ordinal))
            .ToArray());
    }
}
