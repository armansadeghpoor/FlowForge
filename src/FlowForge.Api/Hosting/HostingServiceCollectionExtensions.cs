using System.Reflection;
using FlowForge.Abstractions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowForge.Api.Hosting;

/// <summary>
/// Registers FlowForge host metadata and lifecycle behavior.
/// </summary>
public static class HostingServiceCollectionExtensions
{
    /// <summary>
    /// Adds application information, startup validation, and lifecycle logging.
    /// </summary>
    public static IServiceCollection AddFlowForgeHosting(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        var assembly = typeof(HostingServiceCollectionExtensions).Assembly;
        var assemblyName = assembly.GetName();
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? assemblyName.Version?.ToString() ?? "unknown";
        var applicationName = string.IsNullOrWhiteSpace(environment.ApplicationName)
            ? assemblyName.Name ?? "FlowForge.Api"
            : environment.ApplicationName;

        services.AddSingleton(new ApplicationInformation(
            applicationName,
            version,
            environment.EnvironmentName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ApplicationUptime>();
        services.AddHostedService<HostStartupValidationService>();
        services.AddHostedService<HostLifecycleLoggingService>();

        return services;
    }
}
