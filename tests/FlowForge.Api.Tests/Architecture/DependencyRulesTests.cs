using FlowForge.Api.Controllers;

namespace FlowForge.Api.Tests.Architecture;

public sealed class DependencyRulesTests
{
    [Fact]
    public void Api_CompositionRootReferencesInfrastructureButNotEngine()
    {
        var references = typeof(WorkflowDefinitionsController).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("FlowForge.Engine", references);
        Assert.Contains("FlowForge.Infrastructure", references);
    }
}
