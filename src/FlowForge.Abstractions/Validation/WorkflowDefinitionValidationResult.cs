namespace FlowForge.Abstractions.Validation;

/// <summary>
/// Represents the complete result of validating a workflow definition.
/// </summary>
public sealed class WorkflowDefinitionValidationResult
{
    /// <summary>
    /// Initializes a workflow definition validation result.
    /// </summary>
    /// <param name="errors">The validation errors that were found.</param>
    public WorkflowDefinitionValidationResult(IEnumerable<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = Array.AsReadOnly(errors.ToArray());
    }

    /// <summary>
    /// Gets a value indicating whether the definition is valid.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets all validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }
}
