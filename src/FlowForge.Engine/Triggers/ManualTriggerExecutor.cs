using FlowForge.Abstractions.Definitions;
using FlowForge.Abstractions.Engine;
using FlowForge.Abstractions.Triggers;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Engine.Triggers;

/// <summary>
/// Executes enabled manual triggers through the workflow engine.
/// </summary>
public sealed class ManualTriggerExecutor : IWorkflowTriggerExecutor
{
    private readonly IWorkflowTriggerStore _triggerStore;
    private readonly IWorkflowDefinitionStore _definitionStore;
    private readonly IWorkflowEngine _workflowEngine;

    /// <summary>
    /// Initializes a manual trigger executor.
    /// </summary>
    public ManualTriggerExecutor(
        IWorkflowTriggerStore triggerStore,
        IWorkflowDefinitionStore definitionStore,
        IWorkflowEngine workflowEngine)
    {
        ArgumentNullException.ThrowIfNull(triggerStore);
        ArgumentNullException.ThrowIfNull(definitionStore);
        ArgumentNullException.ThrowIfNull(workflowEngine);
        _triggerStore = triggerStore;
        _definitionStore = definitionStore;
        _workflowEngine = workflowEngine;
    }

    /// <inheritdoc />
    public async Task<WorkflowExecutionId> ExecuteAsync(
        WorkflowTriggerExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.TriggerType != TriggerType.Manual)
        {
            throw new InvalidOperationException(
                "The manual trigger executor only supports manual triggers.");
        }

        var trigger = await _triggerStore.GetAsync(
            context.TriggerId,
            cancellationToken);
        if (trigger is null)
        {
            throw new KeyNotFoundException(
                $"Workflow trigger '{context.TriggerId.Value}' was not found.");
        }

        if (trigger.Type != TriggerType.Manual)
        {
            throw new InvalidOperationException(
                $"Workflow trigger '{trigger.Id.Value}' is not a manual trigger.");
        }

        if (!trigger.Enabled)
        {
            throw new InvalidOperationException(
                $"Workflow trigger '{trigger.Id.Value}' is disabled.");
        }

        var definition = await _definitionStore.GetAsync(
            trigger.WorkflowDefinitionId,
            trigger.DefinitionVersion,
            cancellationToken);
        if (definition is null)
        {
            throw new KeyNotFoundException(
                $"Workflow definition '{trigger.WorkflowDefinitionId.Value}' version " +
                $"'{trigger.DefinitionVersion}' was not found.");
        }

        var execution = await _workflowEngine.ExecuteAsync(
            definition,
            new WorkflowExecutionRequest
            {
                ExecutionRequestId = context.ExecutionRequestId,
                CorrelationId = context.CorrelationId
            },
            cancellationToken);
        return execution.Id;
    }
}
