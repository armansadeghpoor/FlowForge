using FlowForge.Abstractions.Hosting;

namespace FlowForge.Api.Hosting;

/// <summary>
/// Validates metadata required by the API host during startup.
/// </summary>
public sealed class HostStartupValidationService(ApplicationInformation applicationInformation)
    : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(applicationInformation.Name) ||
            string.IsNullOrWhiteSpace(applicationInformation.Version) ||
            string.IsNullOrWhiteSpace(applicationInformation.Environment))
        {
            throw new InvalidOperationException(
                "Application name, version, and environment are required.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
