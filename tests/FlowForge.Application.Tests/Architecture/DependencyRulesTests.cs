using FlowForge.Application.Definitions;

namespace FlowForge.Application.Tests.Architecture;

public sealed class DependencyRulesTests
{
    [Fact]
    public void Application_DoesNotReferenceEngineOrInfrastructure()
    {
        var references = typeof(WorkflowDefinitionService).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("FlowForge.Engine", references);
        Assert.DoesNotContain("FlowForge.Infrastructure", references);
    }
}
