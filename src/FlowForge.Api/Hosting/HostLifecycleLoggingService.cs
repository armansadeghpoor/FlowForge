using FlowForge.Abstractions.Hosting;

namespace FlowForge.Api.Hosting;

/// <summary>
/// Logs application startup and shutdown lifecycle events.
/// </summary>
public sealed class HostLifecycleLoggingService(
    ApplicationInformation applicationInformation,
    ILogger<HostLifecycleLoggingService> logger) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Application {ApplicationName} version {ApplicationVersion} started in {EnvironmentName}",
            applicationInformation.Name,
            applicationInformation.Version,
            applicationInformation.Environment);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Application {ApplicationName} version {ApplicationVersion} is stopping",
            applicationInformation.Name,
            applicationInformation.Version);
        return Task.CompletedTask;
    }
}
