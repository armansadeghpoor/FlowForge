namespace FlowForge.Core.Domain.Identifiers;

/// <summary>
/// Identifies a tenant.
/// </summary>
/// <param name="Value">The underlying identifier value.</param>
public readonly record struct TenantId(Guid Value);
