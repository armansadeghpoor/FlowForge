namespace FlowForge.Core.Domain.Identifiers;

/// <summary>
/// Uniquely identifies a request to start a workflow execution.
/// </summary>
/// <param name="Value">The underlying identifier value.</param>
public readonly record struct ExecutionRequestId(Guid Value);
