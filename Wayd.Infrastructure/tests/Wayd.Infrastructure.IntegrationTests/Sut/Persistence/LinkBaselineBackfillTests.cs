using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Links;
using Wayd.Common.Domain.Links;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Infrastructure.Persistence.Activities;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.Work.Domain;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves each baseline the Backfill-Link-Baseline-Activity migration writes is the entry
/// <see cref="ActivityLogEntryFactory"/> builds for the same baseline event, filed under the link's record too.
/// </summary>
/// <remarks>
/// The payloads are hand-built T-SQL, so only a real SQL Server shows they read back the way the serializer writes
/// them.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class LinkBaselineBackfillTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 17, 9, 30, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    static LinkBaselineBackfillTests()
    {
        PpmLinkOwners.Register();
        WorkLinkOwners.Register();
    }

    public static TheoryData<string, string, string, string> Links => new()
    {
        { PpmLinkOwners.Project.Key, "Project", "Vendor \"contract\" → v2", "https://example.com/a?b=1&c=<d>" },
        { WorkLinkOwners.Sprint.Key, "Iteration", "Sprint board", "http://example.com/board" },
    };

    [Theory]
    [MemberData(nameof(Links))]
    public async Task Backfill_WritesTheLinkBaselineTheFactoryWould_FiledUnderItsRecord(
        string ownerType, string ownerAggregateType, string name, string url)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var link = await Seed(ownerType, name, url, ct);

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var entry = await verify.ActivityLogs.AsNoTracking()
            .Include(a => a.RelatedAggregates)
            .SingleAsync(a => a.AggregateId == link.Id, ct);

        var expectedEvent = new LinkBaselinedEvent(link.Id, link.OwnerType, link.OwnerId, link.Name, link.Url, null, null, entry.Timestamp);
        var expected = ActivityLogEntryFactory.CreateActivityLogEntry(expectedEvent, link, 0, null);

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

        entry.RelatedAggregates.Select(r => (r.AggregateType, r.AggregateId))
            .Should().BeEquivalentTo(expected.RelatedAggregates.Select(r => (r.AggregateType, r.AggregateId)))
            .And.ContainSingle().Which.Should().Be((ownerAggregateType, link.OwnerId));
    }

    [Fact]
    public async Task Backfill_LinkWithACreationEntry_IsLeftAsItIs()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var link = Link.Create(PpmLinkOwners.Project.Key, Guid.CreateVersion7(), "Charter", "https://example.com/charter", EventActor.System, Now).Value;
        await using (var context = _fixture.CreateContext())
        {
            context.Links.Add(link);
            await context.SaveChangesAsync(ct);
        }

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var entries = await verify.ActivityLogs.AsNoTracking().Where(a => a.AggregateId == link.Id).ToListAsync(ct);
        entries.Should().ContainSingle().Which.EventType.Should().Be(nameof(LinkAddedEvent));
    }

    [Fact]
    public async Task Backfill_RunTwice_WritesOneBaseline()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var link = await Seed(PpmLinkOwners.Project.Key, "Charter", "https://example.com/charter", ct);
        await RunBackfill(ct);

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.ActivityLogs.CountAsync(a => a.AggregateId == link.Id, ct)).Should().Be(1);
    }

    /// <summary>
    /// Saves a link, then removes the entry its save wrote, leaving it as every link was when the migration ran: on
    /// file with no history.
    /// </summary>
    private async Task<Link> Seed(string ownerType, string name, string url, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var link = Link.Create(ownerType, Guid.CreateVersion7(), name, url, EventActor.System, Now).Value;
        context.Links.Add(link);
        await context.SaveChangesAsync(ct);

        await context.ActivityLogs.Where(a => a.AggregateId == link.Id).ExecuteDeleteAsync(ct);

        return link;
    }

    private async Task RunBackfill(CancellationToken ct)
    {
        // Straight through ADO: the payload's braces would be read as format placeholders by ExecuteSqlRaw.
        await using var context = _fixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = BackfillLinkBaselineActivity.UpSql;
        await command.ExecuteNonQueryAsync(ct);
    }
}
