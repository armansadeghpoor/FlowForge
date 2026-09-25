using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Abstractions.Engine;

/// <summary>
/// Carries request and correlation identity into workflow execution.
/// </summary>
public sealed record WorkflowExecutionRequest
{
    /// <summary>
    /// Gets the execution request identifier.
    /// </summary>
    public required ExecutionRequestId ExecutionRequestId { get; init; }

    /// <summary>
    /// Gets the execution correlation identifier.
    /// </summary>
    public required ExecutionCorrelationId CorrelationId { get; init; }
}
