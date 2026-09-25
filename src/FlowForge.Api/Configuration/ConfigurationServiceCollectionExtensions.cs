using FlowForge.Abstractions.Configuration;

namespace FlowForge.Api.Configuration;

/// <summary>
/// Registers and validates FlowForge host configuration.
/// </summary>
public static class ConfigurationServiceCollectionExtensions
{
    /// <summary>
    /// Adds FlowForge configuration options and startup validation.
    /// </summary>
    /// <param name="services">The service collection receiving option registrations.</param>
    /// <param name="configuration">The application configuration source.</param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddFlowForgeConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<RuntimeOptions>()
            .Bind(configuration.GetSection(RuntimeOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.OwnerId),
                $"{RuntimeOptions.SectionName}:OwnerId is required.")
            .Validate(
                options => options.HeartbeatInterval > TimeSpan.Zero,
                $"{RuntimeOptions.SectionName}:HeartbeatInterval must be greater than zero.")
            .Validate(
                options => options.StaleExecutionThreshold > options.HeartbeatInterval,
                $"{RuntimeOptions.SectionName}:StaleExecutionThreshold must exceed HeartbeatInterval.")
            .ValidateOnStart();

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                $"{DatabaseOptions.SectionName}:ConnectionString is required.")
            .ValidateOnStart();

        services.AddOptions<ExecutionOptions>()
            .Bind(configuration.GetSection(ExecutionOptions.SectionName))
            .Validate(
                options => options.DefaultNodeTimeout > TimeSpan.Zero,
                $"{ExecutionOptions.SectionName}:DefaultNodeTimeout must be greater than zero.")
            .Validate(
                options => options.MaxNodeAttempts > 0,
                $"{ExecutionOptions.SectionName}:MaxNodeAttempts must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<SecurityOptions>()
            .Bind(configuration.GetSection(SecurityOptions.SectionName))
            .Validate(
                options => !options.RequireAuthentication ||
                    !string.IsNullOrWhiteSpace(options.Authority),
                $"{SecurityOptions.SectionName}:Authority is required when authentication is required.")
            .Validate(
                options => !options.RequireAuthentication ||
                    !string.IsNullOrWhiteSpace(options.Audience),
                $"{SecurityOptions.SectionName}:Audience is required when authentication is required.")
            .Validate(
                options => string.IsNullOrWhiteSpace(options.Authority) ||
                    Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority) &&
                    (authority.Scheme == Uri.UriSchemeHttps ||
                     authority.Scheme == Uri.UriSchemeHttp),
                $"{SecurityOptions.SectionName}:Authority must be an absolute HTTP or HTTPS URI.")
            .ValidateOnStart();

        return services;
    }
}
