using FlowForge.Abstractions.Configuration;
using FlowForge.Abstractions.Security;
using FlowForge.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace FlowForge.Api.Configuration;

/// <summary>
/// Registers API authentication and permission authorization policies.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    private static readonly string[] PermissionPolicies =
    [
        Permissions.WorkflowDefinitionsRead,
        Permissions.WorkflowDefinitionsWrite,
        Permissions.WorkflowTriggersExecute
    ];

    /// <summary>
    /// Adds JWT bearer authentication without providing an identity provider.
    /// </summary>
    public static IServiceCollection AddFlowForgeAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var security = configuration
            .GetSection(SecurityOptions.SectionName)
            .Get<SecurityOptions>() ?? new SecurityOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = string.IsNullOrWhiteSpace(security.Authority)
                    ? null
                    : security.Authority;
                options.Audience = string.IsNullOrWhiteSpace(security.Audience)
                    ? null
                    : security.Audience;
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = !Uri.TryCreate(
                    security.Authority,
                    UriKind.Absolute,
                    out var authority) || authority.Scheme == Uri.UriSchemeHttps;
                options.TokenValidationParameters.NameClaimType =
                    SecurityContextMiddleware.UserIdClaimType;
            });

        var authorization = services.AddAuthorizationBuilder();
        foreach (var permission in PermissionPolicies)
        {
            authorization.AddPolicy(
                permission,
                policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(new PermissionRequirement(permission));
                });
        }

        if (security.RequireAuthentication)
        {
            authorization.SetFallbackPolicy(
                new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build());
        }

        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }
}
