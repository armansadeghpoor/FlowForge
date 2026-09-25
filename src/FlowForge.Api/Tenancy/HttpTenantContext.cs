using FlowForge.Abstractions.Tenancy;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Api.Tenancy;

/// <summary>
/// Holds the tenant context for the current HTTP request scope.
/// </summary>
public sealed class HttpTenantContext : ITenantContext
{
    private TenantContext _current = TenantContext.None;

    /// <inheritdoc />
    public TenantId? TenantId => _current.TenantId;

    /// <inheritdoc />
    public bool HasTenant => _current.HasTenant;

    internal void SetCurrent(TenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _current = context;
    }
}
