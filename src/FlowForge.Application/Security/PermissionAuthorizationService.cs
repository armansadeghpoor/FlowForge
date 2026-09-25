using FlowForge.Abstractions.Security;

namespace FlowForge.Application.Security;

/// <summary>
/// Evaluates permissions against the current provider-independent security context.
/// </summary>
public sealed class PermissionAuthorizationService : IAuthorizationService
{
    private readonly IUserContext _userContext;

    /// <summary>
    /// Initializes the authorization service.
    /// </summary>
    public PermissionAuthorizationService(IUserContext userContext)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        _userContext = userContext;
    }

    /// <inheritdoc />
    public Task<bool> AuthorizeAsync(
        string permission,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        cancellationToken.ThrowIfCancellationRequested();

        var context = _userContext.Current;
        return Task.FromResult(
            context.IsAuthenticated && context.Permissions.Contains(permission));
    }
}
