using FlowForge.Abstractions.Security;

namespace FlowForge.Api.Security;

/// <summary>
/// Holds the security context for the current HTTP request scope.
/// </summary>
public sealed class HttpUserContext : IUserContext
{
    /// <inheritdoc />
    public SecurityContext Current { get; private set; } = SecurityContext.Anonymous;

    internal void SetCurrent(SecurityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Current = context;
    }
}
