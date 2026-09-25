using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using FlowForge.Abstractions.Configuration;
using FlowForge.Abstractions.Security;
using FlowForge.Api.Configuration;
using FlowForge.Api.Controllers;
using FlowForge.Api.Security;
using FlowForge.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using FrameworkAuthorizationService = Microsoft.AspNetCore.Authorization.IAuthorizationService;

namespace FlowForge.Api.Tests.Security;

public sealed class AuthenticationFoundationTests
{
    private const string Audience = "flowforge-tests";
    private const string Issuer = "https://issuer.flowforge.test";

    [Fact]
    public async Task ValidJwt_CreatesUserTenantAndPermissionContext()
    {
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("flowforge-test-signing-key-32-bytes-minimum"));
        var services = CreateServices(requireAuthentication: false);
        services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                options.Authority = null;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,
                    ValidateAudience = true,
                    ValidAudience = Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = SecurityContextMiddleware.UserIdClaimType
                };
            });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims:
            [
                new Claim(SecurityContextMiddleware.UserIdClaimType, "user-42"),
                new Claim(SecurityContextMiddleware.TenantIdClaimType, "tenant-7"),
                new Claim(
                    SecurityContextMiddleware.PermissionClaimType,
                    Permissions.WorkflowDefinitionsRead)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };
        httpContext.Request.Headers.Authorization =
            $"Bearer {new JwtSecurityTokenHandler().WriteToken(token)}";

        var authentication = await httpContext.AuthenticateAsync(
            JwtBearerDefaults.AuthenticationScheme);
        Assert.True(authentication.Succeeded);
        httpContext.User = authentication.Principal!;

        var userContext = scope.ServiceProvider.GetRequiredService<HttpUserContext>();
        var middleware = new SecurityContextMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext, userContext);

        Assert.True(userContext.Current.IsAuthenticated);
        Assert.Equal("user-42", userContext.Current.UserId);
        Assert.Equal("tenant-7", userContext.Current.TenantId);
        Assert.Contains(
            Permissions.WorkflowDefinitionsRead,
            userContext.Current.Permissions);
    }

    [Fact]
    public void AnonymousMode_DoesNotInstallFallbackPolicy()
    {
        using var provider = CreateServices(requireAuthentication: false)
            .BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var authentication = provider
            .GetRequiredService<IOptions<AuthenticationOptions>>()
            .Value;
        var jwt = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Null(options.FallbackPolicy);
        Assert.Equal(
            JwtBearerDefaults.AuthenticationScheme,
            authentication.DefaultScheme);
        Assert.Equal(Issuer, jwt.Authority);
        Assert.Equal(Audience, jwt.Audience);
    }

    [Fact]
    public async Task RequiredAuthentication_RejectsAnonymousPrincipal()
    {
        using var provider = CreateServices(requireAuthentication: true)
            .BuildServiceProvider();
        using var scope = provider.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value;
        var authorization = scope.ServiceProvider
            .GetRequiredService<FrameworkAuthorizationService>();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };

        var result = await authorization.AuthorizeAsync(
            httpContext.User,
            httpContext,
            options.FallbackPolicy!);

        Assert.NotNull(options.FallbackPolicy);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task PermissionPolicy_AllowsMatchingPermission()
    {
        var allowed = await AuthorizePermissionAsync(
            Permissions.WorkflowDefinitionsRead,
            Permissions.WorkflowDefinitionsRead);

        Assert.True(allowed);
    }

    [Fact]
    public async Task PermissionPolicy_DeniesMissingPermission()
    {
        var allowed = await AuthorizePermissionAsync(
            Permissions.WorkflowDefinitionsRead,
            Permissions.WorkflowDefinitionsWrite);

        Assert.False(allowed);
    }

    [Fact]
    public void SelectedEndpoints_DeclarePermissionPolicies()
    {
        AssertPolicy<WorkflowDefinitionsController>(
            nameof(WorkflowDefinitionsController.ListAsync),
            Permissions.WorkflowDefinitionsRead);
        AssertPolicy<WorkflowDefinitionsController>(
            nameof(WorkflowDefinitionsController.CreateAsync),
            Permissions.WorkflowDefinitionsWrite);
        AssertPolicy<TriggerExecutionsController>(
            nameof(TriggerExecutionsController.ExecuteAsync),
            Permissions.WorkflowTriggersExecute);
    }

    private static async Task<bool> AuthorizePermissionAsync(
        string grantedPermission,
        string requestedPermission)
    {
        using var provider = CreateServices(requireAuthentication: false)
            .BuildServiceProvider();
        using var scope = provider.CreateScope();
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(SecurityContextMiddleware.UserIdClaimType, "user-42"),
                new Claim(SecurityContextMiddleware.PermissionClaimType, grantedPermission)
            ],
            JwtBearerDefaults.AuthenticationScheme));
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            User = principal
        };
        var userContext = scope.ServiceProvider.GetRequiredService<HttpUserContext>();
        var authorization = scope.ServiceProvider
            .GetRequiredService<FrameworkAuthorizationService>();
        var allowed = false;
        var middleware = new SecurityContextMiddleware(async context =>
        {
            var result = await authorization.AuthorizeAsync(
                context.User,
                context,
                requestedPermission);
            allowed = result.Succeeded;
        });

        await middleware.InvokeAsync(httpContext, userContext);
        return allowed;
    }

    private static ServiceCollection CreateServices(bool requireAuthentication)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SecurityOptions.SectionName}:Authority"] = Issuer,
                [$"{SecurityOptions.SectionName}:Audience"] = Audience,
                [$"{SecurityOptions.SectionName}:RequireAuthentication"] =
                    requireAuthentication.ToString()
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFlowForgeApi();
        services.AddFlowForgeConfiguration(configuration);
        services.AddFlowForgeAuthentication(configuration);
        services.AddScoped<
            FlowForge.Abstractions.Security.IAuthorizationService,
            PermissionAuthorizationService>();
        return services;
    }

    private static void AssertPolicy<TController>(
        string methodName,
        string expectedPolicy)
    {
        var attribute = typeof(TController)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)!
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(expectedPolicy, attribute.Policy);
    }
}
