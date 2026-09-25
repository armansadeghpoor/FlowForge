namespace FlowForge.Abstractions.Configuration;

/// <summary>
/// Defines default workflow execution policy configuration.
/// </summary>
public sealed record ExecutionOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "FlowForge:Execution";

    /// <summary>
    /// Gets the default timeout intended for node execution policy configuration.
    /// </summary>
    public TimeSpan DefaultNodeTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the default maximum number of node execution attempts.
    /// </summary>
    public int MaxNodeAttempts { get; init; } = 1;
}
