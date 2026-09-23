using FluentAssertions;
using Wayd.Work.Application.WorkTeams.Queries;
using Wayd.Work.Domain.Models.BacklogHealth;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkTeams.Queries;

public sealed class GetTeamBacklogHealthQueryValidatorTests
{
    private readonly GetTeamBacklogHealthQueryValidator _validator = new();

    private static GetTeamBacklogHealthQuery Query(BacklogHealthThresholds thresholds, int lookbackDays = 90) =>
        new(Guid.NewGuid(), thresholds, lookbackDays);

    [Fact]
    public void Validate_Defaults_AreValid()
    {
        // Act
        var result = _validator.Validate(Query(BacklogHealthThresholds.Default));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(13)]
    [InlineData(366)]
    public void Validate_LookbackOutOfRange_IsInvalid(int lookbackDays)
    {
        // Act
        var result = _validator.Validate(Query(BacklogHealthThresholds.Default, lookbackDays));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(GetTeamBacklogHealthQuery.LookbackDays));
    }

    public static TheoryData<string, BacklogHealthThresholds> InvalidThresholds => new()
    {
        { nameof(BacklogHealthThresholds.StaleDays), BacklogHealthThresholds.Default with { StaleDays = 0 } },
        { nameof(BacklogHealthThresholds.AgingWipPercentile), BacklogHealthThresholds.Default with { AgingWipPercentile = 101 } },
        { nameof(BacklogHealthThresholds.ReadinessWindowWeeks), BacklogHealthThresholds.Default with { ReadinessWindowWeeks = 0 } },
        { nameof(BacklogHealthThresholds.UnhealthyPercent), BacklogHealthThresholds.Default with { AtRiskPercent = 30, UnhealthyPercent = 20 } },
        { nameof(BacklogHealthThresholds.RunwayAtRiskWeeks), BacklogHealthThresholds.Default with { RunwayAtRiskWeeks = 1, RunwayUnhealthyWeeks = 2 } },
        { nameof(BacklogHealthThresholds.RunwayTooLongWeeks), BacklogHealthThresholds.Default with { RunwayTooLongWeeks = 4 } },
        { nameof(BacklogHealthThresholds.NetFlowUnhealthy), BacklogHealthThresholds.Default with { NetFlowUnhealthy = 1.1 } },
        { nameof(BacklogHealthThresholds.WipLoadUnhealthy), BacklogHealthThresholds.Default with { WipLoadUnhealthy = 1 } },
    };

    [Theory]
    [MemberData(nameof(InvalidThresholds))]
    public void Validate_InvalidThreshold_NamesIt(string property, BacklogHealthThresholds thresholds)
    {
        // Act
        var result = _validator.Validate(Query(thresholds));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == $"{nameof(GetTeamBacklogHealthQuery.Thresholds)}.{property}");
    }
}
