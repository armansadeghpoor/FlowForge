using System.Collections.Frozen;

namespace FlowForge.Abstractions.Security;

/// <summary>
/// Represents immutable identity and permission information for one operation.
/// </summary>
public sealed record SecurityContext
{
    /// <summary>
    /// Initializes a security context.
    /// </summary>
    /// <param name="userId">The provider-independent user identifier, when available.</param>
    /// <param name="isAuthenticated">Whether an upstream component authenticated the user.</param>
    /// <param name="permissions">The permissions associated with the user.</param>
    /// <param name="tenantId">The provider-independent tenant identifier, when available.</param>
    public SecurityContext(
        string? userId,
        bool isAuthenticated,
        IEnumerable<string> permissions,
        string? tenantId = null)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        UserId = userId;
        TenantId = tenantId;
        IsAuthenticated = isAuthenticated;
        Permissions = permissions.ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the anonymous security context.
    /// </summary>
    public static SecurityContext Anonymous { get; } = new(null, false, []);

    /// <summary>
    /// Gets the provider-independent user identifier, when available.
    /// </summary>
    public string? UserId { get; }

    /// <summary>
    /// Gets the provider-independent tenant identifier, when available.
    /// </summary>
    public string? TenantId { get; }

    /// <summary>
    /// Gets whether an upstream component authenticated the user.
    /// </summary>
    public bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the user's permissions using ordinal comparison semantics.
    /// </summary>
    public IReadOnlySet<string> Permissions { get; }
}
