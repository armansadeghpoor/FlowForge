using FlowForge.Abstractions.Execution;
using System.Collections.ObjectModel;
using System.Text.Json;
using FlowForge.Abstractions.Nodes;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Failures;

namespace FlowForge.Nodes.BuiltIn.Delay;

/// <summary>
/// Executes workflow nodes that asynchronously wait for a configured duration.
/// </summary>
public sealed class DelayNodeRunner : INodeRunner
{
    private static readonly NodeDescriptor DelayDescriptor = new()
    {
        Type = "delay",
        Version = "1.0",
        ConfigurationSchema = new ReadOnlyDictionary<string, NodePropertyDefinition>(
            new Dictionary<string, NodePropertyDefinition>(StringComparer.Ordinal)
            {
                ["durationMs"] = new NodePropertyDefinition
                {
                    Name = "durationMs",
                    Type = NodePropertyType.Integer,
                    Required = true
                }
            })
    };

    /// <inheritdoc />
    public string NodeType => Descriptor.Type;

    /// <inheritdoc />
    public NodeDescriptor Descriptor => DelayDescriptor;

    /// <inheritdoc />
    public async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.NodeDefinition.Configuration.TryGetValue("durationMs", out var durationElement))
        {
            return Failure("Delay node configuration requires 'durationMs'.");
        }

        if (durationElement.ValueKind != JsonValueKind.Number ||
            !durationElement.TryGetInt32(out var durationMilliseconds))
        {
            return Failure(
                "Delay node configuration 'durationMs' must be a JSON integer supported by Task.Delay.");
        }

        if (durationMilliseconds < 0)
        {
            return Failure("Delay node configuration 'durationMs' must be greater than or equal to zero.");
        }

        await Task.Delay(durationMilliseconds, cancellationToken);

        return new NodeExecutionResult
        {
            Success = true,
            Output = null,
            Failure = null
        };
    }

    private static NodeExecutionResult Failure(string message) =>
        new()
        {
            Success = false,
            Output = null,
            Failure = new NodeFailure
            {
                Category = NodeFailureCategory.Validation,
                Message = message
            }
        };
}
