namespace FlowForge.Abstractions.Health;

/// <summary>
/// Defines a provider-independent runtime dependency readiness check.
/// </summary>
public interface IReadinessCheck
{
    /// <summary>Gets the stable dependency name.</summary>
    string Name { get; }

    /// <summary>
    /// Determines whether the dependency is available to serve application requests.
    /// </summary>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}
