using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Notifications.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Application.Tests.Sut.Notifications.Queries;

public sealed class GetMyUnreadNotificationCountQueryHandlerTests
{
    private const string GadgetType = "test.unread-count.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Test Unread-Count Gadget");
    private static readonly Instant Start = Instant.FromUtc(2026, 10, 1, 9, 0);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeRecordReadRule _gadgets = new(GadgetType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public GetMyUnreadNotificationCountQueryHandlerTests()
    {
        FollowOwners.Register(Gadget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private GetMyUnreadNotificationCountQueryHandler CreateHandler() => new(_dbContext, [_gadgets], _currentPrincipal.Object);

    [Fact]
    public async Task Handle_ShouldCountRecordsWithSomethingUnread_NotNotifications()
    {
        // Arrange
        var busy = _gadgets.Add("G-1", "Busy");
        var allRead = _gadgets.Add("G-2", "All read");
        var hidden = _gadgets.Add("G-3", "Hidden");
        Seed(busy.Id);
        Seed(busy.Id);
        Seed(busy.Id, read: true);
        Seed(allRead.Id, read: true);
        Seed(hidden.Id);
        _gadgets.Hide(hidden.Id);

        // Act
        var result = await CreateHandler().Handle(new GetMyUnreadNotificationCountQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Count.Should().Be(1);
        result.IsCapped.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldCapTheCount()
    {
        // Arrange
        for (var i = 0; i < 100; i++)
        {
            Seed(_gadgets.Add($"G-{i}", $"Gadget {i}").Id);
        }

        // Act
        var result = await CreateHandler().Handle(new GetMyUnreadNotificationCountQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Count.Should().Be(99);
        result.IsCapped.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldCountExactly_AtTheCap()
    {
        // Arrange
        for (var i = 0; i < 99; i++)
        {
            Seed(_gadgets.Add($"G-{i}", $"Gadget {i}").Id);
        }

        // Act
        var result = await CreateHandler().Handle(new GetMyUnreadNotificationCountQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Count.Should().Be(99);
        result.IsCapped.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldCountNothing_ForACallerWithNoEmployee()
    {
        // Arrange
        Seed(_gadgets.Add("G-1", "Sprocket").Id);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(new GetMyUnreadNotificationCountQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Count.Should().Be(0);
        result.IsCapped.Should().BeFalse();
    }

    private void Seed(Guid ownerId, bool read = false)
    {
        _dbContext.AddNotification(new NotificationFaker()
            .WithRecipientEmployeeId(_employeeId)
            .WithOwner(GadgetType, ownerId)
            .WithOccurredOn(Start)
            .WithReadOn(read ? Start : null)
            .Generate());
    }
}
