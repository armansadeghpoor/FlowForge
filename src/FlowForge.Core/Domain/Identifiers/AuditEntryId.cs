namespace FlowForge.Core.Domain.Identifiers;

/// <summary>
/// Identifies an enterprise audit entry.
/// </summary>
/// <param name="Value">The underlying identifier value.</param>
public readonly record struct AuditEntryId(Guid Value);
