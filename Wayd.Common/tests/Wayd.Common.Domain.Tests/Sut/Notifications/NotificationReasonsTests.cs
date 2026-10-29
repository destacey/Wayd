using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Domain.Tests.Sut.Notifications;

public sealed class NotificationReasonsTests
{
    [Fact]
    public void All_ShouldCombineEveryDefinedReason()
    {
        // Arrange
        var defined = Enum.GetValues<NotificationReason>().Aggregate((a, b) => a | b);

        // Act
        var all = NotificationReasons.All;

        // Assert
        all.Should().Be(defined);
    }

    [Fact]
    public void Reasons_ShouldEachBeASingleBit()
    {
        // Arrange
        var values = Enum.GetValues<NotificationReason>().Select(r => (int)r);

        // Act
        var notSingleBits = values.Where(v => v <= 0 || (v & (v - 1)) != 0);

        // Assert
        notSingleBits.Should().BeEmpty();
    }

    [Theory]
    [InlineData(NotificationReason.Following, true)]
    [InlineData(NotificationReason.Following | NotificationReason.Mentioned, true)]
    [InlineData((NotificationReason)0, false)]
    [InlineData((NotificationReason)256, false)]
    public void IsValid_ShouldAcceptOnlyNonEmptySetsOfDefinedReasons(NotificationReason reasons, bool expected)
    {
        // Act
        var isValid = NotificationReasons.IsValid(reasons);

        // Assert
        isValid.Should().Be(expected);
    }

    [Theory]
    [InlineData(NotificationReason.Following, true)]
    [InlineData(NotificationReason.Role, true)]
    [InlineData(NotificationReason.Participant, true)]
    [InlineData(NotificationReason.Desk, false)]
    [InlineData(NotificationReason.Requester, true)]
    [InlineData(NotificationReason.Assigned, true)]
    [InlineData(NotificationReason.Mentioned, false)]
    [InlineData(NotificationReason.Following | NotificationReason.Role, false)]
    [InlineData((NotificationReason)0, false)]
    public void IsMutable_ShouldAcceptOnlyASingleProducedReason(NotificationReason reason, bool expected)
    {
        // Act
        var isMutable = NotificationReasons.IsMutable(reason);

        // Assert
        isMutable.Should().Be(expected);
    }
}
