namespace FlowForge.Abstractions.Configuration;

/// <summary>
/// Defines API authentication configuration without identifying a specific identity provider.
/// </summary>
public sealed record SecurityOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "FlowForge:Security";

    /// <summary>Gets the expected JWT issuer authority.</summary>
    public string Authority { get; init; } = string.Empty;

    /// <summary>Gets the expected JWT audience.</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>Gets whether every API endpoint requires an authenticated user.</summary>
    public bool RequireAuthentication { get; init; }
}
