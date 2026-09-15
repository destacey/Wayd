using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;
using Wayd.Common.Domain.Events.Planning.PlanningIntervals;
using Wayd.Common.Domain.Events.Planning.Risks;
using Wayd.Common.Domain.Models;
using Wayd.Common.Models;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Proves planning intervals, their objectives and risks record their events against themselves, carrying the key
/// SQL Server assigned, when they are changed before their first save.
/// </summary>
/// <remarks>
/// Every event raised before the first save waits for the key in a post-persistence action. The objective is reached
/// through the interval's collection, so only a real save shows that its own actions run and in the order they were
/// queued.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class PlanningActivityTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task SaveChanges_IntervalAndObjectiveChangedBeforeTheirFirstSave_RecordsEachAgainstItself()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = SqlServerDbContextFixture.FixedNow;

        await using var context = _fixture.CreateContext();
        var team = await PlanningSeed.Team(context, ct);

        var interval = PlanningInterval.Create($"Atlas PI {Guid.NewGuid():N}"[..20], null,
            new LocalDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 3, 1)), 4, "Iteration ", EventActor.System, now).Value;
        interval.ManageTeams([team.Id], EventActor.System, now);
        var objective = interval.CreateObjective(team, "Ship the thing", null, false, null, null, 1, EventActor.System, now).Value;
        interval.UpdateObjective(objective.Id, objective.Name, objective.Description, ObjectiveStatus.InProgress, 25,
            null, null, false, EventActor.System, now);
        interval.Update(interval.Name, interval.Description, objectivesLocked: true, EventActor.System, now);
        context.PlanningIntervals.Add(interval);

        // Act
        await context.SaveChangesAsync(ct);

        // Assert
        interval.Key.Should().BeGreaterThan(0);
        objective.Key.Should().BeGreaterThan(0);

        await using var verify = _fixture.CreateContext();

        var intervalEntries = await Entries(verify, interval.Id, ct);
        intervalEntries.Select(e => e.EventType).Should().Equal(
            nameof(PlanningIntervalCreatedEvent),
            nameof(PlanningIntervalTeamsChangedEvent),
            nameof(PlanningIntervalObjectivesLockedEvent));
        intervalEntries.Should().OnlyContain(e => e.AggregateType == "PlanningInterval" && KeyOf(e.Payload) == interval.Key);

        var objectiveEntries = await Entries(verify, objective.Id, ct);
        objectiveEntries.Select(e => e.EventType).Should().Equal(
            nameof(PlanningIntervalObjectiveCreatedEvent),
            nameof(PlanningIntervalObjectiveStatusChangedEvent),
            nameof(PlanningIntervalObjectiveProgressChangedEvent));
        objectiveEntries.Should().OnlyContain(e => e.AggregateType == "PlanningIntervalObjective" && KeyOf(e.Payload) == objective.Key);
    }

    [Fact]
    public async Task SaveChanges_RiskClosedBeforeItsFirstSave_RecordsTheCreationThenTheClosure()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = SqlServerDbContextFixture.FixedNow;

        await using var context = _fixture.CreateContext();
        var team = await PlanningSeed.Team(context, ct);
        var reporter = Employee.Create(new PersonName("Ada", null, "Lovelace"), "E" + Guid.NewGuid().ToString("N")[..11], now,
            new EmailAddress($"ada.{Guid.NewGuid():N}@acme.example"), null, null, null, null, true, null, now);
        context.Employees.Add(reporter);
        await context.SaveChangesAsync(ct);

        var risk = Risk.Create("Vendor slip", null, team.Id, now, reporter.Id, RiskCategory.Owned, RiskGrade.High, RiskGrade.Low,
            null, null, null, EventActor.System, now);
        risk.Update(risk.Summary, risk.Description, RiskStatus.Closed, RiskCategory.Resolved, risk.Impact, risk.Likelihood,
            null, null, null, EventActor.System, now);
        context.Risks.Add(risk);

        // Act
        await context.SaveChangesAsync(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var entries = await Entries(verify, risk.Id, ct);

        entries.Select(e => e.EventType).Should().Equal(
            nameof(RiskCreatedEvent),
            nameof(RiskCategoryChangedEvent),
            nameof(RiskClosedEvent));
        entries.Should().OnlyContain(e => e.AggregateType == "Risk" && KeyOf(e.Payload) == risk.Key);

        var created = JsonDocument.Parse(entries[0].Payload).RootElement;
        created.GetProperty("status").GetString().Should().Be("open");
        created.GetProperty("category").GetString().Should().Be("owned");
    }

    private static Task<List<Wayd.Common.Domain.Activities.ActivityLogEntry>> Entries(
        Wayd.Infrastructure.Persistence.Context.WaydDbContext context, Guid aggregateId, CancellationToken ct) =>
        context.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == aggregateId)
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Ordinal)
            .ToListAsync(ct);

    private static int KeyOf(string payload) => JsonDocument.Parse(payload).RootElement.GetProperty("key").GetInt32();
}
