using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Activities;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Runs the Backfill-Product-Reparent-Related-Activity migration over moves recorded before the event named
/// its parents as related aggregates.
/// </summary>
/// <remarks>
/// The migration reads the parent ids out of the stored JSON, so each entry is written with the payload the
/// factory serializes for a real event: only that shows the paths match what the serializer wrote.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class BackfillProductReparentRelatedActivityMigrationTests(SqlServerDbContextFixture fixture)
{
    private const string MigrationBefore = "20260917010702_AddActivityLogRelatedAggregates";

    private static readonly Instant MovedAt = Instant.FromUtc(2026, 3, 1, 12, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Up_RelatesEachRecordedMoveToTheParentsItNamed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var fromParentId = Guid.CreateVersion7();
        var toParentId = Guid.CreateVersion7();
        var rootedParentId = Guid.CreateVersion7();
        var supersededParentId = Guid.CreateVersion7();

        var moved = UnrelatedEntry(new ProductReparentedEventV2(Guid.CreateVersion7(), 1, fromParentId, toParentId, EventActor.System, MovedAt));
        var fromRoot = UnrelatedEntry(new ProductReparentedEventV2(Guid.CreateVersion7(), 2, null, rootedParentId, EventActor.System, MovedAt));
#pragma warning disable CS0618 // Entries written as the superseded type are exactly what the backfill must also reach.
        var superseded = UnrelatedEntry(new ProductReparentedEvent(Guid.CreateVersion7(), 3, "Atlas", null, supersededParentId, EventActor.System, MovedAt));
#pragma warning restore CS0618
        var renamed = UnrelatedEntry(new ProductDetailsUpdatedEvent(Guid.CreateVersion7(), 4, "Atlas", null, null, EventActor.System, MovedAt));

        await using (var context = _fixture.CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync(MigrationBefore, ct);

            context.ActivityLogs.AddRange(moved, fromRoot, superseded, renamed);
            await context.SaveChangesAsync(ct);

            // Act
            await context.Database.MigrateAsync(ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        var entries = await verify.ActivityLogs.AsNoTracking()
            .Include(a => a.RelatedAggregates)
            .Where(a => new[] { moved.Id, fromRoot.Id, superseded.Id, renamed.Id }.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        entries[moved.Id].RelatedAggregates.Should().BeEquivalentTo(
            [new AggregateReference("Product", fromParentId), new AggregateReference("Product", toParentId)]);
        entries[fromRoot.Id].RelatedAggregates.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new AggregateReference("Product", rootedParentId), "a root has no parent to relate");
        entries[superseded.Id].RelatedAggregates.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new AggregateReference("Product", supersededParentId));
        entries[renamed.Id].RelatedAggregates.Should().BeEmpty();
    }

    [Fact]
    public async Task Up_SkipsAParentAlreadyRelated()
    {
        // Arrange — a move the live event already related, as a rerun or a row written after deploy leaves it.
        var ct = TestContext.Current.CancellationToken;
        var raised = new ProductReparentedEventV2(Guid.CreateVersion7(), 5, Guid.CreateVersion7(), Guid.CreateVersion7(), EventActor.System, MovedAt);
        var live = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, ordinal: 0, correlationId: null);

        await using (var context = _fixture.CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync(MigrationBefore, ct);

            context.ActivityLogs.Add(live);
            await context.SaveChangesAsync(ct);

            // Act
            await context.Database.MigrateAsync(ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        var entry = await verify.ActivityLogs.AsNoTracking().Include(a => a.RelatedAggregates).SingleAsync(a => a.Id == live.Id, ct);
        entry.RelatedAggregates.Should().BeEquivalentTo(raised.RelatedAggregates);
    }

    [Fact]
    public async Task Down_RemovesTheRelatedRowsOfMoves_AndLeavesOthers()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var raised = new ProductReparentedEventV2(Guid.CreateVersion7(), 6, Guid.CreateVersion7(), Guid.CreateVersion7(), EventActor.System, MovedAt);
        var move = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, ordinal: 0, correlationId: null);
        var other = new ActivityLogEntry(
            Guid.CreateVersion7(), "ProjectStubEvent", ActivityCategory.Updated, "Ppm", "Project", Guid.CreateVersion7(),
            EventActor.System, MovedAt, ordinal: 0, correlationId: null, "{}", "Project Stub", "1.0",
            [new AggregateReference("Program", Guid.CreateVersion7())]);

        await using (var context = _fixture.CreateContext())
        {
            context.ActivityLogs.AddRange(move, other);
            await context.SaveChangesAsync(ct);
        }

        try
        {
            // Act
            await using (var context = _fixture.CreateContext())
            {
                await context.GetService<IMigrator>().MigrateAsync(MigrationBefore, ct);
            }

            // Assert
            await using var verify = _fixture.CreateContext();
            var related = verify.ActivityLogs.AsNoTracking().Include(a => a.RelatedAggregates);
            (await related.SingleAsync(a => a.Id == move.Id, ct)).RelatedAggregates.Should().BeEmpty();
            (await related.SingleAsync(a => a.Id == other.Id, ct)).RelatedAggregates.Should().ContainSingle();
        }
        finally
        {
            // Leave the shared database at the latest migration for the tests that follow.
            await using var restore = _fixture.CreateContext();
            await restore.Database.MigrateAsync(ct);
        }
    }

    /// <summary>
    /// The entry the factory writes for an event, minus its related rows — what a move looked like before the
    /// event named any.
    /// </summary>
    private static ActivityLogEntry UnrelatedEntry<TEvent>(TEvent raised)
        where TEvent : DomainEvent, IAggregateEvent
    {
        var entry = ActivityLogEntryFactory.CreateActivityLogEntry(raised, raised, ordinal: 0, correlationId: null);

        return new ActivityLogEntry(
            entry.Id, entry.EventType, entry.Category, entry.DomainArea, entry.AggregateType, entry.AggregateId,
            raised.Actor, entry.Timestamp, entry.Ordinal, entry.CorrelationId, entry.Payload, entry.Summary, entry.EventVersion);
    }
}
