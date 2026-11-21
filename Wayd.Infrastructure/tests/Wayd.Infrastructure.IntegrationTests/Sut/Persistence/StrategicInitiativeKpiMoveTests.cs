using System.Data.Common;
using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.Kpis;
using Wayd.Common.Domain.Kpis;
using Wayd.Common.Domain.Settings;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Activities;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves Move-Strategic-Initiative-Kpis-To-Shared-Model carries each strategic initiative KPI, with its checkpoints and
/// measurements, into the shared KPI tables as the shared model holds it, with the baseline
/// <see cref="ActivityLogEntryFactory"/> builds for the same event; that Drop-Strategic-Initiative-Kpi-Tables moves a
/// KPI added in between without bringing back one removed since it moved; and that their Downs put the KPIs back.
/// </summary>
/// <remarks>
/// Both migrations are hand-written T-SQL over tables the current model no longer has, so each test runs against a
/// database of its own, migrated only as far as the migration before them, and seeds the old tables by hand.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class StrategicInitiativeKpiMoveTests(SqlServerDbContextFixture fixture)
{
    private const string Previous = "20261120142242_Add-Dependency-Planned-Start";
    private const string Move = "20261121131927_Move-Strategic-Initiative-Kpis-To-Shared-Model";
    private const string Drop = "20261121144716_Drop-Strategic-Initiative-Kpi-Tables";

    private const string Chicago = "America/Chicago";

    private readonly SqlServerDbContextFixture _fixture = fixture;

    static StrategicInitiativeKpiMoveTests()
    {
        PpmKpiSubjects.Register();
    }

    [Fact]
    public async Task Move_CopiesEachKpiAsTheSharedModelHoldsIt_ReadingDaysInTheSystemZone_WithItsBaseline()
    {
        await InDatabase(async (context, migrator, connectionString, ct) =>
        {
            // Arrange
            await migrator.MigrateAsync(Previous, ct);
            await SetZone(context, Chicago, ct);
            var seed = await SeedInitiative(context, ct);
            var revenue = await AddOldKpi(context, seed.InitiativeId, "Revenue", "Annual \"run\" rate", 100, 250, "$", "M", "Increase", 1, ct);
            var churn = await AddOldKpi(context, seed.InitiativeId, "Churn", null, null, 2.5, null, "%", "Decrease", 2, ct);
            await AddKpiAddedEntry(context, seed.InitiativeId, revenue, seed.EmployeeId, ct);
            // 03:30 UTC on 15 March is still 14 March in Chicago.
            await AddOldMeasurement(context, revenue, 120, "2026-03-15T03:30:00", seed.EmployeeId, "Q1 close", ct);
            await AddOldMeasurement(context, revenue, 130, "2026-03-14T15:00:00", seed.EmployeeId, null, ct);
            await AddOldCheckpoint(context, revenue, 150, "2026-06-30T04:00:00", " Q2 ", 140, ct);
            await AddOldCheckpoint(context, revenue, 250, "2026-12-31T18:00:00", "Q4", null, ct);

            // Act
            await migrator.MigrateAsync(Move, ct);

            // Assert
            await using var verify = _fixture.CreateContext(connectionString);
            var moved = await verify.Kpis.AsNoTracking()
                .Include(k => k.Measurements)
                .Include(k => k.Checkpoints)
                .Where(k => k.SubjectId == seed.InitiativeId)
                .OrderBy(k => k.Order)
                .ToListAsync(ct);

            moved.Select(k => k.Id).Should().Equal(revenue, churn);
            moved.Select(k => k.Order).Should().Equal(0, 1);
            moved[0].Key.Should().BeLessThan(moved[1].Key, "new keys keep the order the KPIs were created in");

            var first = moved[0];
            first.SubjectType.Should().Be(PpmKpiSubjects.StrategicInitiative.Key);
            first.Details.Should().Be(new KpiDetails("Revenue", "Annual \"run\" rate", null, "$", "M", KpiExpressedAs.ProgressToTarget, null));
            first.Goal.Should().Be(new KpiGoal(KpiTargetDirection.Increase, new KpiBaseline(100, null), KpiTarget.Absolute(250), null));
            first.Source.Should().Be(KpiSource.Manual);
            first.Status.Should().Be(KpiStatus.Active);
            first.Window.Should().Be(KpiMeasurementWindow.PointInTime);

            var march14 = new LocalDate(2026, 3, 14);
            first.Measurements.OrderBy(m => m.MeasuredAt)
                .Select(m => (m.Value, m.MeasuredAt, m.PeriodStart, m.PeriodEnd, m.MeasuredOn, m.MeasuredById, m.Note))
                .Should().Equal(
                    (130d, (Instant?)Instant.FromUtc(2026, 3, 14, 15, 0), march14, march14, march14, (Guid?)seed.EmployeeId, (string?)null),
                    (120d, (Instant?)Instant.FromUtc(2026, 3, 15, 3, 30), march14, march14, march14, (Guid?)seed.EmployeeId, (string?)"Q1 close"));

            first.CheckpointPlan.Should().Equal(
                new KpiCheckpointValues(new LocalDate(2026, 6, 29), "Q2", 150, 140),
                new KpiCheckpointValues(new LocalDate(2026, 12, 31), "Q4", 250, null));

            moved[1].Details.ExpressedAs.Should().Be(KpiExpressedAs.Value, "a KPI with no starting value has no progress to read");
            moved[1].Goal.Should().Be(new KpiGoal(KpiTargetDirection.Decrease, KpiBaseline.None, KpiTarget.Absolute(2.5), null));

            var entry = await verify.ActivityLogs.AsNoTracking()
                .Include(a => a.RelatedAggregates)
                .SingleAsync(a => a.AggregateType == "Kpi" && a.AggregateId == revenue, ct);
            var createdOn = Instant.FromDateTimeUtc(DateTime.SpecifyKind(await Scalar<DateTime>(context,
                $"SELECT [SystemCreated] FROM [Ppm].[StrategicInitiativeKpis] WHERE [Id] = '{revenue}';", ct), DateTimeKind.Utc));
            var expectedEvent = new KpiBaselinedEvent(first.Id, first.Key, first.SubjectType, first.SubjectId, first.Details, first.Goal,
                first.Source, first.Order, createdOn, seed.EmployeeId, entry.Timestamp, first.Window);
            var expected = ActivityLogEntryFactory.CreateActivityLogEntry(expectedEvent, first, 0, null);

            entry.EventId.Should().Be(expectedEvent.EventId);
            entry.Should().BeEquivalentTo(expected, options => options
                .Including(e => e.EventType)
                .Including(e => e.Category)
                .Including(e => e.EventVersion)
                .Including(e => e.DomainArea)
                .Including(e => e.AggregateType)
                .Including(e => e.AggregateId)
                .Including(e => e.ActorKind)
                .Including(e => e.UserId)
                .Including(e => e.EmployeeId)
                .Including(e => e.Ordinal)
                .Including(e => e.CorrelationId)
                .Including(e => e.Summary));
            JsonNode.DeepEquals(JsonNode.Parse(entry.Payload), JsonNode.Parse(expected.Payload))
                .Should().BeTrue($"the moved payload {entry.Payload} should match the serialized event {expected.Payload}");
            entry.RelatedAggregates.Select(r => (r.AggregateType, r.AggregateId))
                .Should().ContainSingle().Which.Should().Be(("StrategicInitiative", seed.InitiativeId));
        });
    }

    [Fact]
    public async Task Move_KpiTheSharedModelWouldRefuse_StopsAndMovesNothing()
    {
        await InDatabase(async (context, migrator, connectionString, ct) =>
        {
            // Arrange
            await migrator.MigrateAsync(Previous, ct);
            var seed = await SeedInitiative(context, ct);
            var kpi = await AddOldKpi(context, seed.InitiativeId, "NPS", null, 20, 40, null, null, "Increase", 1, ct);
            // The old plan accepted an at-risk value above the planned value of a KPI that should increase.
            await AddOldCheckpoint(context, kpi, 30, "2026-06-01T12:00:00", "Jun", 35, ct);

            // Act
            var act = () => migrator.MigrateAsync(Move, ct);

            // Assert
            (await act.Should().ThrowAsync<SqlException>())
                .Which.Message.Should().Contain("not short of its planned value");
            (await Scalar<int>(context, "SELECT COUNT(*) FROM [Kpis].[Kpis];", ct)).Should().Be(0);
        });
    }

    [Fact]
    public async Task Drop_MovesAKpiAddedSinceTheMove_AndNeverBringsBackOneRemovedSinceItMoved()
    {
        await InDatabase(async (context, migrator, connectionString, ct) =>
        {
            // Arrange
            await migrator.MigrateAsync(Previous, ct);
            var seed = await SeedInitiative(context, ct);
            var removed = await AddOldKpi(context, seed.InitiativeId, "Uptime", null, null, 99.9, null, "%", "Increase", 1, ct);
            await migrator.MigrateAsync(Move, ct);
            await Execute(context, $"DELETE FROM [Kpis].[Kpis] WHERE [Id] = '{removed}';", ct);
            var late = await AddOldKpi(context, seed.InitiativeId, "Adoption", null, 10, 60, null, "%", "Increase", 2, ct);

            // Act
            await migrator.MigrateAsync(Drop, ct);

            // Assert
            (await Rows(context, $"SELECT [Id] FROM [Kpis].[Kpis] WHERE [SubjectId] = '{seed.InitiativeId}';", ct))
                .Select(r => (Guid)r[0]).Should().Equal(late);
            (await Scalar<int>(context, $"SELECT COUNT(*) FROM [App].[ActivityLogs] WHERE [EventType] = 'KpiBaselinedEvent' AND [AggregateId] = '{late}';", ct))
                .Should().Be(1);
            (await Scalar<int>(context, "SELECT COUNT(*) FROM sys.tables WHERE [name] LIKE 'StrategicInitiativeKpi%';", ct)).Should().Be(0);
        });
    }

    [Fact]
    public async Task Down_PutsEveryInitiativeKpiBackInTheOldTables_AndAMoveReadsTheSameDaysAgain()
    {
        await InDatabase(async (context, migrator, connectionString, ct) =>
        {
            // Arrange
            await migrator.MigrateAsync(Previous, ct);
            await SetZone(context, Chicago, ct);
            var seed = await SeedInitiative(context, ct);
            var revenue = await AddOldKpi(context, seed.InitiativeId, "Revenue", "Run rate", 100, 250, "$", "M", "Increase", 1, ct);
            var churn = await AddOldKpi(context, seed.InitiativeId, "Churn", null, null, 2.5, null, "%", "Decrease", 2, ct);
            await AddOldMeasurement(context, revenue, 120, "2026-03-15T03:30:00", seed.EmployeeId, "Q1 close", ct);
            await AddOldMeasurement(context, revenue, 130, "2026-04-10T15:00:00", seed.EmployeeId, null, ct);
            await AddOldCheckpoint(context, revenue, 150, "2026-06-30T04:00:00", "Q2", 140, ct);
            await migrator.MigrateAsync(cancellationToken: ct);

            // Act
            await migrator.MigrateAsync(Previous, ct);

            // Assert
            (await Rows(context, $"""
                SELECT [Id], [Name], [Description], [StartingValue], [TargetValue], [ActualValue], [Prefix], [Suffix], [TargetDirection], [Order]
                FROM [Ppm].[StrategicInitiativeKpis] WHERE [StrategicInitiativeId] = '{seed.InitiativeId}' ORDER BY [Order];
                """, ct))
                .Select(r => r.Select(v => v is DBNull ? null : v).ToArray())
                .Should().BeEquivalentTo(new[]
                {
                    new object?[] { revenue, "Revenue", "Run rate", 100d, 250d, 130d, "$", "M", "Increase", 1 },
                    new object?[] { churn, "Churn", null, null, 2.5d, null, null, "%", "Decrease", 2 },
                }, options => options.WithStrictOrdering());

            (await Rows(context, $"SELECT [ActualValue], [MeasurementDate], [MeasuredById], [Note] FROM [Ppm].[StrategicInitiativeKpiMeasurements] WHERE [KpiId] = '{revenue}' ORDER BY [MeasurementDate];", ct))
                .Select(r => ((double)r[0], (DateTime)r[1], (Guid)r[2], r[3] as string))
                .Should().Equal(
                    (120d, new DateTime(2026, 3, 15, 3, 30, 0), seed.EmployeeId, "Q1 close"),
                    (130d, new DateTime(2026, 4, 10, 15, 0, 0), seed.EmployeeId, (string?)null));

            // Midnight of 29 June in Chicago, which the move read the checkpoint as.
            (await Rows(context, $"SELECT [TargetValue], [AtRiskValue], [CheckpointDate], [DateLabel] FROM [Ppm].[StrategicInitiativeKpiCheckpoints] WHERE [KpiId] = '{revenue}';", ct))
                .Select(r => ((double)r[0], (double)r[1], (DateTime)r[2], (string)r[3]))
                .Should().Equal((150d, 140d, new DateTime(2026, 6, 29, 5, 0, 0), "Q2"));

            (await Scalar<int>(context, "SELECT COUNT(*) FROM [Kpis].[Kpis];", ct)).Should().Be(0);
            (await Scalar<int>(context, "SELECT COUNT(*) FROM [App].[ActivityLogs] WHERE [EventType] = 'KpiBaselinedEvent';", ct)).Should().Be(0);

            await migrator.MigrateAsync(Move, ct);
            (await Scalar<DateTime>(context, $"SELECT [Date] FROM [Kpis].[Checkpoints] WHERE [KpiId] = '{revenue}';", ct))
                .Should().Be(new DateTime(2026, 6, 29));
        });
    }

    [Fact]
    public async Task Down_KpiTheOldModelCannotHold_StopsTheRollbackAndKeepsTheSharedKpis()
    {
        await InDatabase(async (context, migrator, connectionString, ct) =>
        {
            // Arrange
            await migrator.MigrateAsync(Previous, ct);
            var seed = await SeedInitiative(context, ct);
            var kpi = await AddOldKpi(context, seed.InitiativeId, "Cost", null, 500, 300, "$", "k", "Decrease", 1, ct);
            await migrator.MigrateAsync(cancellationToken: ct);
            // A relative target, which only the shared model can hold.
            await Execute(context, $"UPDATE [Kpis].[Kpis] SET [TargetKind] = 'RelativeChange', [TargetValue] = 20 WHERE [Id] = '{kpi}';", ct);

            // Act
            var act = () => migrator.MigrateAsync(Previous, ct);

            // Assert
            (await act.Should().ThrowAsync<SqlException>())
                .Which.Message.Should().Contain("cannot be rolled back").And.Contain("change from the baseline");
            (await Scalar<int>(context, $"SELECT COUNT(*) FROM [Kpis].[Kpis] WHERE [Id] = '{kpi}';", ct)).Should().Be(1);
        });
    }

    private sealed record Seeded(Guid InitiativeId, Guid EmployeeId);

    /// <summary>Runs <paramref name="test"/> against a fresh database of its own, deleted afterwards.</summary>
    private async Task InDatabase(Func<WaydDbContext, IMigrator, string, CancellationToken, Task> test)
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = $"wayd-migration-{Guid.NewGuid():N}",
        }.ConnectionString;

        await using var context = _fixture.CreateContext(connectionString);
        try
        {
            await test(context, context.GetService<IMigrator>(), connectionString, ct);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(ct);
        }
    }

    private static Task SetZone(WaydDbContext context, string zone, CancellationToken ct) =>
        Execute(context, $$"""
            INSERT INTO [App].[SystemSettings] ([Id], [Key], [Scope], [Value], [SchemaVersion], [SystemCreated], [SystemLastModified])
            VALUES ('{{SystemSettingsSection.IdFor<GeneralSettings>()}}', 'general', 'System', N'{"timeZone":"{{zone}}"}', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
            """, ct);

    /// <summary>An employee, and an active initiative in an active portfolio.</summary>
    private static async Task<Seeded> SeedInitiative(WaydDbContext context, CancellationToken ct)
    {
        var employee = Guid.NewGuid();
        var portfolio = Guid.NewGuid();
        var initiative = Guid.NewGuid();
        await Execute(context, $"""
            INSERT INTO [Organization].[Employees] ([Id], [FirstName], [LastName], [EmployeeNumber], [Email], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
            VALUES ('{employee}', N'Avery', N'Chen', N'E-1', N'e-1@acme.example', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
            INSERT INTO [Ppm].[Portfolios] ([Id], [Name], [Description], [Status], [SystemCreated], [SystemLastModified])
            VALUES ('{portfolio}', N'Digital', N'Digital portfolio', 'Active', SYSUTCDATETIME(), SYSUTCDATETIME());
            INSERT INTO [Ppm].[StrategicInitiatives] ([Id], [Name], [Description], [PortfolioId], [Status], [Start], [End], [SystemCreated], [SystemLastModified])
            VALUES ('{initiative}', N'Uplift', N'Customer experience uplift', '{portfolio}', 'Active', '2026-01-01', '2026-12-31', SYSUTCDATETIME(), SYSUTCDATETIME());
            """, ct);
        return new Seeded(initiative, employee);
    }

    private static async Task<Guid> AddOldKpi(WaydDbContext context, Guid initiativeId, string name, string? description,
        double? startingValue, double targetValue, string? prefix, string? suffix, string direction, int order, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        await Execute(context, $"""
            INSERT INTO [Ppm].[StrategicInitiativeKpis] ([Id], [StrategicInitiativeId], [Name], [Description], [StartingValue], [TargetValue],
                [ActualValue], [Prefix], [Suffix], [TargetDirection], [Order], [SystemCreated], [SystemLastModified])
            VALUES ('{id}', '{initiativeId}', {Text(name)}, {Text(description)}, {Number(startingValue)}, {Number(targetValue)},
                NULL, {Text(prefix)}, {Text(suffix)}, '{direction}', {order}, '2025-12-01T09:00:00.1234567', SYSUTCDATETIME());
            """, ct);
        return id;
    }

    private static Task AddOldMeasurement(WaydDbContext context, Guid kpiId, double value, string at, Guid employeeId, string? note, CancellationToken ct) =>
        Execute(context, $"""
            INSERT INTO [Ppm].[StrategicInitiativeKpiMeasurements] ([Id], [KpiId], [ActualValue], [MeasurementDate], [MeasuredById], [Note], [SystemCreated], [SystemLastModified])
            VALUES ('{Guid.CreateVersion7()}', '{kpiId}', {Number(value)}, '{at}', '{employeeId}', {Text(note)}, SYSUTCDATETIME(), SYSUTCDATETIME());
            """, ct);

    private static Task AddOldCheckpoint(WaydDbContext context, Guid kpiId, double target, string at, string label, double? atRisk, CancellationToken ct) =>
        Execute(context, $"""
            INSERT INTO [Ppm].[StrategicInitiativeKpiCheckpoints] ([Id], [KpiId], [TargetValue], [AtRiskValue], [CheckpointDate], [DateLabel], [SystemCreated], [SystemLastModified])
            VALUES ('{Guid.CreateVersion7()}', '{kpiId}', {Number(target)}, {Number(atRisk)}, '{at}', {Text(label)}, SYSUTCDATETIME(), SYSUTCDATETIME());
            """, ct);

    /// <summary>The entry the old model wrote when the KPI was added, which names who added it.</summary>
    private static Task AddKpiAddedEntry(WaydDbContext context, Guid initiativeId, Guid kpiId, Guid employeeId, CancellationToken ct) =>
        Execute(context, $$"""
            INSERT INTO [App].[ActivityLogs] ([EventId], [EventType], [Category], [EventVersion], [DomainArea], [AggregateType], [AggregateId],
                [ActorKind], [UserId], [EmployeeId], [Timestamp], [Ordinal], [CorrelationId], [Payload], [Summary])
            VALUES (NEWID(), 'StrategicInitiativeKpiAddedEvent', 'Created', '1.0', 'Ppm', 'StrategicInitiative', '{{initiativeId}}',
                'User', 'user-1', '{{employeeId}}', '2025-12-01T09:00:00', 0, NULL, N'{"kpiId":"{{kpiId}}"}', N'KPI Added');
            """, ct);

    private static string Text(string? value) => value is null ? "NULL" : $"N'{value.Replace("'", "''")}'";

    private static string Number(double? value) => value is null ? "NULL" : value.Value.ToString("R", CultureInfo.InvariantCulture);

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
