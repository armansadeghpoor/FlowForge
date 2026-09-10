namespace FlowForge.Core.Graph;

/// <summary>
/// Represents the outcome of validating a workflow graph definition.
/// </summary>
public sealed class GraphValidationResult
{
    internal GraphValidationResult(IEnumerable<GraphValidationError> errors)
    {
        Errors = Array.AsReadOnly(errors.ToArray());
    }

    /// <summary>
    /// Gets a value indicating whether the workflow graph is valid.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<GraphValidationError> Errors { get; }
}
