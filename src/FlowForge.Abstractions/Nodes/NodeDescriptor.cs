namespace FlowForge.Abstractions.Nodes;

/// <summary>
/// Describes a node runner and its configuration contract.
/// </summary>
public sealed record NodeDescriptor
{
    /// <summary>
    /// Gets the node type identifier.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the node contract version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the node configuration property definitions.
    /// </summary>
    public required IReadOnlyDictionary<string, NodePropertyDefinition> ConfigurationSchema { get; init; }
}
