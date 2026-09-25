using FlowForge.Api.Controllers;

namespace FlowForge.Api.Tests.Architecture;

public sealed class DependencyRulesTests
{
    [Fact]
    public void Api_DoesNotReferenceEngineOrInfrastructure()
    {
        var references = typeof(WorkflowDefinitionsController).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("FlowForge.Engine", references);
        Assert.DoesNotContain("FlowForge.Infrastructure", references);
    }
}
