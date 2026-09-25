namespace FlowForge.Abstractions.Hosting;

/// <summary>
/// Describes the application instance exposed by a host.
/// </summary>
/// <param name="Name">The application name.</param>
/// <param name="Version">The application version.</param>
/// <param name="Environment">The host environment name.</param>
public sealed record ApplicationInformation(
    string Name,
    string Version,
    string Environment);
