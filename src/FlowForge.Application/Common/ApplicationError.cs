namespace FlowForge.Application.Common;

/// <summary>
/// Describes an application-level use-case failure.
/// </summary>
/// <param name="Code">The stable application error code.</param>
/// <param name="Message">A client-safe description of the failure.</param>
public sealed record ApplicationError(string Code, string Message);
