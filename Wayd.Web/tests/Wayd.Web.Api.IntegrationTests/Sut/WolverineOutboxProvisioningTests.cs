using Microsoft.Data.SqlClient;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Wolverine's durable inbox/outbox envelope tables are provisioned by Weasel at host startup, in a
/// dedicated <c>wolverine</c> schema, PARALLEL to (not inside) our EF migrations. This proves the two
/// provisioning paths coexist against the real SQL Server schema — the real host boots,
/// <c>InitializeDatabases()</c> applies the <c>Wayd.Infrastructure.Migrators.MSSQL</c> migrations to the
/// application schema, AND Wolverine's tables land in the <c>wolverine</c> schema, on the same database,
/// without either clobbering the other. This asserts the provisioning; durable delivery itself is covered by
/// <c>DurableEventRoutingTests</c>.
/// </summary>
[Trait("Category", "Docker")]
public sealed class WolverineOutboxProvisioningTests(WaydSqlServerApiFactory factory)
    : IClassFixture<WaydSqlServerApiFactory>
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Host_ProvisionsWolverineEnvelopeTables_InDedicatedSchema_AlongsideEfMigrations()
    {
        // Arrange — creating the client boots the real host: EF migrations apply to the app schema and
        // Wolverine's resource-setup provisions the envelope tables in the "wolverine" schema.
        _ = _factory.CreateClient();

        await using var connection = new SqlConnection(_factory.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        // Act
        var wolverineSchemaTables = await QuerySchemaTableNames(connection, "wolverine", TestContext.Current.CancellationToken);
        var dboSchemaTables = await QuerySchemaTableNames(connection, "dbo", TestContext.Current.CancellationToken);
        var allTables = await QueryAllTableNames(connection, TestContext.Current.CancellationToken);

        // Assert — the durable-messaging tables exist in the dedicated schema...
        Assert.Contains("wolverine_outgoing_envelopes", wolverineSchemaTables);
        Assert.Contains("wolverine_incoming_envelopes", wolverineSchemaTables);
        Assert.Contains("wolverine_dead_letters", wolverineSchemaTables);

        // ...did NOT leak into dbo (the guard that we picked a dedicated schema)...
        Assert.DoesNotContain("wolverine_outgoing_envelopes", dboSchemaTables);

        // ...while the application schema still migrated normally (coexistence, not replacement): the
        // Organization Teams table is created by our EF migrations, in its own "Organization" schema.
        Assert.Contains("Organization.Teams", allTables);
    }

    private static async Task<HashSet<string>> QuerySchemaTableNames(
        SqlConnection connection, string schema, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT t.name FROM sys.tables t " +
            "JOIN sys.schemas s ON t.schema_id = s.schema_id " +
            "WHERE s.name = @schema;";
        command.Parameters.AddWithValue("@schema", schema);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static async Task<HashSet<string>> QueryAllTableNames(SqlConnection connection, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT s.name + '.' + t.name FROM sys.tables t " +
            "JOIN sys.schemas s ON t.schema_id = s.schema_id;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
