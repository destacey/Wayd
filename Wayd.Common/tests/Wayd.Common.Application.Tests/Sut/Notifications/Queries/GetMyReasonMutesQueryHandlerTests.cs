using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Notifications.Dtos;
using Wayd.Common.Application.Notifications.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Tests.Sut.Notifications.Queries;

public sealed class GetMyReasonMutesQueryHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 25, 9, 30);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public GetMyReasonMutesQueryHandlerTests()
    {
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private GetMyReasonMutesQueryHandler CreateHandler() => new(_dbContext, _currentPrincipal.Object);

    [Fact]
    public async Task Handle_ShouldListEveryMutableReason_InOrder_WithTheCallersOwnState()
    {
        // Arrange
        _dbContext.AddReasonMute(ReasonMute.Create(_employeeId, NotificationReason.Role, Now).Value);
        _dbContext.AddReasonMute(ReasonMute.Create(Guid.NewGuid(), NotificationReason.Following, Now).Value);

        // Act
        var reasons = await CreateHandler().Handle(new GetMyReasonMutesQuery(), TestContext.Current.CancellationToken);

        // Assert
        reasons.Should().Equal(
            new ReasonMuteDto { Reason = NotificationReason.Following, IsMuted = false, MutedOn = null },
            new ReasonMuteDto { Reason = NotificationReason.Requester, IsMuted = false, MutedOn = null },
            new ReasonMuteDto { Reason = NotificationReason.Participant, IsMuted = false, MutedOn = null },
            new ReasonMuteDto { Reason = NotificationReason.Assigned, IsMuted = false, MutedOn = null },
            new ReasonMuteDto { Reason = NotificationReason.Mentioned, IsMuted = false, MutedOn = null },
            new ReasonMuteDto { Reason = NotificationReason.Role, IsMuted = true, MutedOn = Now },
            new ReasonMuteDto { Reason = NotificationReason.Thread, IsMuted = false, MutedOn = null });
    }

    [Fact]
    public async Task Handle_ShouldListNothingMuted_WhenTheCallerHasNoEmployee()
    {
        // Arrange
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var reasons = await CreateHandler().Handle(new GetMyReasonMutesQuery(), TestContext.Current.CancellationToken);

        // Assert
        reasons.Select(r => r.Reason).Should().Equal(
            NotificationReason.Following,
            NotificationReason.Requester,
            NotificationReason.Participant,
            NotificationReason.Assigned,
            NotificationReason.Mentioned,
            NotificationReason.Role,
            NotificationReason.Thread);
        reasons.Should().OnlyContain(r => !r.IsMuted);
    }
}
