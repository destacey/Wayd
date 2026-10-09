using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Enums.Organization;
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
/// Proves the activity entry the Add-TeamOperatingModel-WorkingDays migration writes for each model is the one a
/// live System <see cref="TeamOperatingModelCorrectedEvent"/> would have written.
/// </summary>
/// <remarks>
/// The payload is hand-built T-SQL, so only a real SQL Server shows it reads back the way the serializer writes it.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class TeamOperatingModelWorkingDaysBackfillTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate ActiveDate = new(2024, 3, 1);
    private static readonly LocalDate ChangedOn = new(2025, 1, 6);
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private static readonly WorkingWeek SundayToThursday = WorkingWeek.Create(
        [IsoDayOfWeek.Sunday, IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Thursday]).Value;

    private readonly SqlServerDbContextFixture _fixture = fixture;

    /// <summary>A team with a closed Monday-to-Friday model and a current Sunday-to-Thursday one.</summary>
    private async Task<Team> SeedTeamWithTwoModels(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var code = new TeamCode($"W{Random.Shared.Next(10_000, 99_999)}");
        var team = Team.Create($"Backfill {code.Value} {Guid.NewGuid():N}", code, null, ActiveDate, Methodology.Scrum, SizingMethod.StoryPoints,
            "America/Chicago", 1, WorkingWeek.MondayToFriday, EventActor.System, Now);
        team.SetOperatingModel(ChangedOn, Methodology.Kanban, SizingMethod.Count, "Asia/Jerusalem", 2, SundayToThursday, null, EventActor.System, Now)
            .IsSuccess.Should().BeTrue();
        context.Teams.Add(team);
        await context.SaveChangesAsync(ct);
        return team;
    }

    private async Task RunBackfill(CancellationToken ct)
    {
        // Straight through ADO: the payload's braces would be read as format placeholders by ExecuteSqlRaw.
        await using var context = _fixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = AddTeamOperatingModelWorkingDays.RecordAssignedWorkingDaysSql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<List<ActivityLogEntry>> CorrectedEntries(Guid teamId, CancellationToken ct)
    {
        await using var verify = _fixture.CreateContext();
        return await verify.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == teamId && a.EventType == nameof(TeamOperatingModelCorrectedEvent))
            .ToListAsync(ct);
    }

    [Fact]
    public async Task RecordAssignedWorkingDays_WritesTheEntryALiveSystemCorrectionWould()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var team = await SeedTeamWithTwoModels(ct);

        // Act
        await RunBackfill(ct);

        // Assert
        var entries = await CorrectedEntries(team.Id, ct);
        entries.Should().HaveCount(2);

        foreach (var model in team.OperatingModels)
        {
            var period = new FlexibleDateRange(model.DateRange.Start, model.DateRange.End);
            var settings = new TeamOperatingModelSettings(model.Methodology, model.SizingMethod, model.TimeZone, model.CommitmentGraceDays, model.WorkingWeek.Days);
            var entry = entries.Single(e => JsonNode.Parse(e.Payload)!["period"]!["start"]!.GetValue<string>() == model.DateRange.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            var liveEvent = new TeamOperatingModelCorrectedEvent(
                team.Id,
                team.Key,
                period,
                settings,
                settings with { WorkingDays = null },
                EventActor.System,
                entry.Timestamp)
            {
                EventId = entry.EventId,
            };
            var expected = ActivityLogEntryFactory.CreateActivityLogEntry(liveEvent, liveEvent, 0, null);

            // The migration is frozen at the 1.1 shape it shipped with; later minor versions only add fields, so
            // the live payload less those fields is what it must have written.
            var expectedPayload = JsonNode.Parse(expected.Payload)!;
            expectedPayload["eventVersion"] = "1.1";
            expectedPayload["settings"]!.AsObject().Remove("holidayCalendarId");
            expectedPayload["previous"]!.AsObject().Remove("holidayCalendarId");

            entry.EventId.Should().NotBe(model.Id);
            entry.EventVersion.Should().Be("1.1");
            entry.Should().BeEquivalentTo(expected, options => options
                .Including(e => e.EventType)
                .Including(e => e.Category)
                .Including(e => e.DomainArea)
                .Including(e => e.AggregateType)
                .Including(e => e.AggregateId)
                .Including(e => e.ActorKind)
                .Including(e => e.UserId)
                .Including(e => e.EmployeeId)
                .Including(e => e.Ordinal)
                .Including(e => e.CorrelationId)
                .Including(e => e.Summary));
            JsonNode.DeepEquals(JsonNode.Parse(entry.Payload), expectedPayload)
                .Should().BeTrue($"the backfilled payload {entry.Payload} should match the serialized event {expectedPayload.ToJsonString()}");
        }
    }

    [Fact]
    public async Task RecordAssignedWorkingDays_RunTwice_WritesOneEntryPerModel()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var team = await SeedTeamWithTwoModels(ct);
        await RunBackfill(ct);

        // Act
        await RunBackfill(ct);

        // Assert
        (await CorrectedEntries(team.Id, ct)).Should().HaveCount(2);
    }
}
