namespace FlowForge.Abstractions.Events;

/// <summary>
/// Defines the runtime boundary for external workflow events.
/// </summary>
public interface IWorkflowEventRuntime
{
    /// <summary>
    /// Dispatches an external event to matching workflow triggers.
    /// </summary>
    Task<IReadOnlyList<EventExecutionResult>> DispatchAsync(
        WorkflowEventContext context,
        CancellationToken cancellationToken);
}
