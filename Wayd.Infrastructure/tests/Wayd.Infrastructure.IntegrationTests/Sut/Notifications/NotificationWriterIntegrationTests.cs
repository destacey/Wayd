using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Notifications;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Notifications;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Notifications;

/// <summary>
/// The writer's audience lookup against real SQL Server, where a departed employee's follows are still in
/// the table — the cascade from <c>Employees</c> never fires, because employees are only soft-deleted — and
/// the soft-delete filter, not the writer's own check, is what a fake would leave untested.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class NotificationWriterIntegrationTests(SqlServerDbContextFixture fixture)
{
    private const string GadgetType = "test.notification-writer-sql.gadget";
    private const string WidgetType = "test.notification-writer-sql.widget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly FollowOwnerDescriptor Widget = new(WidgetType, "Widget");
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 10, 24, 11, 0);

    [Fact]
    public async Task Notify_ShouldTellOnlyCurrentFollowersAndParticipantsOfThatRecord()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        FollowOwners.Register(Gadget, Widget);
        var ownerId = Guid.CreateVersion7();

        var follower = await NotificationTestData.SeedEmployee(fixture);
        var participantAndFollower = await NotificationTestData.SeedEmployee(fixture);
        var inactiveFollower = await NotificationTestData.SeedEmployee(fixture, isActive: false);
        var deletedFollower = await NotificationTestData.SeedEmployee(fixture);
        var widgetFollower = await NotificationTestData.SeedEmployee(fixture);
        var actor = await NotificationTestData.SeedEmployee(fixture);

        await using (var seed = fixture.CreateContext())
        {
            seed.Follows.AddRange(
                Follow.Create(GadgetType, ownerId, follower.Id, Timestamp).Value,
                Follow.Create(GadgetType, ownerId, participantAndFollower.Id, Timestamp).Value,
                Follow.Create(GadgetType, ownerId, inactiveFollower.Id, Timestamp).Value,
                Follow.Create(GadgetType, ownerId, deletedFollower.Id, Timestamp).Value,
                Follow.Create(GadgetType, ownerId, actor.Id, Timestamp).Value,
                // Same id, different owner type: a different record.
                Follow.Create(WidgetType, ownerId, widgetFollower.Id, Timestamp).Value);
            await seed.SaveChangesAsync(cancellationToken);

            var deleted = await seed.Employees.SingleAsync(e => e.Id == deletedFollower.Id, cancellationToken);
            seed.Employees.Remove(deleted);
            await seed.SaveChangesAsync(cancellationToken);
        }

        var source = new GadgetChangedEvent(EventActor.User("actor-user", actor.Id), Timestamp);
        await using var context = fixture.CreateContext();

        // Act
        var added = await CreateWriter(context).Notify(
            new RecordChange(GadgetType, ownerId, NotificationEvent.StatusChanged, source),
            [.. NotificationRecipient.For(NotificationReason.Role, [participantAndFollower.Id])],
            cancellationToken);

        // Assert
        added.Should().Be(2);
        await using var reader = fixture.CreateContext();
        var stored = await reader.Notifications
            .Where(n => n.EventId == source.EventId)
            .Select(n => new { n.RecipientEmployeeId, n.Reasons, n.ActorEmployeeId })
            .ToListAsync(cancellationToken);
        stored.Should().BeEquivalentTo([
            new { RecipientEmployeeId = follower.Id, Reasons = NotificationReason.Following, ActorEmployeeId = (Guid?)actor.Id },
            new { RecipientEmployeeId = participantAndFollower.Id, Reasons = NotificationReason.Following | NotificationReason.Role, ActorEmployeeId = (Guid?)actor.Id },
        ]);
    }

    [Fact]
    public async Task Notify_ShouldApplyRecordAndReasonMutes()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        FollowOwners.Register(Gadget, Widget);
        var ownerId = Guid.CreateVersion7();

        var recordMuter = await NotificationTestData.SeedEmployee(fixture);
        var participantMuter = await NotificationTestData.SeedEmployee(fixture);
        var widgetMuter = await NotificationTestData.SeedEmployee(fixture);
        var actor = await NotificationTestData.SeedEmployee(fixture);

        await using (var seed = fixture.CreateContext())
        {
            seed.Follows.AddRange(
                Follow.Create(GadgetType, ownerId, recordMuter.Id, Timestamp).Value,
                Follow.Create(GadgetType, ownerId, participantMuter.Id, Timestamp).Value,
                Follow.Create(GadgetType, ownerId, widgetMuter.Id, Timestamp).Value);
            seed.RecordMutes.AddRange(
                RecordMute.Create(GadgetType, ownerId, recordMuter.Id, Timestamp).Value,
                // Same id, different owner type: a different record, so it silences nothing here.
                RecordMute.Create(WidgetType, ownerId, widgetMuter.Id, Timestamp).Value);
            seed.ReasonMutes.Add(ReasonMute.Create(participantMuter.Id, NotificationReason.Role, Timestamp).Value);
            await seed.SaveChangesAsync(cancellationToken);
        }

        var source = new GadgetChangedEvent(EventActor.User("actor-user", actor.Id), Timestamp);
        await using var context = fixture.CreateContext();

        // Act
        await CreateWriter(context).Notify(
            new RecordChange(GadgetType, ownerId, NotificationEvent.StatusChanged, source),
            [.. NotificationRecipient.For(NotificationReason.Role, [recordMuter.Id, participantMuter.Id, widgetMuter.Id])],
            cancellationToken);

        // Assert
        await using var reader = fixture.CreateContext();
        var stored = await reader.Notifications
            .Where(n => n.EventId == source.EventId)
            .Select(n => new { n.RecipientEmployeeId, n.Reasons })
            .ToListAsync(cancellationToken);
        stored.Should().BeEquivalentTo([
            new { RecipientEmployeeId = participantMuter.Id, Reasons = NotificationReason.Following },
            new { RecipientEmployeeId = widgetMuter.Id, Reasons = NotificationReason.Following | NotificationReason.Role },
        ]);
    }

    private static NotificationWriter CreateWriter(WaydDbContext context)
    {
        var featureFlags = new Mock<IFeatureFlags>();
        featureFlags.Setup(f => f.IsEnabled(FeatureFlags.Names.Following, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        return new NotificationWriter(
            context,
            context,
            new NotificationStore(context),
            Mock.Of<IUserService>(),
            featureFlags.Object,
            NullLogger<NotificationWriter>.Instance);
    }

    private sealed record GadgetChangedEvent : DomainEvent<GadgetChangedEvent>, IDomainEventDescriptor
    {
        public GadgetChangedEvent(EventActor actor, Instant timestamp)
            : base(actor, "1.0")
        {
            Timestamp = timestamp;
        }

        public static ActivityCategory ActivityCategory => ActivityCategory.Updated;
    }
}
