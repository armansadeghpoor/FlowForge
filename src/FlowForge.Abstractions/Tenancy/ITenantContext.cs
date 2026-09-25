using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Tenancy;

/// <summary>
/// Provides the tenant context associated with the current operation.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant identifier, when available.
    /// </summary>
    TenantId? TenantId { get; }

    /// <summary>
    /// Gets a value indicating whether a tenant is available.
    /// </summary>
    bool HasTenant { get; }
}
