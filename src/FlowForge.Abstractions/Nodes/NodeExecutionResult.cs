using FlowForge.Core.Domain.Values;

namespace FlowForge.Abstractions.Nodes;

/// <summary>
/// Represents the outcome of a workflow node execution.
/// </summary>
public sealed record NodeExecutionResult
{
    /// <summary>
    /// Gets a value indicating whether execution succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the optional output produced by node execution.
    /// </summary>
    public required NodeOutput? Output { get; init; }

    /// <summary>
    /// Gets the error message associated with a failed execution, if any.
    /// </summary>
    public required string? ErrorMessage { get; init; }
}
