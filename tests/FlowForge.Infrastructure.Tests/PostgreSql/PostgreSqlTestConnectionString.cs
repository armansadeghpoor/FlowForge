using Npgsql;

namespace FlowForge.Infrastructure.Tests.PostgreSql;

internal static class PostgreSqlTestConnectionString
{
    public static string CreateSchemaScoped(
        string baseConnectionString,
        string schemaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        return new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = schemaName,
            Pooling = false
        }.ConnectionString;
    }
}
