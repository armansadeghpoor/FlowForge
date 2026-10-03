using System.Text.Json;
using FlowForge.Abstractions.Auditing;
using FlowForge.Abstractions.Definitions;
using FlowForge.Abstractions.Observability;
using FlowForge.Abstractions.Security;
using FlowForge.Abstractions.Sharing;
using FlowForge.Abstractions.Tenancy;
using FlowForge.Abstractions.Validation;
using FlowForge.Application.Definitions;
using FlowForge.Application.Security;
using FlowForge.Application.Tests.Auditing;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Sharing;

namespace FlowForge.Application.Tests.Observability;

public sealed class ApplicationOperationalMetricsTests
{
    [Fact]
    public async Task DefinitionOperations_RecordAggregateCountersWithoutIdentifiers()
    {
        var definition = CreateDefinition();
        var metrics = new RecordingMetricsCollector();
        var service = new WorkflowDefinitionService(
            new ValidDefinitionValidator(),
            new DefinitionStore(),
            new AuthorizationStub(AuthorizationDecision.Allow),
            new SharingStoreStub(),
            new RecordingAuditStore(),
            StaticAuditContext.Create(tenantId: definition.OwnerTenantId),
            metrics);

        var created = await service.CreateAsync(definition, CancellationToken.None);
        var read = await service.GetAsync(
            definition.Id,
            definition.Version,
            CancellationToken.None);
        var listed = await service.ListAsync(CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.True(read.IsSuccess);
        Assert.True(listed.IsSuccess);
        var snapshot = metrics.GetSnapshot();
        Assert.Equal(1, snapshot.Counters[
            OperationalMetricNames.WorkflowDefinitionsCreated]);
        Assert.Equal(1, snapshot.Counters[
            OperationalMetricNames.WorkflowDefinitionsRead]);
        Assert.Equal(1, snapshot.Counters[
            OperationalMetricNames.WorkflowDefinitionsListed]);
        Assert.Equal(2, snapshot.Counters[
            OperationalMetricNames.AuditEventsRecorded]);
        Assert.All(snapshot.Counters.Keys, name =>
        {
            Assert.DoesNotContain(
                definition.OwnerTenantId.Value.ToString("D"),
                name,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                definition.Id.Value.ToString("D"),
                name,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task AuditProviderFailure_RecordsFailureWithoutChangingUseCaseResult()
    {
        var definition = CreateDefinition();
        var metrics = new RecordingMetricsCollector();
        var service = new WorkflowDefinitionService(
            new ValidDefinitionValidator(),
            new DefinitionStore(),
            new AuthorizationStub(AuthorizationDecision.Allow),
            new SharingStoreStub(),
            new ThrowingAuditStore(),
            StaticAuditContext.Create(),
            metrics);

        var result = await service.CreateAsync(definition, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = metrics.GetSnapshot();
        Assert.Equal(1, snapshot.Counters[
            OperationalMetricNames.WorkflowDefinitionsCreated]);
        Assert.Equal(1, snapshot.Counters[
            OperationalMetricNames.AuditEventsFailed]);
        Assert.False(snapshot.Counters.ContainsKey(
            OperationalMetricNames.AuditEventsRecorded));
    }

    [Fact]
    public async Task Authorization_RecordsAllowedAndDeniedDecisions()
    {
        var metrics = new RecordingMetricsCollector();
        var userContext = new UserContextStub(new SecurityContext(
            "user-1",
            true,
            [Permissions.WorkflowDefinitionsRead]));
        var service = new PermissionAuthorizationService(
            userContext,
            TenantContext.None,
            metrics);

        var allowed = await service.AuthorizeAsync(
            new AuthorizationRequest
            {
                Permission = Permissions.WorkflowDefinitionsRead
            },
            CancellationToken.None);
        var denied = await service.AuthorizeAsync(
            new AuthorizationRequest
            {
                Permission = Permissions.WorkflowDefinitionsWrite
            },
            CancellationToken.None);

        Assert.True(allowed.IsAllowed);
        Assert.False(denied.IsAllowed);
        var snapshot = metrics.GetSnapshot();
        Assert.Equal(1, snapshot.Counters[
            OperationalMetricNames.AuthorizationAllowed]);
        Assert.Equal(1, snapshot.Counters[
            OperationalMetricNames.AuthorizationDenied]);
    }

    private static WorkflowDefinition CreateDefinition() =>
        new()
        {
            Id = new WorkflowDefinitionId(Guid.NewGuid()),
            OwnerTenantId = new TenantId(Guid.NewGuid()),
            Name = "Metrics workflow",
            Version = "v1",
            Description = null,
            CreatedAt = DateTime.UtcNow,
            Nodes =
            [
                new NodeDefinition
                {
                    Id = new NodeId(Guid.NewGuid()),
                    Type = "test",
                    Configuration = new Dictionary<string, JsonElement>()
                }
            ],
            Edges = Array.Empty<EdgeDefinition>()
        };

    private sealed class ValidDefinitionValidator : IWorkflowDefinitionValidator
    {
        public WorkflowDefinitionValidationResult Validate(
            WorkflowDefinition definition) => new([]);
    }

    private sealed class DefinitionStore : IWorkflowDefinitionStore
    {
        private readonly List<WorkflowDefinition> _definitions = [];

        public Task SaveAsync(
            WorkflowDefinition definition,
            CancellationToken cancellationToken)
        {
            _definitions.Add(definition);
            return Task.CompletedTask;
        }

        public Task<WorkflowDefinition?> GetAsync(
            WorkflowDefinitionId id,
            string version,
            CancellationToken cancellationToken) =>
            Task.FromResult(_definitions.SingleOrDefault(definition =>
                definition.Id == id &&
                string.Equals(definition.Version, version, StringComparison.Ordinal)));

        public Task<IReadOnlyList<WorkflowDefinition>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkflowDefinition>>(
                _definitions.ToArray());
    }

    private sealed class SharingStoreStub : IWorkflowSharingStore
    {
        public Task SaveAsync(
            WorkflowSharing sharing,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<WorkflowSharing?> GetAsync(
            WorkflowDefinitionId workflowDefinitionId,
            string definitionVersion,
            CancellationToken cancellationToken) =>
            Task.FromResult<WorkflowSharing?>(null);
    }

    private sealed class AuthorizationStub(AuthorizationDecision decision)
        : IAuthorizationService
    {
        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken) => Task.FromResult(decision);
    }

    private sealed class UserContextStub(SecurityContext current) : IUserContext
    {
        public SecurityContext Current { get; } = current;
    }
}
