namespace FlowForge.Api.Contracts;

/// <summary>
/// Represents application-level host health information.
/// </summary>
public sealed record HealthResponse
{
    /// <summary>Gets the application status.</summary>
    public required string Status { get; init; }

    /// <summary>Gets the host environment name.</summary>
    public required string Environment { get; init; }

    /// <summary>Gets the application version.</summary>
    public required string Version { get; init; }
}
