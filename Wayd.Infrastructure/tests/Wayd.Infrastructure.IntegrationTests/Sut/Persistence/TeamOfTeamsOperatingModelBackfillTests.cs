using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Infrastructure.Persistence.Activities;
using Wayd.Organization.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the activity entry the Add-TeamOfTeamsOperatingModels migration writes for a backfilled model is the one
/// a live <see cref="TeamOfTeamsOperatingModelSetEvent"/> would have written.
/// </summary>
/// <remarks>
/// The payload is hand-built T-SQL, so only a real SQL Server shows it reads back the way the serializer writes it.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class TeamOfTeamsOperatingModelBackfillTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate ActiveDate = new(2024, 3, 1);
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    /// <summary>
    /// Seeds a team of teams whose model has no Set entry, as every team of teams had when the migration ran.
    /// </summary>
    private async Task<TeamOfTeams> SeedTeamOfTeamsWithAnUnrecordedModel(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var code = new TeamCode($"B{Random.Shared.Next(10_000, 99_999)}");
        var team = TeamOfTeams.Create($"Backfill {code.Value} {Guid.NewGuid():N}", code, null, ActiveDate, "America/Chicago", EventActor.System, Now);
        context.TeamOfTeams.Add(team);
        await context.SaveChangesAsync(ct);

        await context.ActivityLogs
            .Where(a => a.AggregateId == team.Id && a.EventType == nameof(TeamOfTeamsOperatingModelSetEvent))
            .ExecuteDeleteAsync(ct);

        return team;
    }

    private async Task RunBackfill(CancellationToken ct)
    {
        // Straight through ADO: the payload's braces would be read as format placeholders by ExecuteSqlRaw.
        await using var context = _fixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = AddTeamOfTeamsOperatingModels.RecordBackfilledModelsSql;
        await command.ExecuteNonQueryAsync(ct);
    }

    [Fact]
    public async Task RecordBackfilledModels_WritesTheEntryALiveSetEventWould()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var team = await SeedTeamOfTeamsWithAnUnrecordedModel(ct);
        var model = team.OperatingModels.Single();

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var entry = await verify.ActivityLogs.AsNoTracking()
            .SingleAsync(a => a.AggregateId == team.Id && a.EventType == nameof(TeamOfTeamsOperatingModelSetEvent), ct);

        var liveEvent = new TeamOfTeamsOperatingModelSetEvent(
            team.Id,
            team.Key,
            new FlexibleDateRange(ActiveDate),
            new TeamOfTeamsOperatingModelSettings("America/Chicago"),
            null,
            EventActor.System,
            entry.Timestamp)
        {
            EventId = model.Id,
        };
        var expected = ActivityLogEntryFactory.CreateActivityLogEntry(liveEvent, liveEvent, 0, null);

        entry.EventId.Should().Be(model.Id);
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
            .Should().BeTrue($"the backfilled payload {entry.Payload} should match the serialized event {expected.Payload}");
    }

    [Fact]
    public async Task RecordBackfilledModels_RunTwice_WritesOneEntry()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var team = await SeedTeamOfTeamsWithAnUnrecordedModel(ct);
        await RunBackfill(ct);

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.ActivityLogs.CountAsync(a => a.AggregateId == team.Id && a.EventType == nameof(TeamOfTeamsOperatingModelSetEvent), ct))
            .Should().Be(1);
    }
}
