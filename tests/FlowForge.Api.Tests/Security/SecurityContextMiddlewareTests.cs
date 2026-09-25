using System.Security.Claims;
using FlowForge.Abstractions.Security;
using FlowForge.Api.Configuration;
using FlowForge.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.Api.Tests.Security;

public sealed class SecurityContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AnonymousRequestCreatesAnonymousContext()
    {
        var nextCalled = false;
        var userContext = new HttpUserContext();
        var middleware = new SecurityContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(new DefaultHttpContext(), userContext);

        Assert.True(nextCalled);
        Assert.False(userContext.Current.IsAuthenticated);
        Assert.Null(userContext.Current.UserId);
        Assert.Null(userContext.Current.TenantId);
        Assert.Empty(userContext.Current.Permissions);
    }

    [Fact]
    public async Task InvokeAsync_PropagatesJwtClaimsWithoutAuthenticating()
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(SecurityContextMiddleware.UserIdClaimType, "user-42"),
                new Claim(SecurityContextMiddleware.TenantIdClaimType, "tenant-7"),
                new Claim(
                    SecurityContextMiddleware.PermissionClaimType,
                    Permissions.WorkflowTriggersExecute)
            ],
            authenticationType: "Bearer"));
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        var userContext = new HttpUserContext();
        var middleware = new SecurityContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, userContext);

        Assert.True(userContext.Current.IsAuthenticated);
        Assert.Equal("user-42", userContext.Current.UserId);
        Assert.Equal("tenant-7", userContext.Current.TenantId);
        Assert.Contains(
            Permissions.WorkflowTriggersExecute,
            userContext.Current.Permissions);
        Assert.Same(principal, httpContext.User);
    }

    [Fact]
    public void AddFlowForgeApi_RegistersOneScopedUserContext()
    {
        var services = new ServiceCollection();
        services.AddFlowForgeApi();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var abstraction = scope.ServiceProvider.GetRequiredService<IUserContext>();
        var implementation = scope.ServiceProvider.GetRequiredService<HttpUserContext>();

        Assert.Same(implementation, abstraction);
    }
}
