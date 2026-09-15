using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;
using Wayd.Common.Domain.Events.Planning.PlanningIntervals;
using Wayd.Common.Domain.Events.Planning.Risks;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Organization.Domain.Enums;
using Wayd.Organization.Domain.Models;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Models.Iterations;
using static Wayd.Infrastructure.IntegrationTests.Sut.Persistence.BaselineActivityAssertions;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Runs the Backfill-Planning-Baseline-Activity migration over planning intervals, objectives and risks that
/// predate their creation events.
/// </summary>
/// <remarks>
/// The migration writes every payload by hand in T-SQL, so the only proof that a baseline reads back exactly as the
/// serializer would have written it is a real SQL Server, real rows, and a comparison against the entry
/// <see cref="Wayd.Infrastructure.Persistence.Activities.ActivityLogEntryFactory"/> builds for the same event.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class BackfillPlanningBaselineActivityMigrationTests(SqlServerDbContextFixture fixture)
{
    private const string MigrationBefore = "20260913230000_Backfill-Baseline-Activity";

    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private sealed record SeededRecords(
        Guid PlanningIntervalId,
        Guid ObjectiveId,
        Guid RiskId,
        Guid DeletedRiskId,
        Guid CreatorEmployeeId,
        Guid StrayRiskEntryId)
    {
        public Guid[] All => [PlanningIntervalId, ObjectiveId, RiskId, DeletedRiskId];
    }

    [Fact]
    public async Task Up_BaselinesEveryPlanningAggregate_ExactlyAsTheFactoryWouldRecordIt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await MigrateToBefore(ct);
            var seeded = await SeedWithoutHistory(ct);

            // Act
            await using (var context = _fixture.CreateContext())
            {
                await context.Database.MigrateAsync(ct);
            }

            // Assert
            await using var verify = _fixture.CreateContext();
            var creator = seeded.CreatorEmployeeId;

            var intervalRow = await SingleRow(verify, seeded.PlanningIntervalId, ct);
            var interval = await verify.PlanningIntervals.AsNoTracking()
                .Include(p => p.Iterations)
                .Include(p => p.Teams)
                .Include(p => p.IterationSprints)
                .SingleAsync(p => p.Id == seeded.PlanningIntervalId, ct);
            interval.ObjectivesLocked.Should().BeTrue();
            interval.Teams.Should().NotBeEmpty();
            interval.IterationSprints.Should().NotBeEmpty();
            AssertBaseline(intervalRow, new PlanningIntervalBaselinedEvent(interval.Id, interval.Key, interval.Name,
                interval.Description, interval.DateRange, interval.ObjectivesLocked,
                [.. interval.Iterations.Select(i => new PlanningIntervalIterationValues(i.Id, i.Name, i.Category, i.DateRange))],
                [.. interval.Teams.Select(t => t.TeamId)],
                [.. interval.IterationSprints.Select(s => new PlanningIntervalSprintMapping(s.PlanningIntervalIterationId, s.SprintId))],
                await SystemCreated(verify.PlanningIntervals, seeded.PlanningIntervalId, ct), creator, intervalRow.Timestamp));

            var objectiveRow = await SingleRow(verify, seeded.ObjectiveId, ct);
            var objective = await verify.PlanningIntervalObjectives.AsNoTracking().SingleAsync(o => o.Id == seeded.ObjectiveId, ct);
            objective.ClosedDate.Should().NotBeNull();
            AssertBaseline(objectiveRow, new PlanningIntervalObjectiveBaselinedEvent(objective.Id, objective.Key,
                objective.PlanningIntervalId, objective.TeamId, objective.Name, objective.Description, objective.Type,
                objective.Status, objective.Progress, objective.IsStretch, objective.StartDate, objective.TargetDate,
                objective.ClosedDate, objective.Order,
                await SystemCreated(verify.PlanningIntervalObjectives, seeded.ObjectiveId, ct), creator, objectiveRow.Timestamp));

            var riskRow = await SingleRow(verify, seeded.RiskId, ct);
            riskRow.Id.Should().NotBe(seeded.StrayRiskEntryId, "the older entry the record already had is cleared");
            var risk = await verify.Risks.AsNoTracking().SingleAsync(r => r.Id == seeded.RiskId, ct);
            risk.ClosedDate.Should().NotBeNull();
            AssertBaseline(riskRow, new RiskBaselinedEvent(risk.Id, risk.Key, risk.Summary, risk.Description, risk.TeamId,
                risk.ReportedOn, risk.ReportedById, risk.Status, risk.Category, risk.Impact, risk.Likelihood, risk.AssigneeId,
                risk.FollowUpDate, risk.Response, risk.ClosedDate,
                await SystemCreated(verify.Risks, seeded.RiskId, ct), creator, riskRow.Timestamp));

            (await verify.ActivityLogs.AsNoTracking().CountAsync(a => a.AggregateId == seeded.DeletedRiskId, ct))
                .Should().Be(0, "a soft-deleted risk is not baselined");
        }
        finally
        {
            await RestoreLatest(ct);
        }
    }

    [Fact]
    public async Task Up_RunAgain_WritesNothing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await MigrateToBefore(ct);
            var seeded = await SeedWithoutHistory(ct);

            await using (var context = _fixture.CreateContext())
            {
                await context.Database.MigrateAsync(ct);
            }

            var before = await Snapshot(seeded.All, ct);
            before.Should().HaveCount(3);

            // Act
            // Sent as a plain command, as the migration sends it: ExecuteSqlRaw runs the text through
            // string.Format even with no parameters, and the payload templates are full of braces.
            await using (var context = _fixture.CreateContext())
            {
                await context.Database.OpenConnectionAsync(ct);
                await using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = BackfillPlanningBaselineActivity.UpSql;
                await command.ExecuteNonQueryAsync(ct);
            }

            // Assert
            var after = await Snapshot(seeded.All, ct);
            after.Should().BeEquivalentTo(before);
        }
        finally
        {
            await RestoreLatest(ct);
        }
    }

    [Fact]
    public async Task Up_LeavesARecordThatHasACreationEntry()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await MigrateToBefore(ct);

            Guid riskId;
            await using (var context = _fixture.CreateContext())
            {
                var (team, reporter) = await SeedTeamAndReporter(context, ct);
                var risk = Risk.Create("Atlas kept risk", null, team.Id, Now, reporter.Id, RiskCategory.Owned,
                    RiskGrade.Low, RiskGrade.Low, null, null, null, EventActor.System, Now);
                context.Risks.Add(risk);
                await context.SaveChangesAsync(ct);
                riskId = risk.Id;
            }

            var before = await Snapshot([riskId], ct);
            before.Should().ContainSingle().Which.EventType.Should().Be(nameof(RiskCreatedEvent));

            // Act
            await using (var context = _fixture.CreateContext())
            {
                await context.Database.MigrateAsync(ct);
            }

            // Assert
            var after = await Snapshot([riskId], ct);
            after.Should().BeEquivalentTo(before);
        }
        finally
        {
            await RestoreLatest(ct);
        }
    }

    [Fact]
    public async Task Down_RemovesTheBaselines()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        try
        {
            await MigrateToBefore(ct);
            var seeded = await SeedWithoutHistory(ct);

            await using (var context = _fixture.CreateContext())
            {
                await context.Database.MigrateAsync(ct);
            }

            var baselined = await Snapshot(seeded.All, ct);
            baselined.Should().HaveCount(3).And.OnlyContain(r => r.Category == ActivityCategory.Baseline);

            // Act
            await MigrateToBefore(ct);

            // Assert
            var after = await Snapshot(seeded.All, ct);
            after.Should().BeEmpty();
        }
        finally
        {
            await RestoreLatest(ct);
        }
    }

    private async Task MigrateToBefore(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(MigrationBefore, ct);
    }

    private async Task RestoreLatest(CancellationToken ct)
    {
        // Leave the shared database at the latest migration for the tests that follow.
        await using var restore = _fixture.CreateContext();
        await restore.Database.MigrateAsync(ct);
    }

    /// <summary>
    /// Creates a planning interval with teams, a mapped sprint and locked objectives, a completed objective, a closed
    /// risk and a deleted one through the domain, then removes the entries saving them wrote, as for records that
    /// existed before their events did. The risk keeps one older entry of another kind, which the migration has to
    /// clear.
    /// </summary>
    private async Task<SeededRecords> SeedWithoutHistory(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();

        var (team, reporter) = await SeedTeamAndReporter(context, ct);
        var assignee = NewEmployee("Grace", "Hopper");
        context.Employees.Add(assignee);
        await context.SaveChangesAsync(ct);

        await LinkFixtureUserTo(context, reporter.Id, ct);

        var suffix = UniqueCode(8);
        var dates = new LocalDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 3, 29));

        var interval = Succeeded(PlanningInterval.Create($"Atlas PI {suffix}", "The \"Atlas\" train\\", dates, 4, "Atlas ", EventActor.System, Now));
        Succeeded(interval.ManageTeams([team.Id], EventActor.System, Now));
        context.PlanningIntervals.Add(interval);

        var sprint = Iteration.Create($"Atlas Sprint {suffix}", IterationType.Sprint, IterationState.Active,
            new IterationDateRange(Instant.FromUtc(2026, 1, 5, 0, 0), Instant.FromUtc(2026, 2, 1, 0, 0)),
            team.Id, OwnershipInfo.CreateWaydOwned(), [], EventActor.System, Now);
        context.Iterations.Add(sprint);
        await context.SaveChangesAsync(ct);

        Succeeded(interval.MapSprintToIteration(interval.Iterations.First().Id, sprint, EventActor.System, Now));

        var objective = Succeeded(interval.CreateObjective(team, $"Atlas objective {suffix}", "Ship \"it\"", isStretch: true,
            new LocalDate(2026, 1, 12), new LocalDate(2026, 3, 13), order: 2, EventActor.System, Now));
        await context.SaveChangesAsync(ct);

        Succeeded(interval.UpdateObjective(objective.Id, objective.Name, objective.Description, ObjectiveStatus.Completed,
            62.5, objective.StartDate, objective.TargetDate, objective.IsStretch, EventActor.System, Now.Plus(Duration.FromDays(40))));
        Succeeded(interval.Update(interval.Name, interval.Description, objectivesLocked: true, EventActor.System, Now));

        var risk = Risk.Create($"Atlas risk {suffix}", null, team.Id, Now, reporter.Id, RiskCategory.Owned,
            RiskGrade.High, RiskGrade.Medium, assignee.Id, new LocalDate(2026, 2, 2), "Escalated", EventActor.System, Now);
        var deletedRisk = Risk.Create($"Atlas deleted risk {suffix}", null, team.Id, Now, reporter.Id, RiskCategory.Accepted,
            RiskGrade.Low, RiskGrade.Low, null, null, null, EventActor.System, Now);
        context.Risks.AddRange(risk, deletedRisk);
        await context.SaveChangesAsync(ct);

        Succeeded(risk.Update(risk.Summary, risk.Description, RiskStatus.Closed, RiskCategory.Mitigated, risk.Impact,
            risk.Likelihood, risk.AssigneeId, risk.FollowUpDate, risk.Response, EventActor.System, Now.Plus(Duration.FromMilliseconds(1250))));
        context.Risks.Remove(deletedRisk);
        await context.SaveChangesAsync(ct);

        var seeded = new SeededRecords(interval.Id, objective.Id, risk.Id, deletedRisk.Id, reporter.Id, Guid.CreateVersion7());

        var ids = seeded.All;
        await context.ActivityLogs.Where(a => ids.Contains(a.AggregateId)).ExecuteDeleteAsync(ct);

        context.ActivityLogs.Add(new ActivityLogEntry(
            seeded.StrayRiskEntryId, nameof(RiskAssigneeChangedEvent), ActivityCategory.Updated, "Planning", "Risk",
            seeded.RiskId, EventActor.System, Instant.FromUtc(2025, 6, 1, 12, 0, 0), ordinal: 0, correlationId: null,
            "{}", "Risk Assignee Changed"));
        await context.SaveChangesAsync(ct);

        return seeded;
    }

    private static async Task<(PlanningTeam Team, Employee Reporter)> SeedTeamAndReporter(
        Wayd.Infrastructure.Persistence.Context.WaydDbContext context, CancellationToken ct)
    {
        var reporter = NewEmployee("Ada", "Lovelace");
        context.Employees.Add(reporter);

        var team = Team.Create($"Atlas Team {UniqueCode(8)}", new TeamCode("P" + UniqueCode(9)[1..]), null, new LocalDate(2024, 1, 2),
            Methodology.Scrum, SizingMethod.StoryPoints, EventActor.System, Now);
        context.Teams.Add(team);
        await context.SaveChangesAsync(ct);

        var planningTeam = new PlanningTeam(team, Now);
        context.PlanningTeams.Add(planningTeam);
        await context.SaveChangesAsync(ct);

        return (planningTeam, reporter);
    }

    private static Employee NewEmployee(string firstName, string lastName) =>
        Employee.Create(
            new PersonName(firstName, null, lastName),
            "E" + UniqueCode(11),
            Now,
            new EmailAddress($"{firstName.ToLowerInvariant()}.{UniqueCode(8).ToLowerInvariant()}@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            Now);

    private static string UniqueCode(int length) => Guid.NewGuid().ToString("N").ToUpperInvariant()[..length];

    private static void Succeeded(CSharpFunctionalExtensions.Result result) =>
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

    private static T Succeeded<T>(CSharpFunctionalExtensions.Result<T> result)
    {
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        return result.Value;
    }

    private sealed record RowSnapshot(Guid Id, Guid AggregateId, string EventType, ActivityCategory Category, string Payload, Instant Timestamp);

    private async Task<List<RowSnapshot>> Snapshot(Guid[] aggregateIds, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        return await context.ActivityLogs.AsNoTracking()
            .Where(a => aggregateIds.Contains(a.AggregateId))
            .Select(a => new RowSnapshot(a.Id, a.AggregateId, a.EventType, a.Category, a.Payload, a.Timestamp))
            .ToListAsync(ct);
    }
}
