using FlowForge.Abstractions.Auditing;
using FlowForge.Abstractions.Security;
using FlowForge.Abstractions.Tenancy;
using FlowForge.Api.Correlation;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Api.Auditing;

/// <summary>
/// Provides audit actor, tenant, and correlation information for the current HTTP request.
/// </summary>
public sealed class HttpAuditContext(
    IUserContext userContext,
    ITenantContext tenantContext,
    IHttpContextAccessor httpContextAccessor) : IAuditContext
{
    /// <inheritdoc />
    public AuditContext Current => new()
    {
        UserId = userContext.Current.UserId,
        TenantId = tenantContext.TenantId,
        CorrelationId = new ExecutionCorrelationId(
            CorrelationIdMiddleware.GetExecutionCorrelationId(
                httpContextAccessor.HttpContext))
    };
}
