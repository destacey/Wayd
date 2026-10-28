using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Notifications.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Notifications;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Notifications.Commands;

public sealed class MuteReasonCommandHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 25, 9, 30);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Now));
    private readonly Guid _employeeId = Guid.NewGuid();

    public MuteReasonCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private MuteReasonCommandHandler CreateHandler() => new(new FakeMuteStore(_dbContext), _currentPrincipal.Object, _dateTimeProvider);

    [Fact]
    public async Task Handle_ShouldMuteTheReasonForTheCaller()
    {
        // Act
        var result = await CreateHandler().Handle(new MuteReasonCommand(NotificationReason.Role), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var mute = _dbContext.ReasonMutes.Should().ContainSingle().Subject;
        mute.EmployeeId.Should().Be(_employeeId);
        mute.Reason.Should().Be(NotificationReason.Role);
        mute.MutedOn.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_ShouldKeepTheFirstMute_WhenTheReasonIsAlreadyMuted()
    {
        // Arrange
        await CreateHandler().Handle(new MuteReasonCommand(NotificationReason.Following), TestContext.Current.CancellationToken);
        _dateTimeProvider.Advance(Duration.FromDays(1));

        // Act
        var result = await CreateHandler().Handle(new MuteReasonCommand(NotificationReason.Following), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.ReasonMutes.Should().ContainSingle().Which.MutedOn.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAReasonNothingProduces()
    {
        // Act
        var result = await CreateHandler().Handle(new MuteReasonCommand(NotificationReason.Desk), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.ReasonMutes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheCallerHasNoEmployee()
    {
        // Arrange
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var act = () => CreateHandler().Handle(new MuteReasonCommand(NotificationReason.Following), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _dbContext.ReasonMutes.Should().BeEmpty();
    }

    [Theory]
    [InlineData(NotificationReason.Following, true)]
    [InlineData(NotificationReason.Role, true)]
    [InlineData(NotificationReason.Participant, true)]
    [InlineData(NotificationReason.Requester, true)]
    [InlineData(NotificationReason.Assigned, true)]
    [InlineData(NotificationReason.Desk, false)]
    [InlineData(NotificationReason.Mentioned, false)]
    [InlineData(NotificationReason.Following | NotificationReason.Role, false)]
    public void Validator_ShouldAcceptOnlyAReasonThatCanBeMuted(NotificationReason reason, bool expected)
    {
        // Act
        var result = new MuteReasonCommandValidator().Validate(new MuteReasonCommand(reason));

        // Assert
        result.IsValid.Should().Be(expected);
    }

    [Fact]
    public void Validator_ShouldNameEveryReasonThatCanBeMuted()
    {
        // Act
        var result = new MuteReasonCommandValidator().Validate(new MuteReasonCommand(NotificationReason.Desk));

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("'Reason' must be one of: Following, Requester, Participant, Assigned, Role.");
    }
}
