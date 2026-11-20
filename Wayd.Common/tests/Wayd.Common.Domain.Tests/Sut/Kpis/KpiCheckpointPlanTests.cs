using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Domain.Tests.Sut.Kpis;

public sealed class KpiCheckpointPlanTests
{
    private static readonly KpiCheckpointValues Q1 = new(new LocalDate(2026, 3, 31), "Q1", 40, 30);
    private static readonly KpiCheckpointValues Q2 = new(new LocalDate(2026, 6, 30), "Q2", 60, null);

    [Theory]
    [InlineData(2026, 1, 15, "Q1")]
    [InlineData(2026, 3, 31, "Q1")]
    [InlineData(2026, 4, 1, "Q2")]
    [InlineData(2026, 6, 30, "Q2")]
    public void For_ShouldBeTheFirstCheckpointOnOrAfterThePeriodEnd(int y, int m, int d, string label)
    {
        // Act
        var checkpoint = KpiCheckpointPlan.For([Q2, Q1], new LocalDate(y, m, d));

        // Assert
        checkpoint!.Label.Should().Be(label);
    }

    [Fact]
    public void For_ShouldBeNull_PastTheLastCheckpoint()
    {
        // Act
        var checkpoint = KpiCheckpointPlan.For([Q1, Q2], new LocalDate(2026, 7, 1));

        // Assert
        checkpoint.Should().BeNull();
    }

    [Fact]
    public void Validate_ShouldAcceptASeasonalPlan_ThatFallsBack()
    {
        // Arrange
        var dip = new KpiCheckpointValues(new LocalDate(2026, 9, 30), "Q3", 35, 25);

        // Act
        var error = KpiCheckpointPlan.Validate([Q1, Q2, dip], KpiTargetDirection.Increase);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void Validate_ShouldAcceptAnEmptyPlan()
    {
        // Act
        var error = KpiCheckpointPlan.Validate([], KpiTargetDirection.Increase);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void Validate_ShouldFail_ForTwoCheckpointsOnOneDay()
    {
        // Act
        var error = KpiCheckpointPlan.Validate([Q1, Q1 with { Label = "End of Q1" }], KpiTargetDirection.Increase);

        // Assert
        error.Should().Be(KpiErrors.CheckpointDateTaken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_ShouldFail_WithoutALabel(string label)
    {
        // Act
        var error = KpiCheckpointPlan.Validate([Q1 with { Label = label }], KpiTargetDirection.Increase);

        // Assert
        error.Should().Be(KpiErrors.CheckpointLabelRequired);
    }

    [Fact]
    public void Validate_ShouldFail_ForALabelTooLong()
    {
        // Act
        var error = KpiCheckpointPlan.Validate([Q1 with { Label = new string('x', KpiCheckpointPlan.LabelMaxLength + 1) }], KpiTargetDirection.Increase);

        // Assert
        error.Should().Be(KpiErrors.CheckpointLabelTooLong);
    }

    [Theory]
    [InlineData(double.NaN, null)]
    [InlineData(double.PositiveInfinity, null)]
    [InlineData(40d, double.NegativeInfinity)]
    public void Validate_ShouldFail_ForAValueThatIsNotFinite(double planned, double? atRisk)
    {
        // Act
        var error = KpiCheckpointPlan.Validate([Q1 with { PlannedValue = planned, AtRiskValue = atRisk }], KpiTargetDirection.Increase);

        // Assert
        error.Should().Contain("finite");
    }

    [Theory]
    [InlineData(KpiTargetDirection.Increase, 40, 40, KpiErrors.CheckpointAtRiskNotShortIncrease)]
    [InlineData(KpiTargetDirection.Increase, 40, 45, KpiErrors.CheckpointAtRiskNotShortIncrease)]
    [InlineData(KpiTargetDirection.Decrease, 40, 40, KpiErrors.CheckpointAtRiskNotShortDecrease)]
    [InlineData(KpiTargetDirection.Decrease, 40, 35, KpiErrors.CheckpointAtRiskNotShortDecrease)]
    public void Validate_ShouldFail_WhenAnAtRiskValueIsNotShortOfThePlannedValue(KpiTargetDirection direction, double planned, double atRisk, string expected)
    {
        // Act
        var error = KpiCheckpointPlan.Validate([Q1 with { PlannedValue = planned, AtRiskValue = atRisk }], direction);

        // Assert
        error.Should().Be(expected);
    }

    [Fact]
    public void Validate_ShouldFail_ForMoreCheckpointsThanAPlanHolds()
    {
        // Arrange
        var start = new LocalDate(2026, 1, 1);
        var plan = Enumerable.Range(0, KpiCheckpointPlan.MaxCheckpoints + 1)
            .Select(i => new KpiCheckpointValues(start.PlusDays(i), $"D{i}", i, null))
            .ToList();

        // Act
        var error = KpiCheckpointPlan.Validate(plan, KpiTargetDirection.Increase);

        // Assert
        error.Should().Be(KpiErrors.TooManyCheckpoints);
    }

    [Fact]
    public void Normalize_ShouldTrimLabels_AndOrderByDate()
    {
        // Act
        var plan = KpiCheckpointPlan.Normalize([Q2, Q1 with { Label = " Q1 " }]);

        // Assert
        plan.Should().Equal(Q1, Q2);
    }
}
