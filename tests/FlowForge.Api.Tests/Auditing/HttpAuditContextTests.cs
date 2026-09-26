using System.Security.Claims;
using FlowForge.Abstractions.Auditing;
using FlowForge.Abstractions.Security;
using FlowForge.Api.Configuration;
using FlowForge.Api.Correlation;
using FlowForge.Api.Security;
using FlowForge.Api.Tenancy;
using FlowForge.Core.Domain.Identifiers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.Api.Tests.Auditing;

public sealed class HttpAuditContextTests
{
    [Fact]
    public async Task Current_PreservesActorTenantAndCorrelationFromRequestScope()
    {
        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [
                    new Claim(SecurityContextMiddleware.UserIdClaimType, "user-42")
                ],
                authenticationType: "Bearer"))
        };
        httpContext.Request.Headers[TenantContextMiddleware.HeaderName] =
            tenantId.ToString("D");
        httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName] =
            correlationId.ToString("D");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFlowForgeApi();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext =
            httpContext;

        await new CorrelationIdMiddleware(_ => Task.CompletedTask)
            .InvokeAsync(httpContext);
        await new SecurityContextMiddleware(_ => Task.CompletedTask)
            .InvokeAsync(
                httpContext,
                scope.ServiceProvider.GetRequiredService<HttpUserContext>());
        await new TenantContextMiddleware(_ => Task.CompletedTask)
            .InvokeAsync(
                httpContext,
                scope.ServiceProvider.GetRequiredService<HttpTenantContext>());

        var current = scope.ServiceProvider
            .GetRequiredService<IAuditContext>()
            .Current;

        Assert.Equal("user-42", current.UserId);
        Assert.Equal(new TenantId(tenantId), current.TenantId);
        Assert.Equal(
            new ExecutionCorrelationId(correlationId),
            current.CorrelationId);
    }
}
