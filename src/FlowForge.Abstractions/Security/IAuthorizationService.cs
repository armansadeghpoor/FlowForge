namespace FlowForge.Abstractions.Security;

/// <summary>
/// Determines whether the current operation has a requested permission.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Determines whether the current security context has the requested permission.
    /// </summary>
    Task<bool> AuthorizeAsync(
        string permission,
        CancellationToken cancellationToken);
}
