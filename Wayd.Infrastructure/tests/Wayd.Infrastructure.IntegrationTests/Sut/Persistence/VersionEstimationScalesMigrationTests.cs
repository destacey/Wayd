using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Version-Estimation-Scales migration rekeys existing scales, moves their values onto version 1 with the
/// cards flagged, and pins existing poker sessions to it — and that its Down puts the old shape back.
/// </summary>
/// <remarks>
/// The migration is hand-written T-SQL over a schema the current model no longer has, so it runs against a database
/// of its own, migrated only as far as the migration before it.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class VersionEstimationScalesMigrationTests(SqlServerDbContextFixture fixture)
{
    private const string Previous = "20261101185605_Add-PokerRound-Description";
    private const string Target = "20261101221206_Version-Estimation-Scales";
    private const string FibonacciValues = """["1","2","3","5","?","∞","☕"]""";
    // Emoji compare equal to the ☕ card under the default collation, so these prove the cards are matched exactly.
    private const string SizeValues = """["🐜","🐭","🐶","🐘"]""";

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task UpThenDown_RekeysVersionsAndPinsScales_ThenRestoresThem()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var connectionString = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = $"wayd-migration-{Guid.NewGuid():N}",
        }.ConnectionString;

        await using var context = _fixture.CreateContext(connectionString);
        var migrator = context.GetService<IMigrator>();
        try
        {
            await migrator.MigrateAsync(Previous, ct);

            var sizesKey = await Scalar<int>(context, $"""
                INSERT INTO [Planning].[EstimationScales] ([Name], [Description], [IsActive], [Values], [SystemCreated], [SystemLastModified])
                VALUES (N'Sizes', NULL, 1, N'{SizeValues}', '2026-01-01', '2026-01-01');
                SELECT CAST(SCOPE_IDENTITY() AS int);
                """, ct);
            var fibonacciKey = await Scalar<int>(context, $"""
                INSERT INTO [Planning].[EstimationScales] ([Name], [Description], [IsActive], [Values], [SystemCreated], [SystemLastModified])
                VALUES (N'Fibonacci', N'Classic', 0, N'{FibonacciValues}', '2026-01-02T03:04:05', '2026-01-02');
                SELECT CAST(SCOPE_IDENTITY() AS int);
                """, ct);
            var sessionId = Guid.NewGuid();
            await Execute(context, $"""
                INSERT INTO [Planning].[PokerSessions] ([Id], [Name], [EstimationScaleId], [FacilitatorId], [Status], [ActivatedOn], [SystemCreated], [SystemLastModified])
                VALUES ('{sessionId}', N'Refinement', {fibonacciKey}, N'facilitator', 'Active', '2026-01-03', '2026-01-03', '2026-01-03');
                """, ct);

            // Act
            await migrator.MigrateAsync(Target, ct);

            // Assert
            var scaleId = await Scalar<Guid>(context, $"SELECT [Id] FROM [Planning].[EstimationScales] WHERE [Key] = {fibonacciKey};", ct);
            scaleId.Should().NotBe(Guid.Empty);
            (await Scalar<int>(context, "SELECT COUNT(DISTINCT [Id]) FROM [Planning].[EstimationScales];", ct)).Should().Be(2);

            var versions = await Rows(context, $"""
                SELECT [Id], [Number], [PublishedOn], [Values] FROM [Planning].[EstimationScaleVersions]
                WHERE [EstimationScaleId] = '{scaleId}';
                """, ct);
            var version = versions.Should().ContainSingle().Subject;
            var versionId = (Guid)version[0];
            version[1].Should().Be(1);
            version[2].Should().Be(new DateTime(2026, 1, 2, 3, 4, 5));

            var values = await Rows(context, $"""
                SELECT JSON_VALUE(j.[value], '$.Value'), CAST(JSON_VALUE(j.[value], '$.IsEstimate') AS bit)
                FROM [Planning].[EstimationScaleVersions] v CROSS APPLY OPENJSON(v.[Values]) j
                WHERE v.[Id] = '{versionId}'
                ORDER BY CAST(j.[key] AS int);
                """, ct);
            values.Select(r => ((string)r[0], (bool)r[1])).Should().Equal(
                ("1", true), ("2", true), ("3", true), ("5", true), ("?", false), ("∞", false), ("☕", false));

            var session = (await Rows(context, $"""
                SELECT [EstimationScaleId], [EstimationScaleVersionId] FROM [Planning].[PokerSessions] WHERE [Id] = '{sessionId}';
                """, ct)).Single();
            var sizesValues = await Rows(context, $"""
                SELECT JSON_VALUE(j.[value], '$.Value'), CAST(JSON_VALUE(j.[value], '$.IsEstimate') AS bit)
                FROM [Planning].[EstimationScales] s
                INNER JOIN [Planning].[EstimationScaleVersions] v ON v.[EstimationScaleId] = s.[Id] AND v.[Number] = 1
                CROSS APPLY OPENJSON(v.[Values]) j
                WHERE s.[Key] = {sizesKey}
                ORDER BY CAST(j.[key] AS int);
                """, ct);
            sizesValues.Select(r => ((string)r[0], (bool)r[1])).Should().Equal(
                ("🐜", true), ("🐭", true), ("🐶", true), ("🐘", true));
            (await Scalar<int>(context, "SELECT COUNT(*) FROM [Planning].[EstimationScaleVersions];", ct)).Should().Be(2);

            session[0].Should().Be(scaleId);
            session[1].Should().Be(versionId);

            // The rest of the way, so EF reads the rows through the current model.
            await migrator.MigrateAsync(cancellationToken: ct);
            await using (var read = _fixture.CreateContext(connectionString))
            {
                var scale = await read.EstimationScales.AsNoTracking().Include(s => s.Versions)
                    .SingleAsync(s => s.Key == fibonacciKey, ct);
                scale.Id.Should().Be(scaleId);
                scale.Name.Should().Be("Fibonacci");
                scale.IsActive.Should().BeFalse();
                scale.CurrentVersion.Id.Should().Be(versionId);
                scale.CurrentVersion.Values.Select(v => (v.Value, v.IsEstimate)).Should().Equal(
                    ("1", true), ("2", true), ("3", true), ("5", true), ("?", false), ("∞", false), ("☕", false));

                var pokerSession = await read.PokerSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId, ct);
                pokerSession.EstimationScaleVersionId.Should().Be(versionId);
            }

            await migrator.MigrateAsync(Previous, ct);

            (await Scalar<string>(context, $"SELECT [Values] FROM [Planning].[EstimationScales] WHERE [Id] = {fibonacciKey};", ct))
                .Should().Be(FibonacciValues);
            (await Scalar<string>(context, $"SELECT [Values] FROM [Planning].[EstimationScales] WHERE [Id] = {sizesKey};", ct))
                .Should().Be(SizeValues);
            (await Scalar<int>(context, $"SELECT [EstimationScaleId] FROM [Planning].[PokerSessions] WHERE [Id] = '{sessionId}';", ct))
                .Should().Be(fibonacciKey);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(ct);
        }
    }

    [Theory]
    [InlineData("""["1","?","☕"]""")]
    [InlineData("""["1","2","1"]""")]
    public async Task Up_AScaleThatCannotMakeAValidVersion_StopsTheMigration(string values)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var connectionString = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = $"wayd-migration-{Guid.NewGuid():N}",
        }.ConnectionString;

        await using var context = _fixture.CreateContext(connectionString);
        var migrator = context.GetService<IMigrator>();
        try
        {
            await migrator.MigrateAsync(Previous, ct);
            await Execute(context, $"""
                INSERT INTO [Planning].[EstimationScales] ([Name], [Description], [IsActive], [Values], [SystemCreated], [SystemLastModified])
                VALUES (N'Broken', NULL, 1, N'{values}', '2026-01-01', '2026-01-01');
                """, ct);

            // Act
            var act = () => migrator.MigrateAsync(Target, ct);

            // Assert
            (await act.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("for: Broken.");
            (await Scalar<int>(context, "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('Planning.EstimationScales') AND name = 'Values';", ct))
                .Should().Be(1, "the migration rolls back whole");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(ct);
        }
    }

    private static async Task<DbConnection> Open(WaydDbContext context, CancellationToken ct)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);
        return connection;
    }

    private static async Task Execute(WaydDbContext context, string sql, CancellationToken ct)
    {
        var connection = await Open(context, ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<T> Scalar<T>(WaydDbContext context, string sql, CancellationToken ct)
    {
        var connection = await Open(context, ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync(ct))!;
    }

    private static async Task<List<object[]>> Rows(WaydDbContext context, string sql, CancellationToken ct)
    {
        var connection = await Open(context, ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<object[]>();
        while (await reader.ReadAsync(ct))
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(row);
        }
        return rows;
    }
}
