namespace FlowForge.Abstractions.Security;

/// <summary>
/// Evaluates provider-independent permission and ownership requirements.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Evaluates the supplied authorization request against the current contexts.
    /// </summary>
    Task<AuthorizationDecision> AuthorizeAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken);
}
