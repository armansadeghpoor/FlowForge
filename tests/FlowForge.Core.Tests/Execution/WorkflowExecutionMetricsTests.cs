using System.Text.Json;
using FlowForge.Abstractions.Execution;
using FlowForge.Abstractions.Nodes;
using FlowForge.Abstractions.Observability;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Enums;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Engine.Execution;
using FlowForge.Infrastructure.State;

namespace FlowForge.Core.Tests.Execution;

public sealed class WorkflowExecutionMetricsTests
{
    [Fact]
    public async Task ExecuteAsync_RecordsStartedTerminalOutcomeAndDurationMetrics()
    {
        var metrics = new RecordingMetricsCollector();
        var stateStore = new InMemoryStateStore();
        var engine = new WorkflowEngine(new WorkflowExecutor(
            new RunnerRegistry(new SucceedingRunner()),
            stateStore,
            new ExecutionPipeline([]),
            metrics));

        var succeeded = await engine.ExecuteAsync(
            Workflow("success"),
            CancellationToken.None);
        var failed = await engine.ExecuteAsync(
            Workflow("missing"),
            CancellationToken.None);

        Assert.Equal(WorkflowExecutionStatus.Succeeded, succeeded.Status);
        Assert.Equal(WorkflowExecutionStatus.Failed, failed.Status);
        var snapshot = metrics.GetSnapshot();
        Assert.Equal(2, snapshot.Counters[
            OperationalMetricNames.WorkflowExecutionsStarted]);
        Assert.Equal(2, snapshot.Counters[
            OperationalMetricNames.WorkflowExecutionsCompleted]);
        Assert.Equal(1, snapshot.Counters[
            OperationalMetricNames.WorkflowExecutionsSucceeded]);
        Assert.Equal(1, snapshot.Counters[
            OperationalMetricNames.WorkflowExecutionsFailed]);
        var duration = snapshot.Durations[
            OperationalMetricNames.WorkflowExecutionDuration];
        Assert.Equal(2, duration.Count);
        Assert.True(duration.Total >= TimeSpan.Zero);
    }

    private static WorkflowDefinition Workflow(string nodeType) =>
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
                    Type = nodeType,
                    Configuration = new Dictionary<string, JsonElement>()
                }
            ],
            Edges = Array.Empty<EdgeDefinition>()
        };

    private sealed class RunnerRegistry(INodeRunner runner) : INodeRunnerRegistry
    {
        public INodeRunner? Get(string nodeType) =>
            string.Equals(nodeType, runner.NodeType, StringComparison.Ordinal)
                ? runner
                : null;
    }

    private sealed class SucceedingRunner : INodeRunner
    {
        public string NodeType => "success";

        public NodeDescriptor Descriptor { get; } = new()
        {
            Type = "success",
            Version = "1.0",
            ConfigurationSchema =
                new Dictionary<string, NodePropertyDefinition>()
        };

        public Task<NodeExecutionResult> ExecuteAsync(
            NodeExecutionContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(new NodeExecutionResult
            {
                Success = true,
                Output = null,
                Failure = null
            });
    }

    private sealed class RecordingMetricsCollector : IMetricsCollector
    {
        private readonly Dictionary<string, long> _counters =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<TimeSpan>> _durations =
            new(StringComparer.Ordinal);

        public void IncrementCounter(string name, long amount = 1) =>
            _counters[name] = _counters.GetValueOrDefault(name) + amount;

        public void RecordDuration(string name, TimeSpan duration)
        {
            if (!_durations.TryGetValue(name, out var observations))
            {
                observations = [];
                _durations.Add(name, observations);
            }

            observations.Add(duration);
        }

        public MetricsSnapshot GetSnapshot() =>
            new(
                _counters,
                _durations.Select(pair =>
                {
                    var total = TimeSpan.FromTicks(
                        pair.Value.Sum(duration => duration.Ticks));
                    return new KeyValuePair<string, DurationMetricSnapshot>(
                        pair.Key,
                        new DurationMetricSnapshot(
                            pair.Value.Count,
                            total,
                            TimeSpan.FromTicks(total.Ticks / pair.Value.Count)));
                }));
    }
}
