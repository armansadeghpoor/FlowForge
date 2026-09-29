using FlowForge.Infrastructure.Health;
using FlowForge.Infrastructure.Persistence.PostgreSql;
using FlowForge.Infrastructure.Tests.PostgreSql;

namespace FlowForge.Infrastructure.Tests.Health;

public sealed class PostgreSqlReadinessCheckTests : PostgreSqlIntegrationTestBase
{
    [SkippableFact]
    public async Task IsReadyAsync_AvailableDatabase_ReturnsTrue()
    {
        var check = new PostgreSqlReadinessCheck(CreateConnectionFactory());

        var isReady = await check.IsReadyAsync(CancellationToken.None);

        Assert.True(isReady);
    }

    [Fact]
    public async Task IsReadyAsync_UnavailableDatabase_ReturnsFalse()
    {
        var check = new PostgreSqlReadinessCheck(
            new PostgreSqlConnectionFactory(
                "Host=127.0.0.1;Port=1;Database=flowforge;Username=test;" +
                "Password=do-not-expose;Timeout=1;Command Timeout=1"));

        var isReady = await check.IsReadyAsync(CancellationToken.None);

        Assert.False(isReady);
    }

    [Fact]
    public async Task IsReadyAsync_CallerCancellation_Propagates()
    {
        var check = new PostgreSqlReadinessCheck(
            new PostgreSqlConnectionFactory(
                "Host=127.0.0.1;Port=1;Database=flowforge;Username=test;" +
                "Password=test;Timeout=1;Command Timeout=1"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            check.IsReadyAsync(cancellation.Token));
    }
}
