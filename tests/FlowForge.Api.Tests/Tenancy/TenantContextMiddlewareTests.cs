using System.Security.Claims;
using FlowForge.Abstractions.Tenancy;
using FlowForge.Api.Configuration;
using FlowForge.Api.Tenancy;
using FlowForge.Core.Domain.Identifiers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.Api.Tests.Tenancy;

public sealed class TenantContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ValidHeaderResolvesTenantWithoutChangingPrincipal()
    {
        var tenantId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test"));
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        httpContext.Request.Headers[TenantContextMiddleware.HeaderName] =
            tenantId.ToString("D");
        var context = new HttpTenantContext();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, context);

        Assert.True(context.HasTenant);
        Assert.Equal(new TenantId(tenantId), context.TenantId);
        Assert.Same(principal, httpContext.User);
    }

    [Fact]
    public async Task InvokeAsync_MissingHeaderUsesNoTenantContext()
    {
        var nextCalled = false;
        var context = new HttpTenantContext();
        var middleware = new TenantContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(new DefaultHttpContext(), context);

        Assert.True(nextCalled);
        Assert.False(context.HasTenant);
        Assert.Null(context.TenantId);
    }

    [Fact]
    public void AddFlowForgeApi_RegistersOneScopedTenantContext()
    {
        var services = new ServiceCollection();
        services.AddFlowForgeApi();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var abstraction = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var implementation = scope.ServiceProvider.GetRequiredService<HttpTenantContext>();

        Assert.Same(implementation, abstraction);
        Assert.False(abstraction.HasTenant);
        Assert.Null(abstraction.TenantId);
    }
}
