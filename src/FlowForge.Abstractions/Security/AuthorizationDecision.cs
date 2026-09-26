namespace FlowForge.Abstractions.Security;

/// <summary>
/// Represents an immutable provider-independent authorization decision.
/// </summary>
/// <param name="Outcome">The explicit allow or deny outcome.</param>
public sealed record AuthorizationDecision(AuthorizationOutcome Outcome)
{
    /// <summary>Gets the shared allowed decision.</summary>
    public static AuthorizationDecision Allow { get; } =
        new(AuthorizationOutcome.Allowed);

    /// <summary>Gets the shared denied decision.</summary>
    public static AuthorizationDecision Deny { get; } =
        new(AuthorizationOutcome.Denied);

    /// <summary>Gets a value indicating whether the operation is allowed.</summary>
    public bool IsAllowed => Outcome == AuthorizationOutcome.Allowed;
}
