using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text.Json;
using FlowForge.Abstractions.Triggers;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;

namespace FlowForge.Infrastructure.Triggers;

/// <summary>
/// Stores immutable workflow trigger snapshots in memory.
/// </summary>
public sealed class InMemoryWorkflowTriggerStore : IWorkflowTriggerStore
{
    private readonly ConcurrentDictionary<WorkflowTriggerId, WorkflowTrigger> _triggers = new();

    /// <inheritdoc />
    public Task SaveAsync(
        WorkflowTrigger trigger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        Validate(trigger);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_triggers.TryAdd(trigger.Id, Snapshot(trigger)))
        {
            throw new InvalidOperationException(
                $"Workflow trigger '{trigger.Id.Value}' already exists.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkflowTrigger?> GetAsync(
        WorkflowTriggerId id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var trigger = _triggers.TryGetValue(id, out var stored)
            ? Snapshot(stored)
            : null;

        return Task.FromResult(trigger);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowTrigger>> ListAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<WorkflowTrigger> triggers = Array.AsReadOnly(
            _triggers.Values
                .OrderBy(trigger => trigger.Id.Value)
                .Select(Snapshot)
                .ToArray());

        return Task.FromResult(triggers);
    }

    private static void Validate(WorkflowTrigger trigger)
    {
        if (trigger.Id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "A workflow trigger identifier is required.",
                nameof(trigger));
        }

        if (trigger.WorkflowDefinitionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "A workflow definition identifier is required.",
                nameof(trigger));
        }

        if (string.IsNullOrWhiteSpace(trigger.DefinitionVersion))
        {
            throw new ArgumentException(
                "A workflow definition version is required.",
                nameof(trigger));
        }

        if (!Enum.IsDefined(trigger.Type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(trigger),
                trigger.Type,
                "The workflow trigger type is not defined.");
        }
    }

    private static WorkflowTrigger Snapshot(WorkflowTrigger trigger) =>
        trigger with
        {
            Configuration = new ReadOnlyDictionary<string, JsonElement>(
                trigger.Configuration.ToDictionary(
                    item => item.Key,
                    item => item.Value.Clone(),
                    StringComparer.Ordinal))
        };
}
