using System.Data.Common;
using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Kpis;
using Wayd.Common.Domain.Kpis;
using Wayd.Common.Domain.Settings;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
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
/// database migrated only as far as the migration before them, untouched by the test before it, and seeds the old
/// tables by hand.
/// </remarks>
public sealed class StrategicInitiativeKpiMoveTests(SqlServerMigrationFixture fixture) : IClassFixture<SqlServerMigrationFixture>
{
    private const string Previous = "20261120142242_Add-Dependency-Planned-Start";
    private const string Move = "20261121131927_Move-Strategic-Initiative-Kpis-To-Shared-Model";
    private const string Drop = "20261121144716_Drop-Strategic-Initiative-Kpi-Tables";

    private const string Chicago = "America/Chicago";
    private const string Tokyo = "Asia/Tokyo";

    private static readonly Instant Now = Instant.FromUtc(2026, 11, 20, 12, 0);

    private readonly SqlServerMigrationFixture _fixture = fixture;

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
            await SetZone(context, Chicago, ct);
            var seed = await SeedInitiative(context, ct);
            var revenue = await AddOldKpi(context, seed.InitiativeId, "Revenue", "Annual \"run\" rate", 100, 250, "$", "M", "Increase", 1, ct);
            var churn = await AddOldKpi(context, seed.InitiativeId, "Churn", null, null, 2.5, null, "%", "Decrease", 2, ct);
            // Escapes and the numbers SQL Server and .NET could spell differently.
            var tricky = await AddOldKpi(context, seed.InitiativeId, "Net \\ promoter / é ✓", "Tab\there", 1E-05, 0.30000000000000004, "€", null, "Increase", 3, ct);
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

            moved.Select(k => k.Id).Should().Equal(revenue, churn, tricky);
            moved.Select(k => k.Order).Should().Equal(0, 1, 2);
            moved.Select(k => k.Key).Should().BeInAscendingOrder("new keys keep the order the KPIs were created in");

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
            moved[2].Details.Name.Should().Be("Net \\ promoter / é ✓");
            moved[2].Goal.Baseline.Value.Should().Be(1E-05);
            moved[2].Goal.Target.Value.Should().Be(0.30000000000000004);

            await AssertBaseline(verify, context, first, seed.InitiativeId, seed.EmployeeId, ct);
            await AssertBaseline(verify, context, moved[1], seed.InitiativeId, null, ct);
            await AssertBaseline(verify, context, moved[2], seed.InitiativeId, null, ct);
        });
    }

    public static TheoryData<string, string> Refusals => new()
    {
        { "baseline", "already at its target" },
        { "direction", "neither Increase nor Decrease" },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task Move_KpiTheOldModelCouldNotHaveHeld_StopsAndMovesNothing(string problem, string message)
    {
        await InDatabase(async (context, migrator, connectionString, ct) =>
        {
            // Arrange — written straight to the table, which only a direct edit could have done
            var seed = await SeedInitiative(context, ct);
            await AddOldKpi(context, seed.InitiativeId, "NPS", null, problem == "baseline" ? 40 : 20, 40, null, null,
                problem == "direction" ? "Sideways" : "Increase", 1, ct);

            // Act
            var act = () => migrator.MigrateAsync(Move, ct);

            // Assert
            (await act.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain(message);
            (await Scalar<int>(context, "SELECT COUNT(*) FROM [Kpis].[Kpis];", ct)).Should().Be(0);
        });
    }

    [Fact]
    public async Task Move_RepairsAPlanTheSharedModelWouldRefuse_RatherThanStopping()
    {
        await InDatabase(async (context, migrator, connectionString, ct) =>
        {
            // Arrange
            var seed = await SeedInitiative(context, ct);
            var nps = await AddOldKpi(context, seed.InitiativeId, "NPS", null, 20, 40, null, null, "Increase", 1, ct);
            // An at-risk value above the planned value of a KPI that should increase.
            await AddOldCheckpoint(context, nps, 30, "2026-06-01T12:00:00", "Jun", 35, ct);
            await AddOldCheckpoint(context, nps, 32, "2026-07-01T12:00:00", "   ", 31, ct);
            // Two checkpoints on one day: the later is kept.
            await AddOldCheckpoint(context, nps, 34, "2026-08-01T01:00:00", "Early", null, ct);
            await AddOldCheckpoint(context, nps, 35, "2026-08-01T20:00:00", "Late", null, ct);
            var crowded = await AddOldKpi(context, seed.InitiativeId, "Uptime", null, null, 99.9, null, "%", "Increase", 2, ct);
            // One past the most a plan holds: the one furthest from today goes.
            await AddOldCheckpoint(context, crowded, 99, "1990-01-01T12:00:00", "Old", null, ct);
            for (var day = 0; day < 104; day++)
                await AddOldCheckpoint(context, crowded, 99, $"2026-01-01T12:00:00", $"D{day}", null, ct, dayOffset: day);

            // Act
            await migrator.MigrateAsync(Move, ct);

            // Assert
            (await Rows(context, $"SELECT [Date], [Label], [PlannedValue], [AtRiskValue] FROM [Kpis].[Checkpoints] WHERE [KpiId] = '{nps}' ORDER BY [Date];", ct))
                .Select(r => ((DateTime)r[0], (string)r[1], (double)r[2], r[3] is DBNull ? (double?)null : (double)r[3]))
                .Should().Equal(
                    (new DateTime(2026, 6, 1), "Jun", 30d, (double?)null),
                    (new DateTime(2026, 7, 1), "2026-07-01", 32d, 31d),
                    (new DateTime(2026, 8, 1), "Late", 35d, (double?)null));
            (await Scalar<int>(context, $"SELECT COUNT(*) FROM [Kpis].[Checkpoints] WHERE [KpiId] = '{crowded}';", ct)).Should().Be(104);
            (await Scalar<int>(context, $"SELECT COUNT(*) FROM [Kpis].[Checkpoints] WHERE [KpiId] = '{crowded}' AND [Date] < '2000-01-01';", ct)).Should().Be(0);
        });
    }

    [Fact]
    public async Task Move_RunAgain_ChangesNothing_ReadingDaysEastOfUtc()
    {
        await InDatabase(async (context, migrator, connectionString, ct) =>
        {
            // Arrange
            await SetZone(context, Tokyo, ct);
            var seed = await SeedInitiative(context, ct);
            var kpi = await AddOldKpi(context, seed.InitiativeId, "Adoption", null, 10, 60, null, "%", "Increase", 1, ct);
            // 20:00 UTC on 14 March is already 15 March in Tokyo.
            await AddOldMeasurement(context, kpi, 12, "2026-03-14T20:00:00", seed.EmployeeId, null, ct);
            await AddOldCheckpoint(context, kpi, 30, "2026-06-29T16:00:00", "Q2", null, ct);
            await migrator.MigrateAsync(Move, ct);

            // Act
            await Execute(context, MoveStrategicInitiativeKpisToSharedModel.UpSql, ct);

            // Assert
            (await Rows(context, $"SELECT [MeasuredOn] FROM [Kpis].[Measurements] WHERE [KpiId] = '{kpi}';", ct))
                .Select(r => (DateTime)r[0]).Should().Equal(new DateTime(2026, 3, 15));
            (await Rows(context, $"SELECT [Date] FROM [Kpis].[Checkpoints] WHERE [KpiId] = '{kpi}';", ct))
                .Select(r => (DateTime)r[0]).Should().Equal(new DateTime(2026, 6, 30));
            (await Scalar<int>(context, "SELECT COUNT(*) FROM [Kpis].[Kpis];", ct)).Should().Be(1);
            (await Scalar<int>(context, $"SELECT COUNT(*) FROM [App].[ActivityLogs] WHERE [EventType] = 'KpiBaselinedEvent' AND [AggregateId] = '{kpi}';", ct))
                .Should().Be(1);
        });
    }

    [Fact]
    public async Task Drop_MovesAKpiAddedSinceTheMove_AndNeverBringsBackOneRemovedSinceItMoved()
    {
        await InDatabase(async (context, migrator, connectionString, ct) =>
        {
            // Arrange
            var seed = await SeedInitiative(context, ct);
            var removed = await AddOldKpi(context, seed.InitiativeId, "Uptime", null, null, 99.9, null, "%", "Increase", 1, ct);
            await migrator.MigrateAsync(Move, ct);
            await using (var remove = _fixture.CreateContext(connectionString))
            {
                var kpi = await remove.Kpis.SingleAsync(k => k.Id == removed, ct);
                kpi.Remove(EventActor.System, Now);
                remove.Kpis.Remove(kpi);
                await remove.SaveChangesAsync(ct);
            }
            var late = await AddOldKpi(context, seed.InitiativeId, "Adoption", null, 10, 60, null, "%", "Increase", 2, ct);
            // No zone was saved, so the day is read in UTC.
            await AddOldMeasurement(context, late, 12, "2026-03-15T03:30:00", seed.EmployeeId, "First read", ct);
            await AddOldCheckpoint(context, late, 30, "2026-06-30T04:00:00", "Q2", null, ct);

            // Act
            await migrator.MigrateAsync(Drop, ct);

            // Assert
            (await Rows(context, $"SELECT [Id] FROM [Kpis].[Kpis] WHERE [SubjectId] = '{seed.InitiativeId}';", ct))
                .Select(r => (Guid)r[0]).Should().Equal(late);
            (await Rows(context, $"SELECT [Value], [MeasuredOn], [Note] FROM [Kpis].[Measurements] WHERE [KpiId] = '{late}';", ct))
                .Select(r => ((double)r[0], (DateTime)r[1], (string)r[2])).Should().Equal((12d, new DateTime(2026, 3, 15), "First read"));
            (await Rows(context, $"SELECT [Date], [Label] FROM [Kpis].[Checkpoints] WHERE [KpiId] = '{late}';", ct))
                .Select(r => ((DateTime)r[0], (string)r[1])).Should().Equal((new DateTime(2026, 6, 30), "Q2"));
            (await Scalar<int>(context, $"SELECT COUNT(*) FROM [App].[ActivityLogs] WHERE [EventType] = 'KpiBaselinedEvent' AND [AggregateId] = '{late}';", ct))
                .Should().Be(1);
            (await Scalar<int>(context, "SELECT COUNT(*) FROM sys.tables WHERE [name] LIKE 'StrategicInitiativeKpi%';", ct)).Should().Be(0);
        });
    }

    [Fact]
    public async Task Down_PutsEveryInitiativeKpiBack_AndUpMovesThemAgain_KeepingOneCreationEntryEach()
    {
        await InDatabase(async (context, migrator, connectionString, ct) =>
        {
            // Arrange
            await SetZone(context, Chicago, ct);
            var seed = await SeedInitiative(context, ct);
            var revenue = await AddOldKpi(context, seed.InitiativeId, "Revenue", "Run rate", 100, 250, "$", "M", "Increase", 1, ct);
            var churn = await AddOldKpi(context, seed.InitiativeId, "Churn", null, null, 2.5, null, "%", "Decrease", 2, ct);
            await AddOldMeasurement(context, revenue, 120, "2026-03-15T03:30:00", seed.EmployeeId, "Q1 close", ct);
            await AddOldMeasurement(context, revenue, 130, "2026-04-10T15:00:00", seed.EmployeeId, null, ct);
            await AddOldCheckpoint(context, revenue, 150, "2026-06-30T04:00:00", "Q2", 140, ct);
            await migrator.MigrateAsync(cancellationToken: ct);
            var tickets = await AddSharedKpi(connectionString, seed, ct);

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
                    new object?[] { tickets, "Open tickets", null, 1200d, 400d, 800d, null, null, "Decrease", 3 },
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

            await migrator.MigrateAsync(cancellationToken: ct);
            (await Rows(context, $"SELECT [Id] FROM [Kpis].[Kpis] WHERE [SubjectId] = '{seed.InitiativeId}' ORDER BY [Order];", ct))
                .Select(r => (Guid)r[0]).Should().Equal(revenue, churn, tickets);
            (await Scalar<DateTime>(context, $"SELECT [Date] FROM [Kpis].[Checkpoints] WHERE [KpiId] = '{revenue}';", ct))
                .Should().Be(new DateTime(2026, 6, 29));
            (await Rows(context, $"SELECT [EventType] FROM [App].[ActivityLogs] WHERE [AggregateType] = 'Kpi' AND [AggregateId] = '{tickets}' AND [Category] IN ('Created', 'Baseline');", ct))
                .Select(r => (string)r[0]).Should().Equal(nameof(KpiAddedEvent));
            (await Scalar<int>(context, $"SELECT COUNT(*) FROM [App].[ActivityLogs] WHERE [EventType] = 'KpiBaselinedEvent' AND [AggregateId] = '{revenue}';", ct))
                .Should().Be(1);
        });
    }

    public static TheoryData<string, string> RollbackRefusals => new()
    {
        { "relative", "change from the baseline" },
        { "no-recorder", "records no one who took it" },
        { "long-name", "longer than the old model holds" },
        { "same-moment", "fall at one moment" },
    };

    [Theory]
    [MemberData(nameof(RollbackRefusals))]
    public async Task Down_KpiTheOldModelCannotHold_StopsTheRollbackAndKeepsTheSharedKpis(string problem, string message)
    {
        await InDatabase(async (context, migrator, connectionString, ct) =>
        {
            // Arrange
            var seed = await SeedInitiative(context, ct);
            var kpi = await AddOldKpi(context, seed.InitiativeId, "Cost", null, 500, 300, "$", "k", "Decrease", 1, ct);
            await AddOldMeasurement(context, kpi, 450, "2026-02-01T12:00:00", seed.EmployeeId, null, ct);
            await migrator.MigrateAsync(cancellationToken: ct);
            // What only the shared model can hold, written as a change through it could leave it.
            await Execute(context, problem switch
            {
                "relative" => $"UPDATE [Kpis].[Kpis] SET [TargetKind] = 'RelativeChange', [TargetValue] = 20 WHERE [Id] = '{kpi}';",
                "no-recorder" => $"UPDATE [Kpis].[Measurements] SET [MeasuredById] = NULL WHERE [KpiId] = '{kpi}';",
                "long-name" => $"UPDATE [Kpis].[Kpis] SET [Name] = N'{new string('x', 70)}' WHERE [Id] = '{kpi}';",
                _ => $"""
                    INSERT INTO [Kpis].[Measurements] ([Id], [KpiId], [Value], [PeriodStart], [PeriodEnd], [MeasuredAt], [MeasuredOn], [MeasuredById], [Source], [SystemCreated], [SystemLastModified])
                    VALUES (NEWID(), '{kpi}', 440, '2026-01-01', '2026-01-31', NULL, '2026-03-01', '{seed.EmployeeId}', 'Manual', SYSUTCDATETIME(), SYSUTCDATETIME()),
                           (NEWID(), '{kpi}', 430, '2026-02-01', '2026-02-28', NULL, '2026-03-01', '{seed.EmployeeId}', 'Manual', SYSUTCDATETIME(), SYSUTCDATETIME());
                    """,
            }, ct);

            // Act
            var act = () => migrator.MigrateAsync(Previous, ct);

            // Assert
            (await act.Should().ThrowAsync<SqlException>())
                .Which.Message.Should().Contain("cannot be rolled back").And.Contain(message);
            (await Scalar<int>(context, $"SELECT COUNT(*) FROM [Kpis].[Kpis] WHERE [Id] = '{kpi}';", ct)).Should().Be(1);
        });
    }

    private sealed record Seeded(Guid InitiativeId, Guid EmployeeId);

    /// <summary>Runs <paramref name="test"/> against the database migrated as far as <see cref="Previous"/>, as it was before any test wrote to it.</summary>
    private async Task InDatabase(Func<WaydDbContext, IMigrator, string, CancellationToken, Task> test)
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await _fixture.MigratedTo(Previous, ct);

        await using var context = _fixture.CreateContext(connectionString);
        await test(context, context.GetService<IMigrator>(), connectionString, ct);
    }

    /// <summary>Asserts the KPI's baseline entry is the one the factory builds for the same event, filed under the initiative too.</summary>
    private static async Task AssertBaseline(WaydDbContext verify, WaydDbContext context, Kpi kpi, Guid initiativeId, Guid? createdById, CancellationToken ct)
    {
        var entry = await verify.ActivityLogs.AsNoTracking()
            .Include(a => a.RelatedAggregates)
            .SingleAsync(a => a.AggregateType == "Kpi" && a.AggregateId == kpi.Id, ct);
        var createdOn = Instant.FromDateTimeUtc(DateTime.SpecifyKind(await Scalar<DateTime>(context,
            $"SELECT [SystemCreated] FROM [Ppm].[StrategicInitiativeKpis] WHERE [Id] = '{kpi.Id}';", ct), DateTimeKind.Utc));
        var expectedEvent = new KpiBaselinedEvent(kpi.Id, kpi.Key, kpi.SubjectType, kpi.SubjectId, kpi.Details, kpi.Goal,
            kpi.Source, kpi.Order, createdOn, createdById, entry.Timestamp, kpi.Window);
        var expected = ActivityLogEntryFactory.CreateActivityLogEntry(expectedEvent, kpi, 0, null);

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
            .Should().ContainSingle().Which.Should().Be(("StrategicInitiative", initiativeId));
    }

    /// <summary>A KPI added through the shared model after the move, with one point-in-time measurement.</summary>
    private async Task<Guid> AddSharedKpi(string connectionString, Seeded seed, CancellationToken ct)
    {
        var goal = new KpiGoal(KpiTargetDirection.Decrease, new KpiBaseline(1200, null), KpiTarget.Absolute(400), null);
        var definition = new KpiDefinition("Open tickets", null, null, null, null, KpiExpressedAs.Value, goal, KpiMeasurementWindow.PointInTime);
        var kpi = Kpi.Create(PpmKpiSubjects.StrategicInitiative.Key, seed.InitiativeId, definition, 2, EventActor.System, Now).Value;
        var at = Instant.FromUtc(2026, 5, 1, 12, 0);
        kpi.AddMeasurement(
            new KpiMeasurementInput(800, KpiMeasurementTime.AtMoment(at), null, null, new LocalDate(2026, 5, 1), seed.EmployeeId, KpiMeasurementSource.Manual, null, null),
            DateTimeZoneProviders.Tzdb[Chicago], EventActor.System, Now).IsSuccess.Should().BeTrue();

        await using var add = _fixture.CreateContext(connectionString);
        add.Kpis.Add(kpi);
        await add.SaveChangesAsync(ct);
        return kpi.Id;
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

    private static Task AddOldCheckpoint(WaydDbContext context, Guid kpiId, double target, string at, string label, double? atRisk, CancellationToken ct, int dayOffset = 0) =>
        Execute(context, $"""
            INSERT INTO [Ppm].[StrategicInitiativeKpiCheckpoints] ([Id], [KpiId], [TargetValue], [AtRiskValue], [CheckpointDate], [DateLabel], [SystemCreated], [SystemLastModified])
            VALUES ('{Guid.CreateVersion7()}', '{kpiId}', {Number(target)}, {Number(atRisk)}, DATEADD(day, {dayOffset}, CAST('{at}' AS datetime2)), {Text(label)}, SYSUTCDATETIME(), SYSUTCDATETIME());
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
