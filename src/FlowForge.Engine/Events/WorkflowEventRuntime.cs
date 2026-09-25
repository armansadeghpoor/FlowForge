using FlowForge.Abstractions.Events;
using FlowForge.Abstractions.Triggers;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;
using FlowForge.Engine.Execution;

namespace FlowForge.Engine.Events;

/// <summary>
/// Dispatches external events through matching workflow triggers.
/// </summary>
public sealed class WorkflowEventRuntime : IWorkflowEventRuntime
{
    private readonly IEventTriggerMatcher _matcher;
    private readonly IWorkflowTriggerExecutor _triggerExecutor;

    /// <summary>
    /// Initializes a workflow event runtime.
    /// </summary>
    public WorkflowEventRuntime(
        IEventTriggerMatcher matcher,
        IWorkflowTriggerExecutor triggerExecutor)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        ArgumentNullException.ThrowIfNull(triggerExecutor);
        _matcher = matcher;
        _triggerExecutor = triggerExecutor;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EventExecutionResult>> DispatchAsync(
        WorkflowEventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var matches = await _matcher.FindMatchesAsync(context, cancellationToken);
        var results = new List<EventExecutionResult>();

        foreach (var eventTrigger in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!eventTrigger.Enabled)
            {
                continue;
            }

            results.Add(await ExecuteTriggerAsync(
                eventTrigger,
                context,
                cancellationToken));
        }

        return Array.AsReadOnly(results.ToArray());
    }

    private async Task<EventExecutionResult> ExecuteTriggerAsync(
        WorkflowEventTrigger eventTrigger,
        WorkflowEventContext eventContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var workflowExecutionId = await _triggerExecutor.ExecuteAsync(
                new WorkflowTriggerExecutionContext
                {
                    ExecutionRequestId = ExecutionRequestIdFactory.Create(
                        eventTrigger.Id.Value,
                        eventContext.CorrelationId.Value.ToString("N")),
                    TriggerId = eventTrigger.WorkflowTriggerId,
                    TriggerType = TriggerType.Event,
                    CorrelationId = eventContext.CorrelationId,
                    RequestedAt = eventContext.OccurredAt
                },
                cancellationToken);

            return CreateResult(eventTrigger, true, workflowExecutionId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return CreateResult(eventTrigger, false, null);
        }
    }

    private static EventExecutionResult CreateResult(
        WorkflowEventTrigger eventTrigger,
        bool success,
        WorkflowExecutionId? workflowExecutionId) =>
        new()
        {
            EventTriggerId = eventTrigger.Id,
            TriggerId = eventTrigger.WorkflowTriggerId,
            Success = success,
            WorkflowExecutionId = workflowExecutionId
        };
}
