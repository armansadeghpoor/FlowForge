using FlowForge.Core.Domain.Definitions;

namespace FlowForge.Abstractions.Validation;

/// <summary>
/// Validates workflow definitions independently of storage and execution.
/// </summary>
public interface IWorkflowDefinitionValidator
{
    /// <summary>
    /// Validates a workflow definition and returns every discovered error.
    /// </summary>
    /// <param name="definition">The workflow definition to validate.</param>
    /// <returns>The complete validation result.</returns>
    WorkflowDefinitionValidationResult Validate(WorkflowDefinition definition);
}
