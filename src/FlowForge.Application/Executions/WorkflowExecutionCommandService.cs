using FlowForge.Abstractions.Triggers;
using FlowForge.Application.Common;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Executions;

/// <summary>
/// Provides the application boundary for trigger-based workflow execution.
/// </summary>
public sealed class WorkflowExecutionCommandService : IWorkflowExecutionCommandService
{
    private readonly IWorkflowTriggerExecutor _triggerExecutor;

    /// <summary>
    /// Initializes a workflow execution command service.
    /// </summary>
    public WorkflowExecutionCommandService(IWorkflowTriggerExecutor triggerExecutor)
    {
        ArgumentNullException.ThrowIfNull(triggerExecutor);
        _triggerExecutor = triggerExecutor;
    }

    /// <inheritdoc />
    public async Task<ApplicationResult<WorkflowExecutionId>> ExecuteTriggerAsync(
        WorkflowTriggerExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var validationError = Validate(context);
        if (validationError is not null)
        {
            return ApplicationResult<WorkflowExecutionId>.Failure(validationError);
        }

        try
        {
            var executionId = await _triggerExecutor.ExecuteAsync(
                context,
                cancellationToken);
            return ApplicationResult<WorkflowExecutionId>.Success(executionId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            return ApplicationResult<WorkflowExecutionId>.Failure(
                new ApplicationError(
                    "TriggerExecutionNotFound",
                    "The trigger or referenced workflow definition was not found."));
        }
        catch (InvalidOperationException)
        {
            return ApplicationResult<WorkflowExecutionId>.Failure(
                new ApplicationError(
                    "TriggerExecutionConflict",
                    "The trigger cannot be executed in its current state."));
        }
        catch (Exception)
        {
            return ApplicationResult<WorkflowExecutionId>.Failure(
                new ApplicationError(
                    "TriggerExecutionFailed",
                    "The trigger execution failed."));
        }
    }

    private static ApplicationError? Validate(WorkflowTriggerExecutionContext context)
    {
        if (context.ExecutionRequestId.Value == Guid.Empty)
        {
            return new ApplicationError(
                "ExecutionRequestIdRequired",
                "An execution request identifier is required.");
        }

        if (context.TriggerId.Value == Guid.Empty)
        {
            return new ApplicationError(
                "TriggerIdRequired",
                "A workflow trigger identifier is required.");
        }

        if (context.TriggerType != TriggerType.Manual)
        {
            return new ApplicationError(
                "TriggerTypeInvalid",
                "Only manual triggers can be executed.");
        }

        if (string.IsNullOrWhiteSpace(context.CorrelationId))
        {
            return new ApplicationError(
                "CorrelationIdRequired",
                "A correlation identifier is required.");
        }

        return null;
    }
}
