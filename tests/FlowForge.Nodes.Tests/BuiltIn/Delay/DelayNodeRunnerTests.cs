using System.Text.Json;
using FlowForge.Abstractions.Nodes;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Nodes.BuiltIn.Delay;
using FlowForge.Nodes.Registry;

namespace FlowForge.Nodes.Tests.BuiltIn.Delay;

public sealed class DelayNodeRunnerTests
{
    private readonly DelayNodeRunner _runner = new();

    [Fact]
    public void NodeType_ReturnsDelay()
    {
        Assert.Equal("delay", _runner.NodeType);
    }

    [Fact]
    public void Descriptor_ExposesDelayTypeAndVersion()
    {
        Assert.Equal("delay", _runner.Descriptor.Type);
        Assert.Equal("1.0", _runner.Descriptor.Version);
    }

    [Fact]
    public void Descriptor_ContainsRequiredIntegerDurationSchema()
    {
        var property = Assert.Single(_runner.Descriptor.ConfigurationSchema).Value;

        Assert.Equal("durationMs", property.Name);
        Assert.Equal(NodePropertyType.Integer, property.Type);
        Assert.True(property.Required);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroDuration_Succeeds()
    {
        var result = await _runner.ExecuteAsync(ContextWithDuration(0), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_PositiveDuration_Succeeds()
    {
        var result = await _runner.ExecuteAsync(ContextWithDuration(1), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_MissingDuration_ReturnsFailure()
    {
        var result = await _runner.ExecuteAsync(
            Context(new Dictionary<string, JsonElement>()),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("durationMs", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_StringDuration_ReturnsFailure()
    {
        var result = await _runner.ExecuteAsync(
            ContextWithDuration("100"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("integer", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_FloatingPointDuration_ReturnsFailure()
    {
        var result = await _runner.ExecuteAsync(
            ContextWithDuration(1.5),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("integer", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeDuration_ReturnsFailure()
    {
        var result = await _runner.ExecuteAsync(ContextWithDuration(-1), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("greater than or equal to zero", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_DurationOutsideTaskDelayRange_ReturnsFailure()
    {
        var result = await _runner.ExecuteAsync(
            ContextWithDuration((long)int.MaxValue + 1),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Task.Delay", result.ErrorMessage ?? string.Empty);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_BooleanDuration_ReturnsFailure(bool duration)
    {
        var result = await _runner.ExecuteAsync(
            ContextWithDuration(duration),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("integer", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_NullDuration_ReturnsFailure()
    {
        var result = await _runner.ExecuteAsync(
            ContextWithDuration<object?>(null),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("integer", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_PropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _runner.ExecuteAsync(
                ContextWithDuration(10_000),
                cancellationSource.Token));
    }

    [Fact]
    public void Registry_DelayNodeRunner_ResolvesByNodeType()
    {
        var registry = new NodeRunnerRegistry([_runner]);

        var resolved = registry.Get("delay");

        Assert.Same(_runner, resolved);
    }

    private static NodeExecutionContext ContextWithDuration<T>(T duration) =>
        Context(new Dictionary<string, JsonElement>
        {
            ["durationMs"] = JsonSerializer.SerializeToElement(duration)
        });

    private static NodeExecutionContext Context(
        IReadOnlyDictionary<string, JsonElement> configuration) =>
        new()
        {
            Node = new NodeDefinition
            {
                Id = new NodeId(Guid.NewGuid()),
                Type = "delay",
                Configuration = configuration
            },
            ExecutionId = new WorkflowExecutionId(Guid.NewGuid())
        };
}
