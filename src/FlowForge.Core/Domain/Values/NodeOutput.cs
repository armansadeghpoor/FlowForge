namespace FlowForge.Core.Domain.Values;

/// <summary>
/// Represents optional output data produced by a node execution.
/// </summary>
public sealed record NodeOutput
{
    /// <summary>
    /// Gets the output value.
    /// </summary>
    public required object? Value { get; init; }
}
