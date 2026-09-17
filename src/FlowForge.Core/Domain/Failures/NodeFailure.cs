using FlowForge.Core.Domain.Enums;

namespace FlowForge.Core.Domain.Failures;

/// <summary>
/// Represents a classified node execution failure.
/// </summary>
public sealed record NodeFailure
{
    /// <summary>
    /// Gets the failure category.
    /// </summary>
    public required NodeFailureCategory Category { get; init; }

    /// <summary>
    /// Gets the failure message.
    /// </summary>
    public required string Message { get; init; }
}
