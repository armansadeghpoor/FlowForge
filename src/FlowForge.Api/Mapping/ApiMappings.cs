using System.Collections.ObjectModel;
using System.Text.Json;
using FlowForge.Abstractions.Queries;
using FlowForge.Api.Contracts;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.History;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;

namespace FlowForge.Api.Mapping;

internal static class ApiMappings
{
    public static WorkflowDefinition ToDomain(this CreateWorkflowDefinitionRequest request) =>
        new()
        {
            Id = new WorkflowDefinitionId(request.Id),
            OwnerTenantId = new TenantId(request.OwnerTenantId),
            Name = request.Name,
            Version = request.Version,
            Description = request.Description,
            CreatedAt = request.CreatedAt,
            Nodes = Array.AsReadOnly(request.Nodes
                .Select(node => new NodeDefinition
                {
                    Id = new NodeId(node.Id),
                    Type = node.Type,
                    Configuration = Clone(node.Configuration)
                })
                .ToArray()),
            Edges = Array.AsReadOnly(request.Edges
                .Select(edge => new EdgeDefinition
                {
                    From = new NodeId(edge.From),
                    To = new NodeId(edge.To)
                })
                .ToArray())
        };

    public static WorkflowDefinitionDto ToDto(this WorkflowDefinition definition) =>
        new()
        {
            Id = definition.Id.Value,
            OwnerTenantId = definition.OwnerTenantId.Value,
            Name = definition.Name,
            Version = definition.Version,
            Description = definition.Description,
            CreatedAt = definition.CreatedAt,
            Nodes = Array.AsReadOnly(definition.Nodes
                .Select(node => new WorkflowNodeDto
                {
                    Id = node.Id.Value,
                    Type = node.Type,
                    Configuration = Clone(node.Configuration)
                })
                .ToArray()),
            Edges = Array.AsReadOnly(definition.Edges
                .Select(edge => new WorkflowEdgeDto
                {
                    From = edge.From.Value,
                    To = edge.To.Value
                })
                .ToArray())
        };

    public static WorkflowTrigger ToDomain(this CreateWorkflowTriggerRequest request) =>
        new()
        {
            Id = new WorkflowTriggerId(request.Id),
            WorkflowDefinitionId = new WorkflowDefinitionId(request.WorkflowDefinitionId),
            DefinitionVersion = request.DefinitionVersion,
            Type = ParseTriggerType(request.Type),
            Enabled = request.Enabled,
            Configuration = Clone(request.Configuration)
        };

    public static WorkflowTriggerDto ToDto(this WorkflowTrigger trigger) =>
        new()
        {
            Id = trigger.Id.Value,
            WorkflowDefinitionId = trigger.WorkflowDefinitionId.Value,
            DefinitionVersion = trigger.DefinitionVersion,
            Type = trigger.Type.ToString(),
            Enabled = trigger.Enabled,
            Configuration = Clone(trigger.Configuration)
        };

    public static ExecutionSummaryDto ToDto(this ExecutionSummary summary) =>
        new()
        {
            WorkflowExecutionId = summary.WorkflowExecutionId.Value,
            CorrelationId = summary.CorrelationId.Value,
            Status = summary.Status.ToString(),
            DefinitionVersion = summary.DefinitionVersion,
            StartedAt = summary.StartedAt,
            CompletedAt = summary.CompletedAt,
            OwnerId = summary.OwnerId,
            LastHeartbeatAt = summary.LastHeartbeatAt,
            NodeExecutionCounts = new NodeExecutionCountsDto
            {
                Total = summary.NodeExecutionCounts.Total,
                Pending = summary.NodeExecutionCounts.Pending,
                Running = summary.NodeExecutionCounts.Running,
                Succeeded = summary.NodeExecutionCounts.Succeeded,
                Failed = summary.NodeExecutionCounts.Failed,
                Cancelled = summary.NodeExecutionCounts.Cancelled
            }
        };

    public static ExecutionTimelineEntryDto ToDto(this ExecutionHistoryEntry entry) =>
        new()
        {
            Id = entry.Id.Value,
            WorkflowExecutionId = entry.WorkflowExecutionId.Value,
            NodeExecutionId = entry.NodeExecutionId?.Value,
            EventType = entry.EventType.ToString(),
            Timestamp = entry.Timestamp,
            Metadata = entry.Metadata?.Clone()
        };

    private static TriggerType ParseTriggerType(string value) =>
        Enum.TryParse<TriggerType>(value, ignoreCase: false, out var type) && Enum.IsDefined(type)
            ? type
            : (TriggerType)(-1);

    private static IReadOnlyDictionary<string, JsonElement> Clone(
        IReadOnlyDictionary<string, JsonElement> source) =>
        new ReadOnlyDictionary<string, JsonElement>(source.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.Ordinal));
}
