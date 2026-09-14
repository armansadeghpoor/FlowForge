using FlowForge.Abstractions.Nodes;

namespace FlowForge.Nodes.Registry;

/// <summary>
/// Provides immutable lookup of node runners by node type.
/// </summary>
public sealed class NodeRunnerRegistry : INodeRunnerRegistry
{
    private readonly IReadOnlyDictionary<string, INodeRunner> _runners;

    /// <summary>
    /// Initializes a registry from the supplied node runners.
    /// </summary>
    /// <param name="runners">The node runners to register.</param>
    public NodeRunnerRegistry(IEnumerable<INodeRunner> runners)
    {
        ArgumentNullException.ThrowIfNull(runners);

        var runnerLookup = new Dictionary<string, INodeRunner>(StringComparer.Ordinal);

        foreach (var runner in runners)
        {
            if (runner is null)
            {
                throw new ArgumentException(
                    "The node runner collection cannot contain null elements.",
                    nameof(runners));
            }

            if (string.IsNullOrWhiteSpace(runner.NodeType))
            {
                throw new ArgumentException(
                    "A node runner must declare a non-empty node type.",
                    nameof(runners));
            }

            if (!runnerLookup.TryAdd(runner.NodeType, runner))
            {
                throw new InvalidOperationException(
                    $"A node runner is already registered for node type '{runner.NodeType}'.");
            }
        }

        _runners = runnerLookup;
    }

    /// <inheritdoc />
    public INodeRunner? Get(string nodeType)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        return _runners.GetValueOrDefault(nodeType);
    }
}
