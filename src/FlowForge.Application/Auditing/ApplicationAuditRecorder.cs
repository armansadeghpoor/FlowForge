using System.Text.Json;
using FlowForge.Abstractions.Auditing;
using FlowForge.Core.Domain.Auditing;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Application.Auditing;

internal sealed class ApplicationAuditRecorder(
    IAuditStore auditStore,
    IAuditContext auditContext)
{
    public async Task TryRecordAsync(
        string action,
        string resourceType,
        string resourceIdentifier,
        AuditOutcome outcome,
        TenantId? resourceTenantId,
        CancellationToken cancellationToken,
        ExecutionCorrelationId? correlationId = null,
        JsonElement? metadata = null)
    {
        try
        {
            var context = auditContext.Current;
            var entry = new AuditEntry
            {
                Id = new AuditEntryId(Guid.NewGuid()),
                UserId = context.UserId,
                TenantId = context.TenantId,
                ResourceTenantId = resourceTenantId,
                Action = action,
                ResourceType = resourceType,
                ResourceIdentifier = resourceIdentifier,
                Outcome = outcome,
                CorrelationId = correlationId ?? context.CorrelationId,
                Timestamp = DateTime.UtcNow,
                Metadata = metadata
            };

            await auditStore.AppendAsync(entry, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Audit provider failures must not replace the use-case authorization or result.
        }
    }
}

internal static class ApplicationAuditActions
{
    public const string WorkflowDefinitionCreate = "WorkflowDefinition.Create";

    public const string WorkflowDefinitionRead = "WorkflowDefinition.Read";

    public const string WorkflowTriggerExecute = "WorkflowTrigger.Execute";
}

internal static class ApplicationAuditResourceTypes
{
    public const string WorkflowDefinition = "WorkflowDefinition";

    public const string WorkflowTrigger = "WorkflowTrigger";
}
