using System.Text.Json;
using FlowForge.Core.Domain.Definitions;
using FlowForge.Core.Domain.Identifiers;
using FlowForge.Infrastructure.Definitions;

namespace FlowForge.Infrastructure.Tests.Definitions;

public sealed class InMemoryWorkflowDefinitionStoreTests
{
    [Fact]
    public async Task SaveAsync_Definition_CanBeRetrievedByIdAndVersion()
    {
        var store = new InMemoryWorkflowDefinitionStore();
        var definition = CreateDefinition(version: "v1");

        await store.SaveAsync(definition, CancellationToken.None);
        var retrieved = await store.GetAsync(
            definition.Id,
            definition.Version,
            CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(definition.Id, retrieved.Id);
        Assert.Equal(definition.OwnerTenantId, retrieved.OwnerTenantId);
        Assert.Equal(definition.Name, retrieved.Name);
        Assert.Equal(definition.Version, retrieved.Version);
        Assert.Equal(definition.Description, retrieved.Description);
        Assert.Equal(definition.CreatedAt, retrieved.CreatedAt);
        Assert.Equal(definition.Nodes, retrieved.Nodes);
        Assert.Equal(definition.Edges, retrieved.Edges);
    }

    [Fact]
    public async Task SaveAsync_MultipleVersionsOfSameDefinition_Coexist()
    {
        var store = new InMemoryWorkflowDefinitionStore();
        var definitionId = new WorkflowDefinitionId(Guid.NewGuid());
        var firstVersion = CreateDefinition(definitionId, "v1");
        var secondVersion = CreateDefinition(definitionId, "v2");

        await Task.WhenAll(
            store.SaveAsync(firstVersion, CancellationToken.None),
            store.SaveAsync(secondVersion, CancellationToken.None));
        var definitions = await store.ListAsync(CancellationToken.None);

        Assert.Equal(2, definitions.Count);
        Assert.Contains(definitions, definition => definition.Version == "v1");
        Assert.Contains(definitions, definition => definition.Version == "v2");
        Assert.All(definitions, definition => Assert.Equal(definitionId, definition.Id));
    }

    [Fact]
    public async Task GetAsync_MissingDefinition_ReturnsNull()
    {
        var store = new InMemoryWorkflowDefinitionStore();

        var retrieved = await store.GetAsync(
            new WorkflowDefinitionId(Guid.NewGuid()),
            "missing",
            CancellationToken.None);

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task SaveAsync_IsolatesStoredAndReturnedSnapshots()
    {
        var configuration = new Dictionary<string, JsonElement>
        {
            ["durationMs"] = JsonSerializer.SerializeToElement(10)
        };
        var nodes = new List<NodeDefinition>
        {
            new()
            {
                Id = new NodeId(Guid.NewGuid()),
                Type = "delay",
                Configuration = configuration
            }
        };
        var edges = new List<EdgeDefinition>();
        var definition = CreateDefinition(nodes: nodes, edges: edges);
        var store = new InMemoryWorkflowDefinitionStore();
        await store.SaveAsync(definition, CancellationToken.None);

        configuration["durationMs"] = JsonSerializer.SerializeToElement(99);
        nodes.Clear();
        edges.Add(new EdgeDefinition
        {
            From = new NodeId(Guid.NewGuid()),
            To = new NodeId(Guid.NewGuid())
        });
        var firstRead = await store.GetAsync(
            definition.Id,
            definition.Version,
            CancellationToken.None);
        var secondRead = await store.GetAsync(
            definition.Id,
            definition.Version,
            CancellationToken.None);

        Assert.NotNull(firstRead);
        Assert.NotNull(secondRead);
        var storedNode = Assert.Single(firstRead.Nodes);
        Assert.Equal(10, storedNode.Configuration["durationMs"].GetInt32());
        Assert.Empty(firstRead.Edges);
        Assert.NotSame(firstRead, secondRead);
        Assert.NotSame(firstRead.Nodes, secondRead.Nodes);
        Assert.NotSame(
            Assert.Single(firstRead.Nodes).Configuration,
            Assert.Single(secondRead.Nodes).Configuration);
    }

    private static WorkflowDefinition CreateDefinition(
        WorkflowDefinitionId? id = null,
        string version = "v1",
        IReadOnlyList<NodeDefinition>? nodes = null,
        IReadOnlyList<EdgeDefinition>? edges = null) =>
        new()
        {
            Id = id ?? new WorkflowDefinitionId(Guid.NewGuid()),
            OwnerTenantId = new TenantId(Guid.NewGuid()),
            Name = "Managed workflow",
            Version = version,
            Description = "Managed definition test fixture.",
            CreatedAt = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
            Nodes = nodes ?? Array.Empty<NodeDefinition>(),
            Edges = edges ?? Array.Empty<EdgeDefinition>()
        };
}
