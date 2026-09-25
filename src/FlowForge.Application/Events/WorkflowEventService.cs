using System.Text.Json;
using FlowForge.Abstractions.Events;
using FlowForge.Application.Common;

namespace FlowForge.Application.Events;

/// <summary>
/// Provides the application boundary for external workflow events.
/// </summary>
public sealed class WorkflowEventService : IWorkflowEventService
{
    private readonly IWorkflowEventRuntime _eventRuntime;

    /// <summary>
    /// Initializes a workflow event service.
    /// </summary>
    public WorkflowEventService(IWorkflowEventRuntime eventRuntime)
    {
        ArgumentNullException.ThrowIfNull(eventRuntime);
        _eventRuntime = eventRuntime;
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<IReadOnlyList<EventExecutionResult>>> DispatchAsync(
        WorkflowEventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var validationErrors = Validate(context);
        if (validationErrors.Count > 0)
        {
            return ApplicationResult<IReadOnlyList<EventExecutionResult>>.Failure(
                validationErrors);
        }

        try
        {
            var results = await _eventRuntime.DispatchAsync(context, cancellationToken);
            return ApplicationResult<IReadOnlyList<EventExecutionResult>>.Success(results);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ApplicationResult<IReadOnlyList<EventExecutionResult>>.Failure(
                new ApplicationError(
                    "EventDispatchFailed",
                    "The workflow event dispatch failed."));
        }
    }

    private static IReadOnlyList<ApplicationError> Validate(WorkflowEventContext context)
    {
        var errors = new List<ApplicationError>();

        if (string.IsNullOrWhiteSpace(context.EventType))
        {
            errors.Add(new ApplicationError(
                "EventTypeRequired",
                "An event type is required."));
        }

        if (!IsValidPayload(context.Payload))
        {
            errors.Add(new ApplicationError(
                "EventPayloadRequired",
                "A JSON-compatible event payload is required."));
        }

        if (context.OccurredAt == default)
        {
            errors.Add(new ApplicationError(
                "EventOccurredAtRequired",
                "An event occurrence timestamp is required."));
        }

        if (context.CorrelationId.Value == Guid.Empty)
        {
            errors.Add(new ApplicationError(
                "CorrelationIdRequired",
                "A correlation identifier is required."));
        }

        return Array.AsReadOnly(errors.ToArray());
    }

    private static bool IsValidPayload(JsonElement payload)
    {
        try
        {
            return payload.ValueKind != JsonValueKind.Undefined;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
