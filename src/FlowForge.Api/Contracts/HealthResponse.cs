namespace FlowForge.Api.Contracts;

/// <summary>
/// Represents a stable host health snapshot.
/// </summary>
public sealed record HealthResponse
{
    /// <summary>Gets the application status.</summary>
    public required string Status { get; init; }

    /// <summary>Gets the application name.</summary>
    public required string Application { get; init; }

    /// <summary>Gets the host environment name.</summary>
    public required string Environment { get; init; }

    /// <summary>Gets the application version.</summary>
    public required string Version { get; init; }

    /// <summary>Gets the request correlation identifier.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>Gets dependency health results included in the snapshot.</summary>
    public required IReadOnlyList<HealthDependencyResponse> Dependencies { get; init; }
}

/// <summary>
/// Represents the safe status of one runtime dependency.
/// </summary>
public sealed record HealthDependencyResponse
{
    /// <summary>Gets the stable dependency name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the dependency status.</summary>
    public required string Status { get; init; }
}
