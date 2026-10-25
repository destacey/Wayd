using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Notifications;
using Wayd.Common.Application.Notifications.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Notifications.Commands;

public sealed class PurgeExpiredNotificationsCommandHandlerTests
{
    private const string GadgetType = "test.purge-notifications.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 24, 3, 30);

    private readonly FakeWaydDbContext _dbContext = new();

    public PurgeExpiredNotificationsCommandHandlerTests()
    {
        FollowOwners.Register(Gadget);
    }

    private PurgeExpiredNotificationsCommandHandler CreateHandler(INotificationStore? store = null) => new(
        store ?? new FakeNotificationStore(_dbContext),
        new TestingDateTimeProvider(new FakeClock(Now)),
        NullLogger<PurgeExpiredNotificationsCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ShouldDeleteNotificationsOlderThanNinetyDays_ReadOrNot()
    {
        // Arrange
        var cutoff = Now - Duration.FromDays(90);
        Seed(cutoff - Duration.FromSeconds(1));
        Seed(cutoff - Duration.FromDays(200), readOn: cutoff - Duration.FromDays(199));
        var kept = Seed(cutoff);
        var recent = Seed(Now - Duration.FromDays(1));

        // Act
        var result = await CreateHandler().Handle(new PurgeExpiredNotificationsCommand(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().Be(2);
        _dbContext.Notifications.Should().BeEquivalentTo([kept, recent]);
    }

    [Fact]
    public async Task Handle_ShouldKeepDeletingInBatches_UntilNoneOlderRemain()
    {
        // Arrange
        var store = new Mock<INotificationStore>();
        store.SetupSequence(s => s.DeleteOlderThan(It.IsAny<Instant>(), PurgeExpiredNotificationsCommandHandler.BatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PurgeExpiredNotificationsCommandHandler.BatchSize)
            .ReturnsAsync(PurgeExpiredNotificationsCommandHandler.BatchSize)
            .ReturnsAsync(12);

        // Act
        var result = await CreateHandler(store.Object).Handle(new PurgeExpiredNotificationsCommand(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be((2 * PurgeExpiredNotificationsCommandHandler.BatchSize) + 12);
        store.Verify(s => s.DeleteOlderThan(Now - Duration.FromDays(90), PurgeExpiredNotificationsCommandHandler.BatchSize, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    private Notification Seed(Instant occurredOn, Instant? readOn = null)
    {
        var notification = new NotificationFaker()
            .WithOwner(GadgetType, Guid.NewGuid())
            .WithOccurredOn(occurredOn)
            .WithReadOn(readOn)
            .Generate();
        _dbContext.AddNotification(notification);
        return notification;
    }
}
