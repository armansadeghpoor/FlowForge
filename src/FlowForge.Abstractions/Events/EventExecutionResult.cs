using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Events;

/// <summary>
/// Represents the result of dispatching one workflow event trigger.
/// </summary>
public sealed record EventExecutionResult
{
    /// <summary>
    /// Gets the matching event trigger identifier.
    /// </summary>
    public required WorkflowEventTriggerId EventTriggerId { get; init; }

    /// <summary>
    /// Gets the workflow trigger identifier submitted for execution.
    /// </summary>
    public required WorkflowTriggerId TriggerId { get; init; }

    /// <summary>
    /// Gets a value indicating whether trigger execution succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the created workflow execution identifier when successful.
    /// </summary>
    public required WorkflowExecutionId? WorkflowExecutionId { get; init; }
}
