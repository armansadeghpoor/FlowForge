using FlowForge.Abstractions.Auditing;
using FlowForge.Infrastructure.Persistence.PostgreSql;
using FlowForge.Infrastructure.Tests.Auditing.Conformance;
using FlowForge.Infrastructure.Tests.PostgreSql;

namespace FlowForge.Infrastructure.Tests.Auditing;

public sealed class PostgreSqlAuditStoreTests :
    AuditStoreConformanceTests,
    IAsyncLifetime
{
    private readonly PostgreSqlTestSchema _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    protected override IAuditStore CreateStore() =>
        new PostgreSqlAuditStore(_database.CreateConnectionFactory());
}
