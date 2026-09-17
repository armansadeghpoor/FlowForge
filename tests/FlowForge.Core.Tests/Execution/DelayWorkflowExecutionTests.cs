using System.Text.Json;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Engine.Execution;
using FlowForge.Infrastructure.State;
using FlowForge.Nodes.BuiltIn.Delay;
using FlowForge.Nodes.Registry;

namespace FlowForge.Core.Tests.Execution;

public sealed class DelayWorkflowExecutionTests
{
    [Fact]
    public async Task ExecuteAsync_ZeroDurationDelay_PersistsSuccessfulExecution()
    {
        var delayRunner = new DelayNodeRunner();
        var registry = new NodeRunnerRegistry([delayRunner]);
        var stateStore = new InMemoryStateStore();
        var engine = new WorkflowEngine(new WorkflowExecutor(
            registry,
            stateStore,
            new ExecutionPipeline([])));
        var workflow = new WorkflowDefinition
        {
            Id = new WorkflowId(Guid.NewGuid()),
            Name = "Zero-duration delay",
            Nodes =
            [
                new NodeDefinition
                {
                    Id = new NodeId(Guid.NewGuid()),
                    Type = "delay",
                    Configuration = new Dictionary<string, JsonElement>
                    {
                        ["durationMs"] = JsonSerializer.SerializeToElement(0)
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
        Assert.NotNull(storedExecution);
        Assert.Equal(WorkflowExecutionStatus.Succeeded, storedExecution.Status);
        Assert.NotNull(storedNodeExecution);
        Assert.Equal(NodeExecutionStatus.Succeeded, storedNodeExecution.Status);
    }
}
