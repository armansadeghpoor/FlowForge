using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Triggers;

/// <summary>
/// Describes a request to execute a workflow trigger.
/// </summary>
public sealed record WorkflowTriggerExecutionContext
{
    /// <summary>
    /// Gets the execution request identifier.
    /// </summary>
    public required ExecutionRequestId ExecutionRequestId { get; init; }

    /// <summary>
    /// Gets the trigger identifier.
    /// </summary>
    public required WorkflowTriggerId TriggerId { get; init; }

    /// <summary>
    /// Gets the requested trigger type.
    /// </summary>
    public required TriggerType TriggerType { get; init; }

    /// <summary>
    /// Gets the correlation identifier for the execution request.
    /// </summary>
    public required ExecutionCorrelationId CorrelationId { get; init; }

    /// <summary>
    /// Gets the time at which execution was requested.
    /// </summary>
    public required DateTime RequestedAt { get; init; }
}
