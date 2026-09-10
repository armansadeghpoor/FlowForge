using System.Text.Json;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Domain.Definitions;

/// <summary>
/// Represents a node in a workflow definition.
/// </summary>
public sealed record NodeDefinition
{
    public required NodeId Id { get; init; }

    public required string Type { get; init; }

    public required IReadOnlyDictionary<string, JsonElement> Configuration { get; init; }
}
