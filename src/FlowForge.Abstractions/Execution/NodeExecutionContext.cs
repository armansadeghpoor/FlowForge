using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Execution;

/// <summary>
/// Contains shared identity and attempt information for a node execution.
/// </summary>
public sealed record NodeExecutionContext
{
    /// <summary>
    /// Gets the containing workflow execution identifier.
    /// </summary>
    public required WorkflowExecutionId WorkflowExecutionId { get; init; }

    /// <summary>
    /// Gets the node execution identifier, shared across attempts.
    /// </summary>
    public required NodeExecutionId NodeExecutionId { get; init; }

    /// <summary>
    /// Gets the node definition being executed.
    /// </summary>
    public required NodeDefinition NodeDefinition { get; init; }

    /// <summary>
    /// Gets the current attempt number, starting at one.
    /// </summary>
    public required int AttemptNumber { get; init; }
}
