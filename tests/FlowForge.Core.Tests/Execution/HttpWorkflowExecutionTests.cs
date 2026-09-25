using System.Net;
using System.Text.Json;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Engine.Execution;
using FlowForge.Infrastructure.State;
using FlowForge.Nodes.BuiltIn.Http;
using FlowForge.Nodes.Registry;

namespace FlowForge.Core.Tests.Execution;

public sealed class HttpWorkflowExecutionTests
{
    [Fact]
    public async Task ExecuteAsync_HttpNode_PersistsSuccessfulExecutionAndOutput()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler());
        var httpRunner = new HttpNodeRunner(httpClient);
        var registry = new NodeRunnerRegistry([httpRunner]);
        var stateStore = new InMemoryStateStore();
        var engine = new WorkflowEngine(new WorkflowExecutor(
            registry,
            stateStore,
            new ExecutionPipeline([])));
        var workflow = new WorkflowDefinition
        {
            Id = new WorkflowDefinitionId(Guid.NewGuid()),
            OwnerTenantId = new TenantId(Guid.NewGuid()),
            Name = "HTTP workflow",
            Version = "http-v1",
            Description = null,
            CreatedAt = DateTime.UtcNow,
            Nodes =
            [
                new NodeDefinition
                {
                    Id = new NodeId(Guid.NewGuid()),
                    Type = "http",
                    Configuration = new Dictionary<string, JsonElement>
                    {
                        ["method"] = JsonSerializer.SerializeToElement("GET"),
                        ["url"] = JsonSerializer.SerializeToElement("https://example.test/resource")
                    }
                }
            ],
            Edges = Array.Empty<EdgeDefinition>()
        };

        var execution = await engine.ExecuteAsync(workflow, CancellationToken.None);
        var nodeExecution = Assert.Single(execution.Nodes);
        var storedExecution = await stateStore.GetExecutionAsync(
            execution.Id,
            CancellationToken.None);
        var storedNodeExecution = await stateStore.GetNodeExecutionAsync(
            execution.Id,
            nodeExecution.Id,
            CancellationToken.None);

        Assert.Equal(WorkflowExecutionStatus.Succeeded, execution.Status);
        Assert.Equal(NodeExecutionStatus.Succeeded, nodeExecution.Status);
        var output = Assert.IsType<HttpResponseOutput>(nodeExecution.Output?.Value);
        Assert.Equal(200, output.StatusCode);
        Assert.Equal("integration response", output.Body);

        Assert.NotNull(storedExecution);
        Assert.Equal(WorkflowExecutionStatus.Succeeded, storedExecution.Status);
        Assert.NotNull(storedNodeExecution);
        Assert.Equal(NodeExecutionStatus.Succeeded, storedNodeExecution.Status);
        Assert.Equal(nodeExecution.Output, storedNodeExecution.Output);
        var storedOutput = Assert.IsType<HttpResponseOutput>(storedNodeExecution.Output?.Value);
        Assert.Equal(output, storedOutput);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("integration response")
            });
    }
}
