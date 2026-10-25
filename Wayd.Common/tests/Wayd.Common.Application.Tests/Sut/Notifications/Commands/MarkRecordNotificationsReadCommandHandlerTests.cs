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
using Wayd.Common.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Notifications.Commands;

public sealed class MarkRecordNotificationsReadCommandHandlerTests
{
    private const string GadgetType = "test.mark-record-read.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 24, 17, 0);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public MarkRecordNotificationsReadCommandHandlerTests()
    {
        FollowOwners.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private MarkRecordNotificationsReadCommandHandler CreateHandler() =>
        new(new FakeNotificationStore(_dbContext), _currentPrincipal.Object, new TestingDateTimeProvider(new FakeClock(Now)));

    [Fact]
    public async Task Handle_ShouldMarkEveryNotificationTheCallerHasAboutTheRecord()
    {
        // Arrange
        var gadgetId = Guid.NewGuid();
        var first = Seed(gadgetId, _employeeId);
        var second = Seed(gadgetId, _employeeId);
        var otherRecord = Seed(Guid.NewGuid(), _employeeId);
        var otherPerson = Seed(gadgetId, Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new MarkRecordNotificationsReadCommand(" TEST.Mark-Record-Read.Gadget ", gadgetId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        first.ReadOn.Should().Be(Now);
        second.ReadOn.Should().Be(Now);
        otherRecord.ReadOn.Should().BeNull();
        otherPerson.ReadOn.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAnOwnerTypeThatCannotBeFollowed()
    {
        // Act
        var result = await CreateHandler().Handle(new MarkRecordNotificationsReadCommand("test.mark-record-read.unknown", Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is not a kind of record that can be followed");
    }

    [Fact]
    public async Task Handle_ShouldBeForbidden_ForACallerWithNoEmployee()
    {
        // Arrange
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var act = () => CreateHandler().Handle(new MarkRecordNotificationsReadCommand(GadgetType, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    private Notification Seed(Guid ownerId, Guid recipientId)
    {
        var notification = new NotificationFaker()
            .WithRecipientEmployeeId(recipientId)
            .WithOwner(GadgetType, ownerId)
            .Generate();
        _dbContext.AddNotification(notification);
        return notification;
    }
}
