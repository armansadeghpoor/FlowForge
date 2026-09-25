namespace FlowForge.Core.Domain.Identifiers;

/// <summary>
/// Identifies a correlated workflow execution lifecycle.
/// </summary>
/// <param name="Value">The underlying identifier value.</param>
public readonly record struct ExecutionCorrelationId(Guid Value);
