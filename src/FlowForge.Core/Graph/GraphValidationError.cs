using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Core.Graph;

/// <summary>
/// Describes a structured workflow graph validation error.
/// </summary>
public sealed record GraphValidationError(
    GraphValidationErrorType ErrorType,
    string Message,
    NodeId? NodeId = null,
    NodeId? From = null,
    NodeId? To = null);
