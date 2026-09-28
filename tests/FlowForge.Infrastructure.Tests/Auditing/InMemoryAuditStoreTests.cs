using FlowForge.Abstractions.Auditing;
using FlowForge.Infrastructure.Auditing;
using FlowForge.Infrastructure.Tests.Auditing.Conformance;

namespace FlowForge.Infrastructure.Tests.Auditing;

public sealed class InMemoryAuditStoreTests : AuditStoreConformanceTests
{
    protected override IAuditStore CreateStore() => new InMemoryAuditStore();
}
