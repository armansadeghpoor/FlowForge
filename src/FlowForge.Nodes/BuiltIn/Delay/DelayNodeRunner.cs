using System.Text.Json;
using FlowForge.Abstractions.Nodes;

namespace FlowForge.Nodes.BuiltIn.Delay;

/// <summary>
/// Executes workflow nodes that asynchronously wait for a configured duration.
/// </summary>
public sealed class DelayNodeRunner : INodeRunner
{
    /// <inheritdoc />
    public string NodeType => "delay";

    /// <inheritdoc />
    public async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Node.Configuration.TryGetValue("durationMs", out var durationElement))
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
            ErrorMessage = null
        };
    }

    private static NodeExecutionResult Failure(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}
