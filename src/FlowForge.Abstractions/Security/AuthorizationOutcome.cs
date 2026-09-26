namespace FlowForge.Abstractions.Security;

/// <summary>
/// Describes the explicit outcome of an authorization evaluation.
/// </summary>
public enum AuthorizationOutcome
{
    /// <summary>The operation is allowed.</summary>
    Allowed,

    /// <summary>The operation is denied.</summary>
    Denied
}
