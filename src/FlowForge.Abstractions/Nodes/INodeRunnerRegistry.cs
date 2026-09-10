namespace FlowForge.Abstractions.Nodes;

/// <summary>
/// Defines lookup operations for registered node runners.
/// </summary>
public interface INodeRunnerRegistry
{
    /// <summary>
    /// Gets the runner registered for a node type.
    /// </summary>
    /// <param name="nodeType">The node type to look up.</param>
    /// <returns>The matching runner, or <see langword="null"/> when none is registered.</returns>
    INodeRunner? Get(string nodeType);
}
