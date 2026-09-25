namespace FlowForge.Abstractions.Configuration;

/// <summary>
/// Defines provider-independent database configuration.
/// </summary>
public sealed record DatabaseOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "FlowForge:Database";

    /// <summary>
    /// Gets the database connection string supplied to Infrastructure.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;
}
