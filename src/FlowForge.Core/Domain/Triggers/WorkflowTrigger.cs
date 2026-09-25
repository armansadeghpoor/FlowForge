using System.Text.Json;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Domain.Triggers;

/// <summary>
/// Describes a future mechanism for starting a workflow definition version.
/// </summary>
public sealed record WorkflowTrigger
{
    /// <summary>
    /// Gets the trigger identifier.
    /// </summary>
    public required WorkflowTriggerId Id { get; init; }

    /// <summary>
    /// Gets the referenced workflow definition identifier.
    /// </summary>
    public required WorkflowDefinitionId WorkflowDefinitionId { get; init; }

    /// <summary>
    /// Gets the referenced workflow definition version.
    /// </summary>
    public required string DefinitionVersion { get; init; }

    /// <summary>
    /// Gets the trigger type.
    /// </summary>
    public required TriggerType Type { get; init; }

    /// <summary>
    /// Gets a value indicating whether the trigger may start workflow executions.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets provider-independent trigger configuration.
    /// </summary>
    public required IReadOnlyDictionary<string, JsonElement> Configuration { get; init; }
}
