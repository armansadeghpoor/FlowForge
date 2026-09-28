using FlowForge.Abstractions.Sharing;
using FlowForge.Infrastructure.Persistence.PostgreSql;
using FlowForge.Infrastructure.Tests.PostgreSql;
using FlowForge.Infrastructure.Tests.Sharing.Conformance;

namespace FlowForge.Infrastructure.Tests.Sharing;

public sealed class PostgreSqlWorkflowSharingStoreTests :
    WorkflowSharingStoreConformanceTests,
    IAsyncLifetime
{
    private readonly PostgreSqlTestSchema _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    protected override IWorkflowSharingStore CreateStore() =>
        new PostgreSqlWorkflowSharingStore(_database.CreateConnectionFactory());
}
