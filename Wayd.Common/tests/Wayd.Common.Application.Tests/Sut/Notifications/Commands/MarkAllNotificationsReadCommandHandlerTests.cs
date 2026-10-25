using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Notifications.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;
using Wayd.Tests.Shared;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Application.Tests.Sut.Notifications.Commands;

public sealed class MarkAllNotificationsReadCommandHandlerTests
{
    private const string GadgetType = "test.mark-all-read.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 24, 17, 0);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public MarkAllNotificationsReadCommandHandlerTests()
    {
        FollowOwners.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private MarkAllNotificationsReadCommandHandler CreateHandler() =>
        new(new FakeNotificationStore(_dbContext), _currentPrincipal.Object, new TestingDateTimeProvider(new FakeClock(Now)));

    [Fact]
    public async Task Handle_ShouldMarkEveryUnreadNotificationOfTheCaller_AndLeaveEarlierReadsAlone()
    {
        // Arrange
        var earlier = Now.Minus(Duration.FromDays(1));
        var unread = Seed(_employeeId);
        var alreadyRead = Seed(_employeeId, readOn: earlier);
        var someoneElses = Seed(Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new MarkAllNotificationsReadCommand(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        unread.ReadOn.Should().Be(Now);
        alreadyRead.ReadOn.Should().Be(earlier);
        someoneElses.ReadOn.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_ForACallerWithNoEmployee()
    {
        // Arrange
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var act = () => CreateHandler().Handle(new MarkAllNotificationsReadCommand(), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    private Notification Seed(Guid recipientId, Instant? readOn = null)
    {
        var notification = new NotificationFaker()
            .WithRecipientEmployeeId(recipientId)
            .WithOwner(GadgetType, Guid.NewGuid())
            .WithReadOn(readOn)
            .Generate();
        _dbContext.AddNotification(notification);
        return notification;
    }
}
