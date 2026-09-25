using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Domain.Tenants;

/// <summary>
/// Represents immutable provider-independent tenant information.
/// </summary>
public sealed record Tenant
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public required TenantId Id { get; init; }

    /// <summary>
    /// Gets the tenant name.
    /// </summary>
    public required string Name { get; init; }
}
