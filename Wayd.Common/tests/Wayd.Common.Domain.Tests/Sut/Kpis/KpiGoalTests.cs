using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Domain.Tests.Sut.Kpis;

public sealed class KpiGoalTests
{
    private static KpiGoal Goal(
        KpiTargetDirection direction,
        double? baseline,
        KpiTarget target,
        KpiAtRiskThreshold? atRisk = null,
        KpiPeriod? window = null) =>
        new(direction, new KpiBaseline(baseline, window), target, atRisk);

    [Theory]
    [InlineData(KpiTargetDirection.Increase, 20d, 25d, 25d)]
    [InlineData(KpiTargetDirection.Decrease, 20d, 25d, 15d)]
    [InlineData(KpiTargetDirection.Decrease, -20d, 25d, -25d)]
    [InlineData(KpiTargetDirection.Increase, -20d, 25d, -15d)]
    public void TargetValue_ShouldApplyARelativeChangeToTheBaselineMagnitudeInTheKpiDirection(
        KpiTargetDirection direction, double baseline, double percent, double expected)
    {
        // Arrange
        var goal = Goal(direction, baseline, KpiTarget.RelativeChange(percent));

        // Act
        var target = goal.TargetValue;

        // Assert
        target.Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void TargetValue_ShouldBeNull_ForARelativeTargetWithNoBaseline()
    {
        // Arrange
        var goal = Goal(KpiTargetDirection.Increase, null, KpiTarget.RelativeChange(10));

        // Act
        var target = goal.TargetValue;

        // Assert
        target.Should().BeNull();
    }

    [Fact]
    public void AtRiskValue_ShouldSitThatFarFromBaselineToTarget_WhenStatedAsProgress()
    {
        // Arrange
        var goal = Goal(KpiTargetDirection.Decrease, 30, KpiTarget.Absolute(10), KpiAtRiskThreshold.ProgressToTarget(75));

        // Act
        var atRisk = goal.AtRiskValue;

        // Assert
        atRisk.Should().BeApproximately(15, 1e-9);
    }

    [Fact]
    public void Validate_ShouldAcceptAnAbsoluteTargetWithNoBaseline()
    {
        // Arrange
        var goal = Goal(KpiTargetDirection.Increase, null, KpiTarget.Absolute(20), KpiAtRiskThreshold.Absolute(15));

        // Act
        var error = goal.Validate();

        // Assert
        error.Should().BeNull();
    }

    public static TheoryData<KpiGoal, string> InvalidGoals => new()
    {
        { Goal(KpiTargetDirection.Increase, null, KpiTarget.RelativeChange(10)), KpiErrors.BaselineRequired },
        { Goal(KpiTargetDirection.Increase, 0, KpiTarget.RelativeChange(10)), KpiErrors.RelativeChangeFromZero },
        { Goal(KpiTargetDirection.Increase, 10, KpiTarget.RelativeChange(0)), KpiErrors.RelativeChangeMustBePositive },
        { Goal(KpiTargetDirection.Decrease, 10, KpiTarget.RelativeChange(-5)), KpiErrors.RelativeChangeMustBePositive },
        { Goal(KpiTargetDirection.Increase, 20, KpiTarget.Absolute(20)), KpiErrors.BaselineMeetsTargetIncrease },
        { Goal(KpiTargetDirection.Decrease, 20, KpiTarget.Absolute(20)), KpiErrors.BaselineMeetsTargetDecrease },
        { Goal(KpiTargetDirection.Increase, double.NaN, KpiTarget.Absolute(20)), KpiErrors.NotAFiniteNumber("baseline") },
        { Goal(KpiTargetDirection.Increase, null, KpiTarget.Absolute(double.PositiveInfinity)), KpiErrors.NotAFiniteNumber("target") },
        { Goal(KpiTargetDirection.Increase, null, KpiTarget.Absolute(20), window: new KpiPeriod(new LocalDate(2026, 1, 1), new LocalDate(2026, 3, 31))), KpiErrors.BaselineWindowWithoutValue },
        { Goal(KpiTargetDirection.Increase, null, KpiTarget.Absolute(20), KpiAtRiskThreshold.ProgressToTarget(50)), KpiErrors.BaselineRequired },
        { Goal(KpiTargetDirection.Increase, 10, KpiTarget.Absolute(20), KpiAtRiskThreshold.ProgressToTarget(0)), KpiErrors.AtRiskProgressOutOfRange },
        { Goal(KpiTargetDirection.Increase, 10, KpiTarget.Absolute(20), KpiAtRiskThreshold.ProgressToTarget(100)), KpiErrors.AtRiskProgressOutOfRange },
        { Goal(KpiTargetDirection.Increase, 10, KpiTarget.Absolute(20), KpiAtRiskThreshold.Absolute(20)), KpiErrors.AtRiskNotShortOfTargetIncrease },
        { Goal(KpiTargetDirection.Decrease, 30, KpiTarget.Absolute(20), KpiAtRiskThreshold.Absolute(19)), KpiErrors.AtRiskNotShortOfTargetDecrease },
    };

    [Theory]
    [MemberData(nameof(InvalidGoals))]
    public void Validate_ShouldRejectAGoalThatCannotHold(KpiGoal goal, string expectedError)
    {
        // Arrange — the goal is the theory data

        // Act
        var error = goal.Validate();

        // Assert
        error.Should().Be(expectedError);
    }
}
