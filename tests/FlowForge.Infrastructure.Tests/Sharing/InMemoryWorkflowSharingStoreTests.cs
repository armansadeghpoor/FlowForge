using FlowForge.Abstractions.Sharing;
using FlowForge.Infrastructure.Sharing;
using FlowForge.Infrastructure.Tests.Sharing.Conformance;

namespace FlowForge.Infrastructure.Tests.Sharing;

public sealed class InMemoryWorkflowSharingStoreTests :
    WorkflowSharingStoreConformanceTests
{
    protected override IWorkflowSharingStore CreateStore() =>
        new InMemoryWorkflowSharingStore();
}
