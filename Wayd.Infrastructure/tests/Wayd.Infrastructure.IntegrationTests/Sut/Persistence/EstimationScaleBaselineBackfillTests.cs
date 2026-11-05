using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;
using Wayd.Infrastructure.Persistence.Activities;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves each baseline the Backfill-Estimation-Scale-Baseline-Activity migration writes is the entry
/// <see cref="ActivityLogEntryFactory"/> builds for the same baseline event.
/// </summary>
/// <remarks>
/// The payloads are hand-built T-SQL, so only a real SQL Server shows they read back the way the serializer writes
/// them.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class EstimationScaleBaselineBackfillTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private static readonly EstimationScaleValue[] Values =
    [
        new("1", true), new("2", true), new("3", true), new("?", false), new("∞", false), new("☕", false),
    ];

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Theory]
    [InlineData("Relative \"t-shirt\" sizing")]
    [InlineData(null)]
    public async Task Backfill_WritesTheBaselineTheFactoryWould(string? description)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scale = await SeedScale(ct, description, publishSecondVersion: false);

        // Act
        await RunBackfill(ct);

        // Assert
        await AssertBaseline(scale.Id, expectedVersionNumber: 1, ct);
    }

    [Fact]
    public async Task Backfill_AScaleWithTwoVersions_BaselinesTheCurrentVersion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scale = await SeedScale(ct, "Two versions", publishSecondVersion: true);

        // Act
        await RunBackfill(ct);

        // Assert
        await AssertBaseline(scale.Id, expectedVersionNumber: 2, ct);
    }

    [Fact]
    public async Task Backfill_RunTwice_WritesOneBaseline()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scale = await SeedScale(ct, null, publishSecondVersion: false);
        await RunBackfill(ct);

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.ActivityLogs.CountAsync(a => a.AggregateId == scale.Id, ct)).Should().Be(1);
    }

    [Fact]
    public async Task Backfill_AScaleWithACreationEntry_IsLeftAsItIs()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scale = EstimationScale.Create(UniqueName(), null, Values, EventActor.System, Now).Value;
        await using (var context = _fixture.CreateContext())
        {
            context.EstimationScales.Add(scale);
            await context.SaveChangesAsync(ct);
        }

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var entry = await verify.ActivityLogs.AsNoTracking().SingleAsync(a => a.AggregateId == scale.Id, ct);
        entry.EventType.Should().Be(nameof(EstimationScaleCreatedEvent));
    }

    private static string UniqueName() => $"Scale {Guid.NewGuid():N}"[..20];

    /// <summary>
    /// Saves a scale, then removes the entries its saves wrote, leaving it as every scale was when the migration
    /// ran: on file with no history.
    /// </summary>
    private async Task<EstimationScale> SeedScale(CancellationToken ct, string? description, bool publishSecondVersion)
    {
        var scale = EstimationScale.Create(UniqueName(), description, Values, EventActor.System, Now).Value;
        if (publishSecondVersion)
            scale.PublishVersion([new("S", true), new("M", true), new("L", true), new("?", false)], EventActor.System, Now);

        await using var context = _fixture.CreateContext();
        context.EstimationScales.Add(scale);
        await context.SaveChangesAsync(ct);

        await context.ActivityLogs.Where(a => a.AggregateId == scale.Id).ExecuteDeleteAsync(ct);

        return scale;
    }

    private async Task RunBackfill(CancellationToken ct)
    {
        // Straight through ADO: the payload's braces would be read as format placeholders by ExecuteSqlRaw.
        await using var context = _fixture.CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = BackfillEstimationScaleBaselineActivity.UpSql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task AssertBaseline(Guid scaleId, int expectedVersionNumber, CancellationToken ct)
    {
        await using var verify = _fixture.CreateContext();
        var entry = await verify.ActivityLogs.AsNoTracking().SingleAsync(a => a.AggregateId == scaleId, ct);
        var scale = await verify.EstimationScales.AsNoTracking()
            .Include(s => s.Versions)
            .SingleAsync(s => s.Id == scaleId, ct);
        var recordCreatedOn = await verify.EstimationScales
            .Where(s => s.Id == scaleId)
            .Select(s => EF.Property<Instant>(s, "SystemCreated"))
            .SingleAsync(ct);

        var current = scale.CurrentVersion;
        current.Number.Should().Be(expectedVersionNumber);
        var version = new EstimationScaleVersionValues(current.Id, current.Number,
            [.. current.Values.Select(v => new EstimationScaleValueValues(v.Value, v.IsEstimate))]);

        var expectedEvent = new EstimationScaleBaselinedEvent(scale.Id, scale.Key, scale.Name, scale.Description,
            scale.IsActive, version, recordCreatedOn, null, entry.Timestamp);
        var expected = ActivityLogEntryFactory.CreateActivityLogEntry(expectedEvent, scale, 0, null);

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
