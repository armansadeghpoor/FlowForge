using FlowForge.Abstractions.Engine;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Executions;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Graph;

namespace FlowForge.Engine.Execution;

/// <summary>
/// Initializes workflow execution instances from valid workflow definitions.
/// </summary>
public sealed class WorkflowEngine : IWorkflowEngine
{
    /// <inheritdoc />
    public Task<WorkflowExecution> ExecuteAsync(
        WorkflowDefinition workflow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        if (!WorkflowGraph.TryCreate(workflow, out _, out var validationResult))
        {
            var validationDetails = string.Join(
                Environment.NewLine,
                validationResult.Errors.Select(error => $"{error.ErrorType}: {error.Message}"));

            throw new InvalidOperationException(
                $"Workflow graph validation failed:{Environment.NewLine}{validationDetails}");
        }

        var execution = new WorkflowExecution
        {
            Id = new WorkflowExecutionId(Guid.NewGuid()),
            WorkflowId = workflow.Id,
            Status = WorkflowExecutionStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            StartedAt = null,
            CompletedAt = null,
            Nodes = Array.Empty<NodeExecutionState>()
        };

        return Task.FromResult(execution);
    }
}
