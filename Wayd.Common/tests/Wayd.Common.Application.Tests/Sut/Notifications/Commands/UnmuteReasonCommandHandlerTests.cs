using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Notifications.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Tests.Sut.Notifications.Commands;

public sealed class UnmuteReasonCommandHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 25, 9, 30);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public UnmuteReasonCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private UnmuteReasonCommandHandler CreateHandler() => new(new FakeMuteStore(_dbContext), _currentPrincipal.Object);

    [Fact]
    public async Task Handle_ShouldRemoveOnlyTheCallersMuteOfThatReason()
    {
        // Arrange
        var otherPerson = Guid.NewGuid();
        _dbContext.AddReasonMute(ReasonMute.Create(_employeeId, NotificationReason.Following, Now).Value);
        _dbContext.AddReasonMute(ReasonMute.Create(_employeeId, NotificationReason.Role, Now).Value);
        _dbContext.AddReasonMute(ReasonMute.Create(otherPerson, NotificationReason.Following, Now).Value);

        // Act
        var result = await CreateHandler().Handle(new UnmuteReasonCommand(NotificationReason.Following), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.ReasonMutes.AsEnumerable().Select(m => (m.EmployeeId, m.Reason)).Should().BeEquivalentTo([
            (_employeeId, NotificationReason.Role),
            (otherPerson, NotificationReason.Following),
        ]);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenTheReasonWasNotMuted()
    {
        // Act
        var result = await CreateHandler().Handle(new UnmuteReasonCommand(NotificationReason.Role), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(NotificationReason.Following, true)]
    [InlineData(NotificationReason.Mentioned, true)]
    [InlineData(NotificationReason.Following | NotificationReason.Role, true)]
    [InlineData((NotificationReason)128, false)]
    [InlineData((NotificationReason)999, false)]
    public void Validator_ShouldAcceptOnlyDefinedReasons(NotificationReason reason, bool expected)
    {
        // Act
        var result = new UnmuteReasonCommandValidator().Validate(new UnmuteReasonCommand(reason));

        // Assert
        result.IsValid.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheCallerHasNoEmployee()
    {
        // Arrange
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var act = () => CreateHandler().Handle(new UnmuteReasonCommand(NotificationReason.Following), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
