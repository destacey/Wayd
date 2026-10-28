using NodaTime;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Domain.Tests.Sut.Notifications;

public sealed class ReasonMuteTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 25, 9, 15);

    [Theory]
    [InlineData(NotificationReason.Following)]
    [InlineData(NotificationReason.Role)]
    public void Create_ShouldRecordWhoMutedWhichReasonAndWhen(NotificationReason reason)
    {
        // Arrange
        var employeeId = Guid.NewGuid();

        // Act
        var result = ReasonMute.Create(employeeId, reason, Now);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.EmployeeId.Should().Be(employeeId);
        result.Value.Reason.Should().Be(reason);
        result.Value.MutedOn.Should().Be(Now);
    }

    [Theory]
    [InlineData(NotificationReason.Requester)]
    [InlineData(NotificationReason.Assigned)]
    [InlineData(NotificationReason.Mentioned)]
    [InlineData(NotificationReason.Following | NotificationReason.Role)]
    [InlineData((NotificationReason)0)]
    [InlineData(NotificationReason.Participant)]
    [InlineData(NotificationReason.Desk)]
    [InlineData((NotificationReason)128)]
    public void Create_ShouldFail_ForAnythingButOneProducedReason(NotificationReason reason)
    {
        // Act
        var result = ReasonMute.Create(Guid.NewGuid(), reason, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is not a reason that can be muted");
    }

    [Fact]
    public void Create_ShouldThrow_WhenTheEmployeeIdIsEmpty()
    {
        // Act
        Action act = () => ReasonMute.Create(Guid.Empty, NotificationReason.Following, Now);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("employeeId");
    }
}
