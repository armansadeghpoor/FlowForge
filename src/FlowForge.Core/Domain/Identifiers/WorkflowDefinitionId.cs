namespace FlowForge.Core.Domain.Identifiers;

/// <summary>
/// Identifies a managed workflow definition across its versions.
/// </summary>
public readonly record struct WorkflowDefinitionId(Guid Value);
