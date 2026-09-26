using FlowForge.Abstractions.Security;
using FlowForge.Abstractions.Tenancy;

namespace FlowForge.Application.Security;

/// <summary>
/// Evaluates permissions and workflow ownership against current operation contexts.
/// </summary>
public sealed class PermissionAuthorizationService : IAuthorizationService
{
    private readonly IUserContext _userContext;
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// Initializes the authorization service.
    /// </summary>
    public PermissionAuthorizationService(
        IUserContext userContext,
        ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(tenantContext);
        _userContext = userContext;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public Task<AuthorizationDecision> AuthorizeAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Permission);
        cancellationToken.ThrowIfCancellationRequested();

        var context = _userContext.Current;
        if (!context.IsAuthenticated ||
            !context.Permissions.Contains(request.Permission))
        {
            return Task.FromResult(AuthorizationDecision.Deny);
        }

        if (request.OwnerTenantId is not { } ownerTenantId)
        {
            return Task.FromResult(AuthorizationDecision.Allow);
        }

        var tenantMatches = _tenantContext.TenantId == ownerTenantId;
        var securityTenantMatches = Guid.TryParse(
                context.TenantId,
                out var securityTenantId) &&
            securityTenantId == ownerTenantId.Value;

        return Task.FromResult(
            tenantMatches && securityTenantMatches
                ? AuthorizationDecision.Allow
                : AuthorizationDecision.Deny);
    }
}
