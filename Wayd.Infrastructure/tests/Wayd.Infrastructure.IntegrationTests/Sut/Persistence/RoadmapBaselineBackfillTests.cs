using System.Data.SqlTypes;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Roadmaps;
using Wayd.Common.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Infrastructure.Persistence.Activities;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves each baseline the Backfill-Roadmap-Baseline-Activity migration writes is the entry
/// <see cref="ActivityLogEntryFactory"/> builds for the same baseline event.
/// </summary>
/// <remarks>
/// The payloads are hand-built T-SQL, so only a real SQL Server shows they read back the way the serializer writes
/// them.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class RoadmapBaselineBackfillTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);
    private static readonly LocalDate Start = new(2026, 1, 5);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Backfill_WritesTheBaselineTheFactoryWould()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var managerId = await SeedEmployee(ct);
        var roadmap = await SeedRoadmap(ct, managerId, withContent: true);

        // Act
        await RunBackfill(ct);

        // Assert
        await AssertBaseline(roadmap.Id, ct);
    }

    [Fact]
    public async Task Backfill_AnEmptyRoadmap_WritesTheBaselineTheFactoryWould()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var managerId = await SeedEmployee(ct);
        var roadmap = await SeedRoadmap(ct, managerId, withContent: false);

        // Act
        await RunBackfill(ct);

        // Assert
        await AssertBaseline(roadmap.Id, ct);
    }

    [Fact]
    public async Task Backfill_RunTwice_WritesOneBaseline()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var managerId = await SeedEmployee(ct);
        var roadmap = await SeedRoadmap(ct, managerId, withContent: false);
        await RunBackfill(ct);

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.ActivityLogs.CountAsync(a => a.AggregateId == roadmap.Id, ct)).Should().Be(1);
    }

    [Fact]
    public async Task Backfill_ARoadmapWithACreationEntry_IsLeftAsItIs()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var managerId = await SeedEmployee(ct);
        var roadmap = Roadmap.Create("Recorded", null, new LocalDateRange(Start, Start.PlusDays(90)), Visibility.Public, [managerId], EventActor.System, Now).Value;
        await using (var context = _fixture.CreateContext())
        {
            context.Roadmaps.Add(roadmap);
            await context.SaveChangesAsync(ct);
        }

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var entry = await verify.ActivityLogs.AsNoTracking().SingleAsync(a => a.AggregateId == roadmap.Id, ct);
        entry.EventType.Should().Be(nameof(RoadmapCreatedEvent));
    }

    private async Task<Guid> SeedEmployee(CancellationToken ct)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var employee = Employee.Create(new PersonName("Avery", null, "Chen"), $"E-{suffix}", null,
            new EmailAddress($"avery.{suffix}@acme.example"), "Engineer", "Delivery", null, null, true, null,
            EventActor.System, Now);

        await using var context = _fixture.CreateContext();
        context.Employees.Add(employee);
        await context.SaveChangesAsync(ct);

        return employee.Id;
    }

    /// <summary>
    /// Saves a roadmap, then removes the entries its saves wrote, leaving it as every roadmap was when the migration
    /// ran: on file with no history.
    /// </summary>
    private async Task<Roadmap> SeedRoadmap(CancellationToken ct, Guid managerId, bool withContent)
    {
        var roadmap = Roadmap.Create("Platform \"2026\"", withContent ? "Platform work" : null,
            new LocalDateRange(Start, Start.PlusDays(180)), Visibility.Private, [managerId], EventActor.System, Now).Value;

        if (withContent)
        {
            roadmap.UpdateColors([
                new ColorOption("#4096FF", "Committed", 1, true),
                new ColorOption("#FA8C16", "Stretch", 2, false)], managerId, EventActor.System, Now);

            var parent = roadmap.CreateActivity(new Activity("Discovery", null, new LocalDateRange(Start, Start.PlusDays(30)), "#4096FF"), managerId, EventActor.System, Now).Value;
            roadmap.CreateActivity(new Activity("Delivery", "Build it", new LocalDateRange(Start.PlusDays(31), Start.PlusDays(90)), null), managerId, EventActor.System, Now);
            roadmap.CreateActivity(new Activity("Research", null, new LocalDateRange(Start.PlusDays(2), Start.PlusDays(10)), null) { ParentId = parent.Id }, managerId, EventActor.System, Now);
            roadmap.CreateMilestone(new Milestone("Beta", Start.PlusDays(20)) { ParentId = parent.Id }, managerId, EventActor.System, Now);
            roadmap.CreateTimebox(new Timebox("Hardening", new LocalDateRange(Start.PlusDays(60), Start.PlusDays(74))), managerId, EventActor.System, Now);
        }

        await using var context = _fixture.CreateContext();
        context.Roadmaps.Add(roadmap);
        await context.SaveChangesAsync(ct);

        await context.ActivityLogs.Where(a => a.AggregateId == roadmap.Id).ExecuteDeleteAsync(ct);

        return roadmap;
    }

    private async Task RunBackfill(CancellationToken ct)
    {
        // Straight through ADO: the payload's braces would be read as format placeholders by ExecuteSqlRaw.
        await using var context = _fixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = BackfillRoadmapBaselineActivity.UpSql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task AssertBaseline(Guid roadmapId, CancellationToken ct)
    {
        await using var verify = _fixture.CreateContext();
        var entry = await verify.ActivityLogs.AsNoTracking().SingleAsync(a => a.AggregateId == roadmapId, ct);
        var roadmap = await verify.Roadmaps.AsNoTracking()
            .Include(r => r.RoadmapManagers)
            .Include(r => r.Items)
            .SingleAsync(r => r.Id == roadmapId, ct);
        var recordCreatedOn = await verify.Roadmaps
            .Where(r => r.Id == roadmapId)
            .Select(r => EF.Property<Instant>(r, "SystemCreated"))
            .SingleAsync(ct);

        // The migration's order: managers by id, items by type, order, start and id, each id as SQL Server sorts it.
        Guid[] managerIds = [.. roadmap.RoadmapManagers.Select(m => m.ManagerId).OrderBy(id => new SqlGuid(id))];
        RoadmapColorValues[] colors = [.. roadmap.Colors.OrderBy(c => c.Order)
            .Select(c => new RoadmapColorValues(c.Color, c.Name, c.Order, c.IsDefault))];
        RoadmapItemValues[] items = [.. roadmap.Items
            .Select(i => new RoadmapItemValues(i.Id, i.Type, i.Name, i.Description, i.ParentId, i.Color, i switch
            {
                RoadmapActivity a => a.DateRange,
                RoadmapTimebox t => t.DateRange,
                RoadmapMilestone m => new LocalDateRange(m.Date, m.Date),
                _ => throw new InvalidOperationException()
            }, (i as RoadmapActivity)?.Order))
            .OrderBy(i => i.Type)
            .ThenBy(i => i.Order)
            .ThenBy(i => i.DateRange.Start)
            .ThenBy(i => new SqlGuid(i.ItemId))];

        var expectedEvent = new RoadmapBaselinedEvent(roadmap.Id, roadmap.Key, roadmap.Name, roadmap.Description,
            roadmap.DateRange, roadmap.Visibility, roadmap.State, managerIds, colors, items,
            recordCreatedOn, null, entry.Timestamp);
        var expected = ActivityLogEntryFactory.CreateActivityLogEntry(expectedEvent, roadmap, 0, null);

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
            .Should().BeTrue($"the backfilled payload {entry.Payload} should match the serialized event {expected.Payload}");
    }

    private sealed record ColorOption(string Color, string Name, int Order, bool IsDefault) : IUpsertRoadmapColor;

    private sealed record Activity(string Name, string? Description, LocalDateRange DateRange, string? Color) : IUpsertRoadmapActivity
    {
        public Guid? ParentId { get; init; }
    }

    private sealed record Milestone(string Name, LocalDate Date) : IUpsertRoadmapMilestone
    {
        public string? Description => null;
        public Guid? ParentId { get; init; }
        public string? Color => null;
    }

    private sealed record Timebox(string Name, LocalDateRange DateRange) : IUpsertRoadmapTimebox
    {
        public string? Description => null;
        public Guid? ParentId => null;
        public string? Color => null;
    }
}
