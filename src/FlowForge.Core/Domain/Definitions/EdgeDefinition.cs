using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Domain.Definitions;

/// <summary>
/// Represents a dependency edge between two workflow nodes.
/// </summary>
public sealed record EdgeDefinition
{
    public required NodeId From { get; init; }

    public required NodeId To { get; init; }
}
