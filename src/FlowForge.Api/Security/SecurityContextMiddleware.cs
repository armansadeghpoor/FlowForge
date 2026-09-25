using System.Security.Claims;
using FlowForge.Abstractions.Security;

namespace FlowForge.Api.Security;

/// <summary>
/// Projects an existing HTTP principal into FlowForge's provider-independent security context.
/// </summary>
public sealed class SecurityContextMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Gets the claim type used to carry FlowForge permissions.
    /// </summary>
    public const string PermissionClaimType = "permissions";

    /// <summary>
    /// Gets the JWT claim type used for user identifiers.
    /// </summary>
    public const string UserIdClaimType = "sub";

    /// <summary>
    /// Gets the JWT claim type used for tenant identifiers.
    /// </summary>
    public const string TenantIdClaimType = "tenant_id";

    /// <summary>
    /// Creates the current request's security context without authenticating the request.
    /// </summary>
    public async Task InvokeAsync(
        HttpContext httpContext,
        HttpUserContext userContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(userContext);

        var principal = httpContext.User;
        var identity = principal.Identity;
        if (identity?.IsAuthenticated != true)
        {
            userContext.SetCurrent(SecurityContext.Anonymous);
        }
        else
        {
            var userId = principal.FindFirstValue(UserIdClaimType) ??
                principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
                identity.Name;
            var tenantId = principal.FindFirstValue(TenantIdClaimType);
            var permissions = principal.FindAll(PermissionClaimType)
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value));

            userContext.SetCurrent(new SecurityContext(
                userId,
                true,
                permissions,
                tenantId));
        }

        await next(httpContext);
    }
}
