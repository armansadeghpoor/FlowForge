using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text.Json;
using FlowForge.Abstractions.Definitions;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Infrastructure.Definitions;

/// <summary>
/// Stores immutable workflow definition versions in memory.
/// </summary>
public sealed class InMemoryWorkflowDefinitionStore : IWorkflowDefinitionStore
{
    private readonly ConcurrentDictionary<DefinitionKey, WorkflowDefinition> _definitions = new();

    /// <inheritdoc />
    public Task SaveAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_definitions.TryAdd(
                new DefinitionKey(definition.Id, definition.Version),
                Snapshot(definition)))
        {
            throw new InvalidOperationException(
                $"Workflow definition '{definition.Id.Value}' version " +
                $"'{definition.Version}' already exists.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkflowDefinition?> GetAsync(
        WorkflowDefinitionId id,
        string version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        cancellationToken.ThrowIfCancellationRequested();

        var definition = _definitions.TryGetValue(
            new DefinitionKey(id, version),
            out var stored)
                ? Snapshot(stored)
                : null;

        return Task.FromResult(definition);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowDefinition>> ListAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<WorkflowDefinition> definitions = Array.AsReadOnly(
            _definitions.Values
                .OrderBy(definition => definition.CreatedAt)
                .ThenBy(definition => definition.Id.Value)
                .ThenBy(definition => definition.Version, StringComparer.Ordinal)
                .Select(Snapshot)
                .ToArray());

        return Task.FromResult(definitions);
    }

    private static WorkflowDefinition Snapshot(WorkflowDefinition definition) =>
        definition with
        {
            Nodes = Array.AsReadOnly(definition.Nodes.Select(Snapshot).ToArray()),
            Edges = Array.AsReadOnly(
                definition.Edges.Select(edge => edge with { }).ToArray())
        };

    private static NodeDefinition Snapshot(NodeDefinition node) =>
        node with
        {
            Configuration = new ReadOnlyDictionary<string, JsonElement>(
                node.Configuration.ToDictionary(
                    item => item.Key,
                    item => item.Value.Clone(),
                    StringComparer.Ordinal))
        };

    private readonly record struct DefinitionKey(
        WorkflowDefinitionId Id,
        string Version);
}
