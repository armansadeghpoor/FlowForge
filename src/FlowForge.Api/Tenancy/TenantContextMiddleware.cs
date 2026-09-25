using FlowForge.Abstractions.Tenancy;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Api.Tenancy;

/// <summary>
/// Resolves provider-independent tenant context from an HTTP request header.
/// </summary>
public sealed class TenantContextMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Gets the HTTP header used for tenant identifiers.
    /// </summary>
    public const string HeaderName = "X-Tenant-Id";

    /// <summary>
    /// Resolves the request tenant without authenticating or authorizing it.
    /// </summary>
    public async Task InvokeAsync(
        HttpContext httpContext,
        HttpTenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(tenantContext);

        var headerValue = httpContext.Request.Headers[HeaderName].FirstOrDefault();
        var current = Guid.TryParse(headerValue, out var tenantId)
            ? new TenantContext(new TenantId(tenantId))
            : TenantContext.None;

        tenantContext.SetCurrent(current);
        await next(httpContext);
    }
}
