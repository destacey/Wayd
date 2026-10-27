using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Infrastructure.Persistence.Activities;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves each baseline the Backfill-Intake-Channel-Baseline-Activity migration writes is the entry
/// <see cref="ActivityLogEntryFactory"/> builds for the same baseline event.
/// </summary>
/// <remarks>
/// The payloads are hand-built T-SQL, so only a real SQL Server shows they read back the way the serializer writes
/// them.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class IntakeChannelBaselineBackfillTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Theory]
    [InlineData("Raised by security: a \"finding\", a risk or a control.", true)]
    [InlineData(null, false)]
    public async Task Backfill_WritesTheChannelBaselineTheFactoryWould(string? description, bool isActive)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var channel = await Seed(description, isActive, ct);

        // Act
        await RunBackfill(ct);

        // Assert
        await AssertBaseline(channel, (recordCreatedOn, timestamp) => new IntakeChannelBaselinedEvent(
            channel.Id, channel.Name, channel.Description, channel.Order, recordCreatedOn, null, timestamp), ct);
    }

    [Fact]
    public async Task Backfill_ChannelWithACreationEntry_IsLeftAsItIs()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var channel = IntakeChannel.Create(UniqueName(), null, 1, EventActor.System, Now);
        await using (var context = _fixture.CreateContext())
        {
            context.IntakeChannels.Add(channel);
            await context.SaveChangesAsync(ct);
        }

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var entries = await verify.ActivityLogs.AsNoTracking().Where(a => a.AggregateId == channel.Id).ToListAsync(ct);
        entries.Should().ContainSingle().Which.EventType.Should().Be(nameof(IntakeChannelCreatedEvent));
    }

    [Fact]
    public async Task Backfill_RunTwice_WritesOneBaseline()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var channel = await Seed(null, true, ct);
        await RunBackfill(ct);

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.ActivityLogs.CountAsync(a => a.AggregateId == channel.Id, ct)).Should().Be(1);
    }

    private static string UniqueName() => $"Channel {Guid.NewGuid():N}"[..30];

    /// <summary>
    /// Saves a channel, then removes the entries its save wrote, leaving it as every seeded channel was when the
    /// migration ran: on file with no history.
    /// </summary>
    private async Task<IntakeChannel> Seed(string? description, bool isActive, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var channel = IntakeChannel.Create(UniqueName(), description, 7, EventActor.System, Now);
        if (!isActive)
            channel.Archive(EventActor.System, Now);

        context.IntakeChannels.Add(channel);
        await context.SaveChangesAsync(ct);

        await context.ActivityLogs.Where(a => a.AggregateId == channel.Id).ExecuteDeleteAsync(ct);

        return channel;
    }

    private async Task RunBackfill(CancellationToken ct)
    {
        // Straight through ADO: the payload's braces would be read as format placeholders by ExecuteSqlRaw.
        await using var context = _fixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = BackfillIntakeChannelBaselineActivity.UpSql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task AssertBaseline(IntakeChannel channel, Func<Instant?, Instant, DomainEvent> baseline, CancellationToken ct)
    {
        await using var verify = _fixture.CreateContext();
        var entry = await verify.ActivityLogs.AsNoTracking()
            .SingleAsync(a => a.AggregateId == channel.Id, ct);
        var recordCreatedOn = await verify.IntakeChannels
            .Where(c => c.Id == channel.Id)
            .Select(c => EF.Property<Instant>(c, "SystemCreated"))
            .SingleAsync(ct);

        var expectedEvent = baseline(recordCreatedOn, entry.Timestamp);
        var expected = ActivityLogEntryFactory.CreateActivityLogEntry(expectedEvent, channel, 0, null);

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
}
