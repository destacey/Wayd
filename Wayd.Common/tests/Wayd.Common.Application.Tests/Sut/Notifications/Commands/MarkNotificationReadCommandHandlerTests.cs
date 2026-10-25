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

public sealed class MarkNotificationReadCommandHandlerTests
{
    private const string GadgetType = "test.mark-notification-read.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 24, 17, 0);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public MarkNotificationReadCommandHandlerTests()
    {
        FollowOwners.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private MarkNotificationReadCommandHandler CreateHandler() =>
        new(new FakeNotificationStore(_dbContext), _currentPrincipal.Object, new TestingDateTimeProvider(new FakeClock(Now)));

    [Fact]
    public async Task Handle_ShouldMarkTheCallersNotificationRead()
    {
        // Arrange
        var notification = Seed(id: 7, _employeeId);

        // Act
        var result = await CreateHandler().Handle(new MarkNotificationReadCommand(7), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeTrue();
        notification.ReadOn.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_ShouldReportNotFound_ForSomeoneElsesNotification()
    {
        // Arrange
        var notification = Seed(id: 7, Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new MarkNotificationReadCommand(7), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeFalse();
        notification.ReadOn.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_ForACallerWithNoEmployee()
    {
        // Arrange
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var act = () => CreateHandler().Handle(new MarkNotificationReadCommand(7), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    private Notification Seed(long id, Guid recipientId)
    {
        var notification = new NotificationFaker()
            .WithId(id)
            .WithRecipientEmployeeId(recipientId)
            .WithOwner(GadgetType, Guid.NewGuid())
            .Generate();
        _dbContext.AddNotification(notification);
        return notification;
    }
}
