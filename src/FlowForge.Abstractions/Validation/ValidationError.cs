namespace FlowForge.Abstractions.Validation;

/// <summary>
/// Describes one workflow definition validation failure.
/// </summary>
/// <param name="Code">The stable validation error code.</param>
/// <param name="Message">A diagnostic description of the failure.</param>
public sealed record ValidationError(string Code, string Message);
