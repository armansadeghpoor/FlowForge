using System.Text.Json;
using FlowForge.Abstractions.Definitions;
using FlowForge.Abstractions.Security;
using FlowForge.Abstractions.Triggers;
using FlowForge.Abstractions.Validation;
using FlowForge.Application.Definitions;
using FlowForge.Application.Triggers;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;

namespace FlowForge.Application.Tests;

public sealed class ApplicationServicesTests
{
    [Fact]
    public async Task DefinitionCreateAsync_ValidDefinition_IsPersisted()
    {
        var definition = CreateDefinition();
        var store = new FakeDefinitionStore();
        var authorization = new FakeAuthorizationService(AuthorizationDecision.Allow);
        var service = new WorkflowDefinitionService(
            new FakeDefinitionValidator(),
            store,
            authorization);

        var result = await service.CreateAsync(definition, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(definition, result.Value);
        Assert.Same(definition, store.SavedDefinition);
        Assert.Equal(definition.OwnerTenantId, result.Value!.OwnerTenantId);
        Assert.Equal(definition.OwnerTenantId, store.SavedDefinition!.OwnerTenantId);
        var request = Assert.Single(authorization.Requests);
        Assert.Equal(Permissions.WorkflowDefinitionsWrite, request.Permission);
        Assert.Equal(definition.OwnerTenantId, request.OwnerTenantId);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task DefinitionCreateAsync_InvalidDefinition_IsRejectedWithoutPersistence()
    {
        var store = new FakeDefinitionStore();
        var service = new WorkflowDefinitionService(
            new FakeDefinitionValidator(
                new ValidationError("DefinitionNameRequired", "Name is required.")),
            store,
            new FakeAuthorizationService(AuthorizationDecision.Allow));

        var result = await service.CreateAsync(
            CreateDefinition() with { Name = " " },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(store.SavedDefinition);
        var error = Assert.Single(result.Errors);
        Assert.Equal("DefinitionNameRequired", error.Code);
    }

    [Fact]
    public async Task DefinitionGetAsync_ExistingDefinition_IsReturned()
    {
        var definition = CreateDefinition();
        var store = new FakeDefinitionStore { DefinitionToReturn = definition };
        var service = new WorkflowDefinitionService(
            new FakeDefinitionValidator(),
            store,
            new FakeAuthorizationService(AuthorizationDecision.Allow));

        var result = await service.GetAsync(
            definition.Id,
            definition.Version,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(definition, result.Value);
    }

    [Fact]
    public async Task TriggerCreateAsync_ValidTrigger_IsPersisted()
    {
        var trigger = CreateTrigger();
        var store = new FakeTriggerStore();
        var service = new WorkflowTriggerService(store);

        var result = await service.CreateAsync(trigger, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(trigger, result.Value);
        Assert.Same(trigger, store.SavedTrigger);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task TriggerCreateAsync_InvalidTrigger_IsRejectedWithoutPersistence()
    {
        var store = new FakeTriggerStore();
        var service = new WorkflowTriggerService(store);
        var invalidTrigger = CreateTrigger() with
        {
            Id = default,
            WorkflowDefinitionId = default,
            DefinitionVersion = " ",
            Type = (TriggerType)999
        };

        var result = await service.CreateAsync(
            invalidTrigger,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(store.SavedTrigger);
        Assert.Equal(4, result.Errors.Count);
    }

    [Fact]
    public void ApplicationAssembly_DoesNotReferenceInfrastructure()
    {
        var references = typeof(WorkflowDefinitionService).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Contains("FlowForge.Abstractions", references);
        Assert.Contains("FlowForge.Core", references);
        Assert.DoesNotContain("FlowForge.Engine", references);
        Assert.DoesNotContain("FlowForge.Infrastructure", references);
    }

    [Fact]
    public async Task DefinitionCreateAsync_StoreFailure_ReturnsApplicationError()
    {
        var store = new FakeDefinitionStore { SaveException = new IOException("store detail") };
        var service = new WorkflowDefinitionService(
            new FakeDefinitionValidator(),
            store,
            new FakeAuthorizationService(AuthorizationDecision.Allow));

        var result = await service.CreateAsync(
            CreateDefinition(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("DefinitionPersistenceFailed", error.Code);
        Assert.DoesNotContain("store detail", error.Message);
    }

    [Fact]
    public async Task DefinitionCreateAsync_DeniedAuthorization_DoesNotPersist()
    {
        var store = new FakeDefinitionStore();
        var service = new WorkflowDefinitionService(
            new FakeDefinitionValidator(),
            store,
            new FakeAuthorizationService(AuthorizationDecision.Deny));

        var result = await service.CreateAsync(
            CreateDefinition(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(store.SavedDefinition);
        Assert.Equal("PermissionDenied", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task TriggerCreateAsync_StoreFailure_ReturnsApplicationError()
    {
        var store = new FakeTriggerStore { SaveException = new IOException("store detail") };
        var service = new WorkflowTriggerService(store);

        var result = await service.CreateAsync(
            CreateTrigger(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("TriggerPersistenceFailed", error.Code);
        Assert.DoesNotContain("store detail", error.Message);
    }

    private static WorkflowDefinition CreateDefinition() =>
        new()
        {
            Id = new WorkflowDefinitionId(Guid.NewGuid()),
            OwnerTenantId = new TenantId(Guid.NewGuid()),
            Name = "Application workflow",
            Version = "v1",
            Description = null,
            CreatedAt = new DateTime(2026, 9, 24, 15, 0, 0, DateTimeKind.Utc),
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

    private static WorkflowTrigger CreateTrigger() =>
        new()
        {
            Id = new WorkflowTriggerId(Guid.NewGuid()),
            WorkflowDefinitionId = new WorkflowDefinitionId(Guid.NewGuid()),
            DefinitionVersion = "v1",
            Type = TriggerType.Manual,
            Configuration = new Dictionary<string, JsonElement>()
        };

    private sealed class FakeDefinitionValidator(params ValidationError[] errors)
        : IWorkflowDefinitionValidator
    {
        public WorkflowDefinitionValidationResult Validate(WorkflowDefinition definition) =>
            new(errors);
    }

    private sealed class FakeDefinitionStore : IWorkflowDefinitionStore
    {
        public WorkflowDefinition? SavedDefinition { get; private set; }

        public WorkflowDefinition? DefinitionToReturn { get; init; }

        public Exception? SaveException { get; init; }

        public Task SaveAsync(
            WorkflowDefinition definition,
            CancellationToken cancellationToken)
        {
            if (SaveException is not null)
            {
                throw SaveException;
            }

            SavedDefinition = definition;
            return Task.CompletedTask;
        }

        public Task<WorkflowDefinition?> GetAsync(
            WorkflowDefinitionId id,
            string version,
            CancellationToken cancellationToken) =>
            Task.FromResult(DefinitionToReturn);

        public Task<IReadOnlyList<WorkflowDefinition>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkflowDefinition>>(
                DefinitionToReturn is null ? [] : [DefinitionToReturn]);
    }

    private sealed class FakeTriggerStore : IWorkflowTriggerStore
    {
        public WorkflowTrigger? SavedTrigger { get; private set; }

        public Exception? SaveException { get; init; }

        public Task SaveAsync(
            WorkflowTrigger trigger,
            CancellationToken cancellationToken)
        {
            if (SaveException is not null)
            {
                throw SaveException;
            }

            SavedTrigger = trigger;
            return Task.CompletedTask;
        }

        public Task<WorkflowTrigger?> GetAsync(
            WorkflowTriggerId id,
            CancellationToken cancellationToken) =>
            Task.FromResult<WorkflowTrigger?>(null);

        public Task<IReadOnlyList<WorkflowTrigger>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkflowTrigger>>([]);
    }

    private sealed class FakeAuthorizationService(AuthorizationDecision decision)
        : IAuthorizationService
    {
        public List<AuthorizationRequest> Requests { get; } = [];

        public Task<AuthorizationDecision> AuthorizeAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(decision);
        }
    }
}
