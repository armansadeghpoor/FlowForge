using FlowForge.Abstractions.Observability;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowForge.Api.Observability;

/// <summary>
/// Registers operational observability services.
/// </summary>
public static class ObservabilityServiceCollectionExtensions
{
    /// <summary>
    /// Adds the process-local operational metrics implementation.
    /// </summary>
    public static IServiceCollection AddFlowForgeObservability(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IMetricsCollector, InMemoryMetricsCollector>();
        return services;
    }
}
