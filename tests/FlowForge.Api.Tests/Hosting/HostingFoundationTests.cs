using System.Reflection;
using System.Text.Json;
using FlowForge.Abstractions.Configuration;
using FlowForge.Abstractions.Health;
using FlowForge.Abstractions.Hosting;
using FlowForge.Api.Contracts;
using FlowForge.Api.Controllers;
using FlowForge.Api.Health;
using FlowForge.Api.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace FlowForge.Api.Tests.Hosting;

public sealed class HostingFoundationTests
{
    [Fact]
    public void HealthEndpoint_ReturnsApplicationStatusEnvironmentAndVersion()
    {
        var information = new ApplicationInformation(
            "FlowForge.Api",
            "11.3.0",
            Environments.Staging);
        var controller = new HealthController(information);

        var result = controller.Get();

        var response = Assert.IsType<HealthResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("Healthy", response.Status);
        Assert.Equal("FlowForge.Api", response.Application);
        Assert.Equal(Environments.Staging, response.Environment);
        Assert.Equal("11.3.0", response.Version);
        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
        Assert.Empty(response.Dependencies);
        Assert.Equal(
            "health",
            typeof(HealthController).GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal(
            "live",
            typeof(HealthController)
                .GetMethod(nameof(HealthController.GetLiveness))
                ?.GetCustomAttribute<HttpGetAttribute>()
                ?.Template);
        Assert.Equal(
            "ready",
            typeof(HealthController)
                .GetMethod(nameof(HealthController.GetReadinessAsync))
                ?.GetCustomAttribute<HttpGetAttribute>()
                ?.Template);
    }

    [Fact]
    public void LivenessEndpoint_DoesNotExecuteDependencies()
    {
        var check = new StubReadinessCheck(
            "external",
            _ => throw new InvalidOperationException("Must not be called."));
        var controller = CreateHealthController(check);

        var result = controller.GetLiveness();

        var response = Assert.IsType<HealthResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("Healthy", response.Status);
        Assert.Empty(response.Dependencies);
        Assert.Equal(0, check.CallCount);
    }

    [Fact]
    public async Task ReadinessEndpoint_HealthyDependency_ReturnsAvailableResponse()
    {
        var check = new StubReadinessCheck(
            "postgresql",
            _ => Task.FromResult(true));
        var controller = CreateHealthController(check);

        var result = await controller.GetReadinessAsync(CancellationToken.None);

        var response = Assert.IsType<HealthResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("Healthy", response.Status);
        var dependency = Assert.Single(response.Dependencies);
        Assert.Equal("postgresql", dependency.Name);
        Assert.Equal("Healthy", dependency.Status);
        Assert.Equal(1, check.CallCount);
    }

    [Fact]
    public async Task ReadinessEndpoint_UnavailableDependency_ReturnsSafeResponse()
    {
        const string sensitiveMessage =
            "Host=database;Password=do-not-expose; internal stack details";
        var check = new StubReadinessCheck(
            "postgresql",
            _ => throw new InvalidOperationException(sensitiveMessage));
        var controller = CreateHealthController(check);
        controller.HttpContext.Request.Headers["X-Correlation-Id"] =
            "health-correlation";

        var result = await controller.GetReadinessAsync(CancellationToken.None);

        var unavailable = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
        var response = Assert.IsType<HealthResponse>(unavailable.Value);
        Assert.Equal("Unhealthy", response.Status);
        Assert.Equal("health-correlation", response.CorrelationId);
        Assert.Equal("Unhealthy", Assert.Single(response.Dependencies).Status);
        Assert.DoesNotContain(
            sensitiveMessage,
            JsonSerializer.Serialize(response),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AddFlowForgeHealth_ConfiguredDatabase_RegistersPostgreSqlCheck()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:ConnectionString"] =
                    "Host=localhost;Database=flowforge;Username=test;Password=test"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddFlowForgeHealth(configuration);
        using var provider = services.BuildServiceProvider();

        var check = Assert.Single(provider.GetServices<IReadinessCheck>());
        Assert.Equal("postgresql", check.Name);
    }

    [Fact]
    public void AddFlowForgeHealth_UnconfiguredDatabase_RegistersNoDependency()
    {
        var services = new ServiceCollection();

        services.AddFlowForgeHealth(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        Assert.Empty(provider.GetServices<IReadinessCheck>());
    }

    [Fact]
    public void ApplicationInformation_PreservesProviderIndependentMetadata()
    {
        var information = new ApplicationInformation(
            "FlowForge.Api",
            "1.0.0",
            Environments.Production);

        Assert.Equal("FlowForge.Api", information.Name);
        Assert.Equal("1.0.0", information.Version);
        Assert.Equal(Environments.Production, information.Environment);
    }

    [Fact]
    public void AddFlowForgeHosting_ReportsHostEnvironment()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFlowForgeHosting(new TestHostEnvironment
        {
            ApplicationName = "FlowForge.TestHost",
            EnvironmentName = Environments.Development
        });
        using var provider = services.BuildServiceProvider();

        var information = provider.GetRequiredService<ApplicationInformation>();

        Assert.Equal("FlowForge.TestHost", information.Name);
        Assert.Equal(Environments.Development, information.Environment);
        Assert.False(string.IsNullOrWhiteSpace(information.Version));
    }

    [Fact]
    public async Task StartupValidation_RejectsMissingApplicationMetadata()
    {
        var service = new HostStartupValidationService(
            new ApplicationInformation(string.Empty, "1.0.0", Environments.Production));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));

        Assert.Equal(
            "Application name, version, and environment are required.",
            exception.Message);
    }

    [Fact]
    public void AddFlowForgeHosting_RegistersValidationAndLifecycleServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFlowForgeHosting(new TestHostEnvironment());
        using var provider = services.BuildServiceProvider();

        var hostedServices = provider.GetServices<IHostedService>().ToArray();

        Assert.Contains(hostedServices, service => service is HostStartupValidationService);
        Assert.Contains(hostedServices, service => service is HostLifecycleLoggingService);
    }

    private static HealthController CreateHealthController(
        params IReadinessCheck[] readinessChecks) =>
        new(
            new ApplicationInformation(
                "FlowForge.Api",
                "13.2.0",
                Environments.Production),
            readinessChecks)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    private sealed class StubReadinessCheck(
        string name,
        Func<CancellationToken, Task<bool>> check) : IReadinessCheck
    {
        public string Name { get; } = name;

        public int CallCount { get; private set; }

        public Task<bool> IsReadyAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return check(cancellationToken);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "FlowForge.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
