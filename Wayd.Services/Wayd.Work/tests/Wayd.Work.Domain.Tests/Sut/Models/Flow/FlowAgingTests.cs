using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Domain.Models.Flow;

namespace Wayd.Work.Domain.Tests.Sut.Models.Flow;

public class FlowAgingTests
{
    [Theory]
    [InlineData(6.0, 3.0, AttentionStatus.OnTrack)]
    [InlineData(6.1, 3.0, AttentionStatus.Watch)]
    [InlineData(9.0, 3.0, AttentionStatus.Watch)]
    [InlineData(9.1, 3.0, AttentionStatus.NeedsAttention)]
    public void Assess_WeighsTheOldestItemAgainstTheMedianCycleTime(double oldestDays, double medianDays, AttentionStatus expected)
    {
        // Arrange

        // Act
        var status = FlowAging.Assess(oldestDays, medianDays);

        // Assert
        status.Should().Be(expected);
    }

    [Theory]
    [InlineData(2.0, AttentionStatus.OnTrack)]
    [InlineData(2.5, AttentionStatus.Watch)]
    [InlineData(3.5, AttentionStatus.NeedsAttention)]
    public void Assess_TakesAMedianUnderADayAsADay(double oldestDays, AttentionStatus expected)
    {
        // Arrange — a team that finishes most items within hours

        // Act
        var status = FlowAging.Assess(oldestDays, 0.2);

        // Assert
        status.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, 3.0)]
    [InlineData(40.0, null)]
    public void Assess_WithNothingInProgressOrNoRecentMedian_IsOnTrack(double? oldestDays, double? medianDays)
    {
        // Arrange

        // Act
        var status = FlowAging.Assess(oldestDays, medianDays);

        // Assert
        status.Should().Be(AttentionStatus.OnTrack);
    }
}
