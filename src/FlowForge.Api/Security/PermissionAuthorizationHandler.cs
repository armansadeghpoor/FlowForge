using Microsoft.AspNetCore.Authorization;

namespace FlowForge.Api.Security;

/// <summary>
/// Bridges ASP.NET Core authorization policies to FlowForge's authorization contract.
/// </summary>
public sealed class PermissionAuthorizationHandler(
    FlowForge.Abstractions.Security.IAuthorizationService authorizationService)
    : AuthorizationHandler<PermissionRequirement>
{
    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;
        if (await authorizationService.AuthorizeAsync(
                requirement.Permission,
                cancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}
