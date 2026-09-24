using FlowForge.Abstractions.Nodes;
using FlowForge.Abstractions.Validation;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Graph;

namespace FlowForge.Engine.Validation;

/// <summary>
/// Validates workflow identity, node configuration, and graph structure.
/// </summary>
public sealed class WorkflowDefinitionValidator : IWorkflowDefinitionValidator
{
    private readonly INodeRunnerRegistry _nodeRunnerRegistry;
    private readonly WorkflowGraphValidator _graphValidator = new();

    /// <summary>
    /// Initializes a workflow definition validator.
    /// </summary>
    /// <param name="nodeRunnerRegistry">
    /// The registry used to obtain node configuration descriptors.
    /// </param>
    public WorkflowDefinitionValidator(INodeRunnerRegistry nodeRunnerRegistry)
    {
        ArgumentNullException.ThrowIfNull(nodeRunnerRegistry);
        _nodeRunnerRegistry = nodeRunnerRegistry;
    }

    /// <inheritdoc />
    public WorkflowDefinitionValidationResult Validate(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<ValidationError>();

        ValidateIdentity(definition, errors);
        ValidateNodes(definition.Nodes, errors);

        var graphResult = _graphValidator.Validate(definition);
        errors.AddRange(graphResult.Errors.Select(error =>
            new ValidationError(error.ErrorType.ToString(), error.Message)));

        return new WorkflowDefinitionValidationResult(errors);
    }

    private static void ValidateIdentity(
        WorkflowDefinition definition,
        ICollection<ValidationError> errors)
    {
        if (definition.Id.Value == Guid.Empty)
        {
            errors.Add(new ValidationError(
                "DefinitionIdRequired",
                "A workflow definition identifier is required."));
        }

        if (string.IsNullOrWhiteSpace(definition.Version))
        {
            errors.Add(new ValidationError(
                "DefinitionVersionRequired",
                "A workflow definition version is required."));
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            errors.Add(new ValidationError(
                "DefinitionNameRequired",
                "A workflow definition name is required."));
        }
    }

    private void ValidateNodes(
        IEnumerable<NodeDefinition> nodes,
        ICollection<ValidationError> errors)
    {
        foreach (var node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Type))
            {
                errors.Add(new ValidationError(
                    "NodeTypeRequired",
                    $"Node '{node.Id.Value}' must declare a node type."));
                continue;
            }

            var runner = _nodeRunnerRegistry.Get(node.Type);
            if (runner is null)
            {
                continue;
            }

            foreach (var property in runner.Descriptor.ConfigurationSchema.Values)
            {
                if (property.Required &&
                    !node.Configuration.ContainsKey(property.Name))
                {
                    errors.Add(new ValidationError(
                        "RequiredNodePropertyMissing",
                        $"Node '{node.Id.Value}' requires configuration property " +
                        $"'{property.Name}'."));
                }
            }
        }
    }
}
