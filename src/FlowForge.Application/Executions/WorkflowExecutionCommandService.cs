using System.Text.Json;
using FlowForge.Abstractions.Auditing;
using FlowForge.Abstractions.Triggers;
using FlowForge.Application.Auditing;
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
    private readonly ApplicationAuditRecorder _auditRecorder;

    /// <summary>
    /// Initializes a workflow execution command service.
    /// </summary>
    public WorkflowExecutionCommandService(
        IWorkflowTriggerExecutor triggerExecutor,
        IAuditStore auditStore,
        IAuditContext auditContext)
    {
        ArgumentNullException.ThrowIfNull(triggerExecutor);
        ArgumentNullException.ThrowIfNull(auditStore);
        ArgumentNullException.ThrowIfNull(auditContext);
        _triggerExecutor = triggerExecutor;
        _auditRecorder = new ApplicationAuditRecorder(auditStore, auditContext);
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
            await AuditExecutionRequestAsync(
                context,
                AuditOutcome.Failed,
                cancellationToken);
            return ApplicationResult<WorkflowExecutionId>.Failure(validationError);
        }

        try
        {
            var executionId = await _triggerExecutor.ExecuteAsync(
                context,
                cancellationToken);
            await AuditExecutionRequestAsync(
                context,
                AuditOutcome.Succeeded,
                cancellationToken,
                executionId);
            return ApplicationResult<WorkflowExecutionId>.Success(executionId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (KeyNotFoundException)
        {
            await AuditExecutionRequestAsync(
                context,
                AuditOutcome.Failed,
                cancellationToken);
            return ApplicationResult<WorkflowExecutionId>.Failure(
                new ApplicationError(
                    "TriggerExecutionNotFound",
                    "The trigger or referenced workflow definition was not found."));
        }
        catch (InvalidOperationException)
        {
            await AuditExecutionRequestAsync(
                context,
                AuditOutcome.Failed,
                cancellationToken);
            return ApplicationResult<WorkflowExecutionId>.Failure(
                new ApplicationError(
                    "TriggerExecutionConflict",
                    "The trigger cannot be executed in its current state."));
        }
        catch (Exception)
        {
            await AuditExecutionRequestAsync(
                context,
                AuditOutcome.Failed,
                cancellationToken);
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

        if (context.CorrelationId.Value == Guid.Empty)
        {
            return new ApplicationError(
                "CorrelationIdRequired",
                "A correlation identifier is required.");
        }

        return null;
    }

    private Task AuditExecutionRequestAsync(
        WorkflowTriggerExecutionContext context,
        AuditOutcome outcome,
        CancellationToken cancellationToken,
        WorkflowExecutionId? workflowExecutionId = null)
    {
        var metadata = JsonSerializer.SerializeToElement(
            new
            {
                executionRequestId = context.ExecutionRequestId.Value,
                workflowExecutionId = workflowExecutionId?.Value
            });

        return _auditRecorder.TryRecordAsync(
            ApplicationAuditActions.WorkflowTriggerExecute,
            ApplicationAuditResourceTypes.WorkflowTrigger,
            context.TriggerId.Value.ToString("D"),
            outcome,
            resourceTenantId: null,
            cancellationToken,
            context.CorrelationId,
            metadata);
    }
}
