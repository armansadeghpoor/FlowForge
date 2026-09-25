using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Tenancy;

/// <summary>
/// Represents immutable tenant information for one operation.
/// </summary>
public sealed record TenantContext : ITenantContext
{
    private TenantContext()
    {
    }

    /// <summary>
    /// Initializes a context for an available tenant.
    /// </summary>
    /// <param name="tenantId">The current tenant identifier.</param>
    public TenantContext(TenantId tenantId)
    {
        TenantId = tenantId;
    }

    /// <summary>
    /// Gets the default context when no tenant is available.
    /// </summary>
    public static TenantContext None { get; } = new();

    /// <inheritdoc />
    public TenantId? TenantId { get; }

    /// <inheritdoc />
    public bool HasTenant => TenantId.HasValue;
}
