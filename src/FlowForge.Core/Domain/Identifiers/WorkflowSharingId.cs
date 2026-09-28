namespace FlowForge.Core.Domain.Identifiers;

/// <summary>
/// Identifies immutable workflow sharing metadata.
/// </summary>
/// <param name="Value">The underlying identifier value.</param>
public readonly record struct WorkflowSharingId(Guid Value);
