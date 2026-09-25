using System.Reflection;
using System.Text.Json;
using FlowForge.Abstractions.Queries;
using FlowForge.Api.Contracts;
using FlowForge.Api.Controllers;
using FlowForge.Application.Common;
using FlowForge.Application.Definitions;
using FlowForge.Application.Queries;
using FlowForge.Application.Triggers;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.History;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Core.Domain.Triggers;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Tests.Controllers;

public sealed class ApiControllerTests
{
    [Fact]
    public async Task WorkflowCreation_ReturnsCreatedDto()
    {
        var definition = CreateDefinition();
        var service = new DefinitionServiceStub
        {
            CreateResult = ApplicationResult<WorkflowDefinition>.Success(definition)
        };
        var controller = new WorkflowDefinitionsController(service);

        var result = await controller.CreateAsync(
            CreateDefinitionRequest(definition),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<WorkflowDefinitionDto>(created.Value);
        Assert.Equal(definition.Id.Value, response.Id);
        Assert.Equal(definition.Version, response.Version);
        Assert.NotSame(definition, response);
    }

    [Fact]
    public async Task InvalidWorkflowRequest_ReturnsBadRequest()
    {
        var service = new DefinitionServiceStub
        {
            CreateResult = ApplicationResult<WorkflowDefinition>.Failure(
                new ApplicationError("DefinitionVersionRequired", "A version is required."))
        };
        var controller = new WorkflowDefinitionsController(service);

        var result = await controller.CreateAsync(
            CreateDefinitionRequest(CreateDefinition()),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("DefinitionVersionRequired", Assert.Single(response.Errors).Code);
    }

    [Fact]
    public async Task WorkflowRetrieval_ReturnsDto()
    {
        var definition = CreateDefinition();
        var service = new DefinitionServiceStub
        {
            GetResult = ApplicationResult<WorkflowDefinition?>.Success(definition)
        };
        var controller = new WorkflowDefinitionsController(service);

        var result = await controller.GetAsync(
            definition.Id.Value,
            definition.Version,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<WorkflowDefinitionDto>(ok.Value);
        Assert.Equal(definition.Name, response.Name);
        Assert.Single(response.Nodes);
    }

    [Fact]
    public async Task TriggerCreation_ReturnsCreatedDto()
    {
        var trigger = CreateTrigger();
        var service = new TriggerServiceStub
        {
            CreateResult = ApplicationResult<WorkflowTrigger>.Success(trigger)
        };
        var controller = new TriggersController(service);

        var result = await controller.CreateAsync(
            new CreateWorkflowTriggerRequest
            {
                Id = trigger.Id.Value,
                WorkflowDefinitionId = trigger.WorkflowDefinitionId.Value,
                DefinitionVersion = trigger.DefinitionVersion,
                Type = trigger.Type.ToString(),
                Configuration = trigger.Configuration
            },
            CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        var response = Assert.IsType<WorkflowTriggerDto>(created.Value);
        Assert.Equal("Manual", response.Type);
    }

    [Fact]
    public async Task ExecutionQueries_ReturnSummaryAndTimelineDtos()
    {
        var executionId = new WorkflowExecutionId(Guid.NewGuid());
        var correlationId = new ExecutionCorrelationId(Guid.NewGuid());
        var history = new ExecutionHistoryEntry
        {
            Id = new ExecutionHistoryId(Guid.NewGuid()),
            WorkflowExecutionId = executionId,
            NodeExecutionId = null,
            EventType = ExecutionHistoryEventType.WorkflowCreated,
            Timestamp = DateTime.UtcNow,
            Metadata = null
        };
        var service = new ExecutionQueryServiceStub
        {
            SummaryResult = ApplicationResult<ExecutionSummary>.Success(
                new ExecutionSummary
                {
                    WorkflowExecutionId = executionId,
                    CorrelationId = correlationId,
                    Status = WorkflowExecutionStatus.Running,
                    DefinitionVersion = "2.0",
                    StartedAt = DateTime.UtcNow,
                    CompletedAt = null,
                    OwnerId = "worker-a",
                    LastHeartbeatAt = DateTime.UtcNow,
                    NodeExecutionCounts = new NodeExecutionCounts
                    {
                        Total = 1,
                        Pending = 0,
                        Running = 1,
                        Succeeded = 0,
                        Failed = 0,
                        Cancelled = 0
                    }
                }),
            TimelineResult = ApplicationResult<IReadOnlyList<ExecutionHistoryEntry>>.Success([history])
        };
        var controller = new ExecutionsController(service);

        var summaryResult = await controller.GetSummaryAsync(
            executionId.Value,
            CancellationToken.None);
        var timelineResult = await controller.GetTimelineAsync(
            executionId.Value,
            CancellationToken.None);

        var summary = Assert.IsType<ExecutionSummaryDto>(
            Assert.IsType<OkObjectResult>(summaryResult).Value);
        Assert.Equal("Running", summary.Status);
        Assert.Equal(correlationId.Value, summary.CorrelationId);
        Assert.Equal(1, summary.NodeExecutionCounts.Running);

        var timeline = Assert.IsType<ExecutionTimelineEntryDto[]>(
            Assert.IsType<OkObjectResult>(timelineResult).Value);
        Assert.Equal("WorkflowCreated", Assert.Single(timeline).EventType);
    }

    [Fact]
    public async Task ExecutionCorrelationQuery_ReturnsMatchingSummaries()
    {
        var correlationId = new ExecutionCorrelationId(Guid.NewGuid());
        var summary = new ExecutionSummary
        {
            WorkflowExecutionId = new WorkflowExecutionId(Guid.NewGuid()),
            CorrelationId = correlationId,
            Status = WorkflowExecutionStatus.Succeeded,
            DefinitionVersion = "1.0",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            OwnerId = null,
            LastHeartbeatAt = null,
            NodeExecutionCounts = new NodeExecutionCounts
            {
                Total = 0,
                Pending = 0,
                Running = 0,
                Succeeded = 0,
                Failed = 0,
                Cancelled = 0
            }
        };
        var service = new ExecutionQueryServiceStub
        {
            CorrelationResult =
                ApplicationResult<IReadOnlyList<ExecutionSummary>>.Success([summary])
        };

        var result = await new ExecutionsController(service)
            .FindExecutionsByCorrelationIdAsync(correlationId.Value, CancellationToken.None);

        var response = Assert.IsType<ExecutionSummaryDto[]>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(correlationId.Value, Assert.Single(response).CorrelationId);
    }

    [Fact]
    public async Task MissingResources_ReturnNotFound()
    {
        var definitionService = new DefinitionServiceStub
        {
            GetResult = ApplicationResult<WorkflowDefinition?>.Success(null)
        };
        var queryService = new ExecutionQueryServiceStub
        {
            SummaryResult = ApplicationResult<ExecutionSummary>.Failure(
                new ApplicationError("ExecutionNotFound", "The execution was not found."))
        };

        var workflowResult = await new WorkflowDefinitionsController(definitionService)
            .GetAsync(Guid.NewGuid(), "1.0", CancellationToken.None);
        var executionResult = await new ExecutionsController(queryService)
            .GetSummaryAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(workflowResult);
        Assert.IsType<NotFoundObjectResult>(executionResult);
    }

    [Fact]
    public void ApiProject_DoesNotReferenceInfrastructureOrEngine()
    {
        var references = typeof(WorkflowDefinitionsController).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("FlowForge.Infrastructure", references);
        Assert.DoesNotContain("FlowForge.Engine", references);
    }

    private static WorkflowDefinition CreateDefinition()
    {
        using var document = JsonDocument.Parse("{\"durationMs\":0}");
        var node = new NodeDefinition
        {
            Id = new NodeId(Guid.NewGuid()),
            Type = "delay",
            Configuration = new Dictionary<string, JsonElement>
            {
                ["durationMs"] = document.RootElement.GetProperty("durationMs").Clone()
            }
        };

        return new WorkflowDefinition
        {
            Id = new WorkflowDefinitionId(Guid.NewGuid()),
            Name = "API workflow",
            Version = "1.0",
            Description = "Created through the API boundary.",
            CreatedAt = DateTime.UtcNow,
            Nodes = [node],
            Edges = []
        };
    }

    private static CreateWorkflowDefinitionRequest CreateDefinitionRequest(
        WorkflowDefinition definition) =>
        new()
        {
            Id = definition.Id.Value,
            Name = definition.Name,
            Version = definition.Version,
            Description = definition.Description,
            CreatedAt = definition.CreatedAt,
            Nodes = definition.Nodes.Select(node => new WorkflowNodeDto
            {
                Id = node.Id.Value,
                Type = node.Type,
                Configuration = node.Configuration
            }).ToArray(),
            Edges = definition.Edges.Select(edge => new WorkflowEdgeDto
            {
                From = edge.From.Value,
                To = edge.To.Value
            }).ToArray()
        };

    private static WorkflowTrigger CreateTrigger() =>
        new()
        {
            Id = new WorkflowTriggerId(Guid.NewGuid()),
            WorkflowDefinitionId = new WorkflowDefinitionId(Guid.NewGuid()),
            DefinitionVersion = "1.0",
            Type = TriggerType.Manual,
            Configuration = new Dictionary<string, JsonElement>()
        };

    private sealed class DefinitionServiceStub : IWorkflowDefinitionService
    {
        public ApplicationResult<WorkflowDefinition> CreateResult { get; init; } =
            ApplicationResult<WorkflowDefinition>.Failure(
                new ApplicationError("Unexpected", "Not configured."));

        public ApplicationResult<WorkflowDefinition?> GetResult { get; init; } =
            ApplicationResult<WorkflowDefinition?>.Failure(
                new ApplicationError("Unexpected", "Not configured."));

        public Task<ApplicationResult<WorkflowDefinition>> CreateAsync(
            WorkflowDefinition definition,
            CancellationToken cancellationToken) => Task.FromResult(CreateResult);

        public Task<ApplicationResult<WorkflowDefinition?>> GetAsync(
            WorkflowDefinitionId id,
            string version,
            CancellationToken cancellationToken) => Task.FromResult(GetResult);

        public Task<ApplicationResult<IReadOnlyList<WorkflowDefinition>>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(ApplicationResult<IReadOnlyList<WorkflowDefinition>>.Success([]));
    }

    private sealed class TriggerServiceStub : IWorkflowTriggerService
    {
        public ApplicationResult<WorkflowTrigger> CreateResult { get; init; } =
            ApplicationResult<WorkflowTrigger>.Failure(
                new ApplicationError("Unexpected", "Not configured."));

        public Task<ApplicationResult<WorkflowTrigger>> CreateAsync(
            WorkflowTrigger trigger,
            CancellationToken cancellationToken) => Task.FromResult(CreateResult);

        public Task<ApplicationResult<WorkflowTrigger?>> GetAsync(
            WorkflowTriggerId id,
            CancellationToken cancellationToken) =>
            Task.FromResult(ApplicationResult<WorkflowTrigger?>.Success(null));

        public Task<ApplicationResult<IReadOnlyList<WorkflowTrigger>>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(ApplicationResult<IReadOnlyList<WorkflowTrigger>>.Success([]));
    }

    private sealed class ExecutionQueryServiceStub : IWorkflowExecutionQueryService
    {
        public ApplicationResult<ExecutionSummary> SummaryResult { get; init; } =
            ApplicationResult<ExecutionSummary>.Failure(
                new ApplicationError("Unexpected", "Not configured."));

        public ApplicationResult<IReadOnlyList<ExecutionHistoryEntry>> TimelineResult { get; init; } =
            ApplicationResult<IReadOnlyList<ExecutionHistoryEntry>>.Success([]);

        public ApplicationResult<IReadOnlyList<ExecutionSummary>> CorrelationResult { get; init; } =
            ApplicationResult<IReadOnlyList<ExecutionSummary>>.Success([]);

        public Task<ApplicationResult<ExecutionSummary>> GetSummaryAsync(
            WorkflowExecutionId executionId,
            CancellationToken cancellationToken) => Task.FromResult(SummaryResult);

        public Task<ApplicationResult<IReadOnlyList<ExecutionSummary>>> FindExecutionsByCorrelationIdAsync(
            ExecutionCorrelationId correlationId,
            CancellationToken cancellationToken) => Task.FromResult(CorrelationResult);

        public Task<ApplicationResult<IReadOnlyList<ExecutionHistoryEntry>>> GetTimelineAsync(
            WorkflowExecutionId executionId,
            CancellationToken cancellationToken) => Task.FromResult(TimelineResult);
    }
}
