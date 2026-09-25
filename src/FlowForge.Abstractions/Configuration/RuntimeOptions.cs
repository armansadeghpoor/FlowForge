namespace FlowForge.Abstractions.Configuration;

/// <summary>
/// Defines host-level workflow runtime configuration.
/// </summary>
public sealed record RuntimeOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "FlowForge:Runtime";

    /// <summary>
    /// Gets the identifier used to distinguish this runtime instance.
    /// </summary>
    public string OwnerId { get; init; } = "flowforge-runtime";

    /// <summary>
    /// Gets the intended interval between execution heartbeats.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the heartbeat age after which an execution may be considered stale.
    /// </summary>
    public TimeSpan StaleExecutionThreshold { get; init; } = TimeSpan.FromMinutes(2);
}
