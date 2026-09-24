using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text.Json;
using FlowForge.Abstractions.Triggers;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;

namespace FlowForge.Infrastructure.Triggers;

/// <summary>
/// Stores immutable workflow event trigger snapshots in memory.
/// </summary>
public sealed class InMemoryWorkflowEventTriggerStore : IWorkflowEventTriggerStore
{
    private readonly ConcurrentDictionary<WorkflowEventTriggerId, WorkflowEventTrigger>
        _eventTriggers = new();

    /// <inheritdoc />
    public Task SaveAsync(
        WorkflowEventTrigger eventTrigger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventTrigger);
        Validate(eventTrigger);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_eventTriggers.TryAdd(eventTrigger.Id, Snapshot(eventTrigger)))
        {
            throw new InvalidOperationException(
                $"Workflow event trigger '{eventTrigger.Id.Value}' already exists.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkflowEventTrigger?> GetAsync(
        WorkflowEventTriggerId id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var eventTrigger = _eventTriggers.TryGetValue(id, out var stored)
            ? Snapshot(stored)
            : null;

        return Task.FromResult(eventTrigger);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowEventTrigger>> ListAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<WorkflowEventTrigger> eventTriggers = Array.AsReadOnly(
            _eventTriggers.Values
                .OrderBy(eventTrigger => eventTrigger.CreatedAt)
                .ThenBy(eventTrigger => eventTrigger.Id.Value)
                .Select(Snapshot)
                .ToArray());

        return Task.FromResult(eventTriggers);
    }

    private static void Validate(WorkflowEventTrigger eventTrigger)
    {
        if (eventTrigger.Id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "A workflow event trigger identifier is required.",
                nameof(eventTrigger));
        }

        if (eventTrigger.WorkflowTriggerId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "A workflow trigger reference is required.",
                nameof(eventTrigger));
        }

        if (string.IsNullOrWhiteSpace(eventTrigger.EventType))
        {
            throw new ArgumentException(
                "A workflow event type is required.",
                nameof(eventTrigger));
        }

        if (!IsValidFilter(eventTrigger.Filter))
        {
            throw new ArgumentException(
                "Workflow event filter metadata must contain valid JSON values.",
                nameof(eventTrigger));
        }

        if (eventTrigger.CreatedAt == default)
        {
            throw new ArgumentException(
                "A workflow event trigger creation timestamp is required.",
                nameof(eventTrigger));
        }
    }

    private static bool IsValidFilter(
        IReadOnlyDictionary<string, JsonElement>? filter)
    {
        if (filter is null)
        {
            return true;
        }

        try
        {
            return filter.Values.All(value => value.ValueKind != JsonValueKind.Undefined);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static WorkflowEventTrigger Snapshot(WorkflowEventTrigger eventTrigger) =>
        eventTrigger with
        {
            Filter = eventTrigger.Filter is null
                ? null
                : new ReadOnlyDictionary<string, JsonElement>(
                    eventTrigger.Filter.ToDictionary(
                        item => item.Key,
                        item => item.Value.Clone(),
                        StringComparer.Ordinal))
        };
}
