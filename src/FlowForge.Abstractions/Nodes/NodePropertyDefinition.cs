namespace FlowForge.Abstractions.Nodes;

/// <summary>
/// Describes a node configuration property.
/// </summary>
public sealed record NodePropertyDefinition
{
    /// <summary>
    /// Gets the configuration property name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the configuration property value type.
    /// </summary>
    public required NodePropertyType Type { get; init; }

    /// <summary>
    /// Gets a value indicating whether the configuration property is required.
    /// </summary>
    public required bool Required { get; init; }
}
