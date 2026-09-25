using FlowForge.Abstractions.Events;
using FlowForge.Application.Common;

namespace FlowForge.Application.Events;

/// <summary>
/// Defines external workflow event use cases.
/// </summary>
public interface IWorkflowEventService
{
    /// <summary>
    /// Dispatches an external event.
    /// </summary>
    Task<ApplicationResult<IReadOnlyList<EventExecutionResult>>> DispatchAsync(
        WorkflowEventContext context,
        CancellationToken cancellationToken);
}
