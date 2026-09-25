namespace FlowForge.Abstractions.Security;

/// <summary>
/// Provides the security context associated with the current operation.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Gets the current security context.
    /// </summary>
    SecurityContext Current { get; }
}
