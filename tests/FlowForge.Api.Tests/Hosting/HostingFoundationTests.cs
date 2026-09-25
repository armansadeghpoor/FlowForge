using System.Reflection;
using FlowForge.Abstractions.Hosting;
using FlowForge.Api.Contracts;
using FlowForge.Api.Controllers;
using FlowForge.Api.Hosting;
using Microsoft.AspNetCore.Mvc;
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
        Assert.Equal(Environments.Staging, response.Environment);
        Assert.Equal("11.3.0", response.Version);
        Assert.Equal(
            "health",
            typeof(HealthController).GetCustomAttribute<RouteAttribute>()?.Template);
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

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "FlowForge.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
