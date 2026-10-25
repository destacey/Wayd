using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Notifications;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Notifications;

/// <summary>
/// The store against real SQL Server: what makes a redelivered event write nothing twice is the unique key on
/// (event, recipient), which no fake enforces.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class NotificationStoreIntegrationTests(SqlServerDbContextFixture fixture)
{
    private const string GadgetType = "test.notification-store.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 10, 24, 10, 0);

    [Fact]
    public async Task Add_ShouldSaveEveryNotificationOfTheEvent()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var avery = await NotificationTestData.SeedEmployee(fixture);
        var blake = await NotificationTestData.SeedEmployee(fixture);
        var eventId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        await using var context = fixture.CreateContext();

        // Act
        var added = await new NotificationStore(context).Add(
            [New(eventId, ownerId, avery.Id), New(eventId, ownerId, blake.Id, NotificationReason.Participant | NotificationReason.Following)],
            cancellationToken);

        // Assert
        added.Should().Be(2);
        await using var reader = fixture.CreateContext();
        var stored = await reader.Notifications.Where(n => n.EventId == eventId).ToListAsync(cancellationToken);
        stored.Should().HaveCount(2);
        var blakes = stored.Single(n => n.RecipientEmployeeId == blake.Id);
        blakes.OwnerType.Should().Be(GadgetType);
        blakes.OwnerId.Should().Be(ownerId);
        blakes.Event.Should().Be(NotificationEvent.StatusChanged);
        blakes.ActorKind.Should().Be(EventActorKind.User);
        blakes.OccurredOn.Should().Be(Timestamp);
        blakes.Reasons.Should().Be(NotificationReason.Participant | NotificationReason.Following);
        blakes.ReadOn.Should().BeNull();
        stored.Select(n => n.Id).Should().OnlyHaveUniqueItems().And.NotContain(0);
    }

    [Fact]
    public async Task Add_ShouldWriteOnlyTheMissingRecipients_WhenTheEventIsDeliveredAgain()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var avery = await NotificationTestData.SeedEmployee(fixture);
        var blake = await NotificationTestData.SeedEmployee(fixture);
        var eventId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        await using (var first = fixture.CreateContext())
        {
            await new NotificationStore(first).Add([New(eventId, ownerId, avery.Id)], cancellationToken);
        }

        await using var context = fixture.CreateContext();

        // Act
        var added = await new NotificationStore(context).Add(
            [New(eventId, ownerId, avery.Id), New(eventId, ownerId, blake.Id)], cancellationToken);

        // Assert
        added.Should().Be(1);
        await using var reader = fixture.CreateContext();
        (await reader.Notifications.CountAsync(n => n.EventId == eventId, cancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task Add_ShouldLetExactlyOneOfManyConcurrentDeliveriesWrite()
    {
        // Arrange — each delivery on its own context, as concurrent handlers would be, so every one can pass
        // the existence check before any of them commits.
        var cancellationToken = TestContext.Current.CancellationToken;
        var avery = await NotificationTestData.SeedEmployee(fixture);
        var blake = await NotificationTestData.SeedEmployee(fixture);
        var eventId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        var contexts = Enumerable.Range(0, 8).Select(_ => fixture.CreateContext()).ToList();

        try
        {
            // Act
            var outcomes = await Task.WhenAll(contexts.Select(c => Task.Run(
                () => new NotificationStore(c).Add([New(eventId, ownerId, avery.Id), New(eventId, ownerId, blake.Id)], cancellationToken),
                cancellationToken)));

            // Assert
            outcomes.Sum().Should().Be(2);
            await using var reader = fixture.CreateContext();
            (await reader.Notifications.CountAsync(n => n.EventId == eventId, cancellationToken)).Should().Be(2);
        }
        finally
        {
            foreach (var context in contexts)
            {
                await context.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Add_ShouldTellEveryoneEitherDeliveryWouldHave_WhenConcurrentDeliveriesSeeDifferentAudiences()
    {
        // Arrange — a follow added between two deliveries' reads gives the later one a larger audience; it
        // must not lose that extra recipient because the earlier one already wrote the rest.
        var cancellationToken = TestContext.Current.CancellationToken;
        var avery = await NotificationTestData.SeedEmployee(fixture);
        var blake = await NotificationTestData.SeedEmployee(fixture);
        var casey = await NotificationTestData.SeedEmployee(fixture);
        var eventId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        var contexts = Enumerable.Range(0, 8).Select(_ => fixture.CreateContext()).ToList();

        try
        {
            // Act
            await Task.WhenAll(contexts.Select((c, i) => Task.Run(
                () => new NotificationStore(c).Add(
                    i % 2 == 0
                        ? [New(eventId, ownerId, avery.Id), New(eventId, ownerId, blake.Id)]
                        : [New(eventId, ownerId, avery.Id), New(eventId, ownerId, blake.Id), New(eventId, ownerId, casey.Id)],
                    cancellationToken),
                cancellationToken)));

            // Assert
            await using var reader = fixture.CreateContext();
            var recipients = await reader.Notifications
                .Where(n => n.EventId == eventId)
                .Select(n => n.RecipientEmployeeId)
                .ToListAsync(cancellationToken);
            recipients.Should().BeEquivalentTo([avery.Id, blake.Id, casey.Id]);
        }
        finally
        {
            foreach (var context in contexts)
            {
                await context.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Add_ShouldRefuseNotificationsOfMoreThanOneEvent()
    {
        // Arrange
        var avery = await NotificationTestData.SeedEmployee(fixture);
        await using var context = fixture.CreateContext();

        // Act
        var act = () => new NotificationStore(context).Add(
            [New(Guid.CreateVersion7(), Guid.CreateVersion7(), avery.Id), New(Guid.CreateVersion7(), Guid.CreateVersion7(), avery.Id)],
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static Notification New(
        Guid eventId,
        Guid ownerId,
        Guid recipientId,
        NotificationReason reasons = NotificationReason.Following,
        Instant? occurredOn = null)
    {
        FollowOwners.Register(Gadget);
        return Notification.Create(
            recipientId, GadgetType, ownerId, eventId, NotificationEvent.StatusChanged,
            EventActorKind.User, null, occurredOn ?? Timestamp, reasons).Value;
    }
}
