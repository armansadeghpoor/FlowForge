using FlowForge.Abstractions.Configuration;
using FlowForge.Abstractions.Health;
using FlowForge.Infrastructure.Health;
using FlowForge.Infrastructure.Persistence.PostgreSql;

namespace FlowForge.Api.Health;

/// <summary>
/// Registers runtime dependency readiness checks for the API host.
/// </summary>
public static class HealthServiceCollectionExtensions
{
    /// <summary>
    /// Adds health dependencies configured for the current host.
    /// </summary>
    public static IServiceCollection AddFlowForgeHealth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration[
            $"{DatabaseOptions.SectionName}:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IReadinessCheck>(
                new PostgreSqlReadinessCheck(
                    new PostgreSqlConnectionFactory(connectionString)));
        }

        return services;
    }
}
