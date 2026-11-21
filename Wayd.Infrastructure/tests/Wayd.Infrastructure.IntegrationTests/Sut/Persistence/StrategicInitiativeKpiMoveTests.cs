using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Kpis;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Kpis;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Settings;
using Wayd.Common.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.IntegrationTests.Sut.Notifications;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Infrastructure.Persistence.Activities;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Move-Strategic-Initiative-Kpis-To-Shared-Model migration carries each strategic initiative KPI, with its
/// checkpoints and measurements, into the shared KPI tables as the shared model would hold it, and writes the baseline
/// <see cref="ActivityLogEntryFactory"/> builds for the same event.
/// </summary>
/// <remarks>
/// The copy and the payloads are hand-built T-SQL, so only a real SQL Server shows they read back the way EF and the
/// serializer would have written them. Each test sets the system time zone, a single row the collection shares, and
/// removes it again.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class StrategicInitiativeKpiMoveTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 20, 12, 0);
    private static readonly Guid GeneralId = SystemSettingsSection.IdFor<GeneralSettings>();

    private readonly SqlServerDbContextFixture _fixture = fixture;

    static StrategicInitiativeKpiMoveTests()
    {
        PpmKpiSubjects.Register();
    }

    [Fact]
    public async Task Move_CopiesTheKpiAsTheSharedModelHoldsIt_ReadingDaysInTheSystemZone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await UseZone("America/Chicago", ct);
        try
        {
            var (initiative, employeeId) = await SeedInitiative(ct);
            var revenue = AddKpi(initiative, new("Revenue", "Annual \"run\" rate", 100, 250, "$", "M", KpiTargetDirection.Increase));
            var churn = AddKpi(initiative, new("Churn", null, null, 2.5, null, "%", KpiTargetDirection.Decrease));
            // 03:30 UTC on 15 March is still 14 March in Chicago.
            var lateNight = Instant.FromUtc(2026, 3, 15, 3, 30);
            var sameDay = Instant.FromUtc(2026, 3, 14, 15, 0);
            AddMeasurement(initiative, revenue, 120, lateNight, employeeId, "Q1 close");
            AddMeasurement(initiative, revenue, 130, sameDay, employeeId, null);
            initiative.ManageKpiCheckpointPlan(revenue.Id,
            [
                UpsertStrategicInitiativeKpiCheckpoint.Create(null, 150, Instant.FromUtc(2026, 6, 30, 4, 0), " Q2 ", 140),
                UpsertStrategicInitiativeKpiCheckpoint.Create(null, 250, Instant.FromUtc(2026, 12, 31, 18, 0), "Q4"),
            ], EventActor.System, Now).IsSuccess.Should().BeTrue();
            await Save(initiative, ct);

            // Act
            await RunMove(ct);

            // Assert
            await using var verify = _fixture.CreateContext();
            var moved = await verify.Kpis.AsNoTracking()
                .Include(k => k.Measurements)
                .Include(k => k.Checkpoints)
                .Where(k => k.SubjectId == initiative.Id)
                .OrderBy(k => k.Order)
                .ToListAsync(ct);

            moved.Select(k => k.Id).Should().Equal(revenue.Id, churn.Id);
            moved.Select(k => k.Order).Should().Equal(0, 1);
            moved[0].Key.Should().BeLessThan(moved[1].Key, "new keys keep the order the KPIs were created in");

            var first = moved[0];
            first.SubjectType.Should().Be(PpmKpiSubjects.StrategicInitiative.Key);
            first.Details.Should().Be(new KpiDetails("Revenue", "Annual \"run\" rate", null, "$", "M", KpiExpressedAs.ProgressToTarget, null));
            first.Goal.Should().Be(new KpiGoal(KpiTargetDirection.Increase, new KpiBaseline(100, null), KpiTarget.Absolute(250), null));
            first.Source.Should().Be(KpiSource.Manual);
            first.Status.Should().Be(KpiStatus.Active);
            first.Window.Should().Be(KpiMeasurementWindow.PointInTime);

            first.Measurements.OrderBy(m => m.MeasuredAt).Select(m => (m.Value, m.MeasuredAt, m.PeriodStart, m.PeriodEnd, m.MeasuredOn, m.MeasuredById, m.Note, m.Source))
                .Should().Equal(
                    (130d, (Instant?)sameDay, new LocalDate(2026, 3, 14), new LocalDate(2026, 3, 14), new LocalDate(2026, 3, 14), (Guid?)employeeId, (string?)null, KpiMeasurementSource.Manual),
                    (120d, (Instant?)lateNight, new LocalDate(2026, 3, 14), new LocalDate(2026, 3, 14), new LocalDate(2026, 3, 14), (Guid?)employeeId, (string?)"Q1 close", KpiMeasurementSource.Manual));

            first.CheckpointPlan.Should().Equal(
                new KpiCheckpointValues(new LocalDate(2026, 6, 29), "Q2", 150, 140),
                new KpiCheckpointValues(new LocalDate(2026, 12, 31), "Q4", 250, null));

            var second = moved[1];
            second.Details.ExpressedAs.Should().Be(KpiExpressedAs.Value, "a KPI with no starting value has no progress to read");
            second.Goal.Should().Be(new KpiGoal(KpiTargetDirection.Decrease, KpiBaseline.None, KpiTarget.Absolute(2.5), null));
        }
        finally
        {
            await UseZone(null, ct);
        }
    }

    [Fact]
    public async Task Move_WritesTheBaselineTheFactoryWould_FiledUnderTheInitiative()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await UseZone(null, ct);
        var (initiative, employeeId) = await SeedInitiative(ct);
        var kpi = AddKpi(initiative, new("Adoption", null, 0.1, 0.35, null, "%", KpiTargetDirection.Increase), EventActor.User(SystemUser.Id, employeeId));
        await Save(initiative, ct);

        // Act
        await RunMove(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var moved = await verify.Kpis.AsNoTracking().SingleAsync(k => k.Id == kpi.Id, ct);
        var createdOn = await verify.StrategicInitiatives.AsNoTracking()
            .SelectMany(i => i.Kpis).Where(k => k.Id == kpi.Id).Select(k => EF.Property<Instant>(k, "SystemCreated")).SingleAsync(ct);
        var entry = await verify.ActivityLogs.AsNoTracking()
            .Include(a => a.RelatedAggregates)
            .SingleAsync(a => a.AggregateType == "Kpi" && a.AggregateId == kpi.Id, ct);

        var expectedEvent = new KpiBaselinedEvent(moved.Id, moved.Key, moved.SubjectType, moved.SubjectId, moved.Details, moved.Goal,
            moved.Source, moved.Order, createdOn, employeeId, entry.Timestamp, moved.Window);
        var expected = ActivityLogEntryFactory.CreateActivityLogEntry(expectedEvent, moved, 0, null);

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
            .Should().ContainSingle().Which.Should().Be(("StrategicInitiative", initiative.Id));
    }

    [Fact]
    public async Task Move_RunTwice_CopiesAndBaselinesOnce()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await UseZone(null, ct);
        var (initiative, employeeId) = await SeedInitiative(ct);
        var kpi = AddKpi(initiative, new("Uptime", null, null, 99.9, null, "%", KpiTargetDirection.Increase));
        AddMeasurement(initiative, kpi, 99.5, Instant.FromUtc(2026, 2, 1, 0, 0), employeeId, null);
        await Save(initiative, ct);
        await RunMove(ct);

        // Act
        await RunMove(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.Kpis.CountAsync(k => k.Id == kpi.Id, ct)).Should().Be(1);
        (await verify.Set<KpiMeasurement>().CountAsync(m => m.KpiId == kpi.Id, ct)).Should().Be(1);
        (await verify.ActivityLogs.CountAsync(a => a.AggregateType == "Kpi" && a.AggregateId == kpi.Id, ct)).Should().Be(1);
    }

    [Fact]
    public async Task Move_KpiTheSharedModelWouldRefuse_StopsAndMovesNothing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await UseZone(null, ct);
        var (initiative, _) = await SeedInitiative(ct);
        var kpi = AddKpi(initiative, new("NPS", null, 20, 40, null, null, KpiTargetDirection.Increase));
        // The old plan accepted an at-risk value above the planned value of a KPI that should increase.
        initiative.ManageKpiCheckpointPlan(kpi.Id,
            [UpsertStrategicInitiativeKpiCheckpoint.Create(null, 30, Instant.FromUtc(2026, 6, 1, 12, 0), "Jun", 35)],
            EventActor.System, Now).IsSuccess.Should().BeTrue();
        await Save(initiative, ct);

        try
        {
            // Act
            var act = () => RunMove(ct);

            // Assert
            (await act.Should().ThrowAsync<SqlException>())
                .Which.Message.Should().Contain("not short of its planned value").And.Contain($"initiative {initiative.Key}");
            await using var verify = _fixture.CreateContext();
            (await verify.Kpis.AnyAsync(k => k.SubjectId == initiative.Id, ct)).Should().BeFalse();
        }
        finally
        {
            // A KPI left unmovable would stop every later run in the collection.
            await using var cleanup = _fixture.CreateContext();
            await cleanup.StrategicInitiatives.Where(i => i.Id == initiative.Id).ExecuteDeleteAsync(ct);
        }
    }

    private static StrategicInitiativeKpi AddKpi(StrategicInitiative initiative, StrategicInitiativeKpiUpsertParameters parameters, EventActor? actor = null)
    {
        var kpi = initiative.CreateKpi(parameters, actor ?? EventActor.System, Now);
        kpi.IsSuccess.Should().BeTrue(kpi.IsFailure ? kpi.Error : null);
        return kpi.Value;
    }

    private static void AddMeasurement(StrategicInitiative initiative, StrategicInitiativeKpi kpi, double value, Instant at, Guid employeeId, string? note)
    {
        var measurement = StrategicInitiativeKpiMeasurement.Create(kpi.Id, value, at, employeeId, note, Now).Value;
        initiative.AddKpiMeasurement(kpi.Id, measurement, EventActor.System, Now).IsSuccess.Should().BeTrue();
    }

    /// <summary>An active initiative, in an active portfolio, saved; and an employee to record measurements.</summary>
    private async Task<(StrategicInitiative Initiative, Guid EmployeeId)> SeedInitiative(CancellationToken ct)
    {
        var employee = await NotificationTestData.SeedEmployee(_fixture);

        var portfolio = ProjectPortfolio.Create($"Portfolio {Guid.NewGuid():N}"[..16], "KPI move portfolio",
            new() { [ProjectPortfolioRole.Owner] = [employee.Id] }, EventActor.System, Now);
        portfolio.Activate(PpmActor.System, new LocalDate(2026, 1, 1), Now).IsSuccess.Should().BeTrue();

        var initiative = portfolio.CreateStrategicInitiative($"Initiative {Guid.NewGuid():N}"[..18], "KPI move initiative",
            new LocalDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 12, 31)), null, EventActor.System, Now).Value;

        await using var context = _fixture.CreateContext();
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync(ct);

        return (initiative, employee.Id);
    }

    /// <summary>Saves the KPIs added to <paramref name="initiative"/> since it was seeded.</summary>
    private async Task Save(StrategicInitiative initiative, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        context.Attach(initiative);
        foreach (var kpi in initiative.Kpis)
        {
            context.Entry(kpi).State = EntityState.Added;
            foreach (var checkpoint in kpi.Checkpoints)
                context.Entry(checkpoint).State = EntityState.Added;
            foreach (var measurement in kpi.Measurements)
                context.Entry(measurement).State = EntityState.Added;
        }

        await context.SaveChangesAsync(ct);
    }

    /// <summary>Sets the system time zone, or removes the general settings so it reads as UTC.</summary>
    private async Task UseZone(string? zone, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.SystemSettingsSections.Where(s => s.Id == GeneralId).ExecuteDeleteAsync(ct);
        if (zone is null)
            return;

        context.SystemSettingsSections.Add(SystemSettingsSection.Create(new GeneralSettings { TimeZone = zone }, EventActor.System, Now)!);
        await context.SaveChangesAsync(ct);
    }

    private async Task RunMove(CancellationToken ct)
    {
        // Straight through ADO: the payload's braces would be read as format placeholders by ExecuteSqlRaw.
        await using var context = _fixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = MoveStrategicInitiativeKpisToSharedModel.UpSql;
        await command.ExecuteNonQueryAsync(ct);
    }
}
