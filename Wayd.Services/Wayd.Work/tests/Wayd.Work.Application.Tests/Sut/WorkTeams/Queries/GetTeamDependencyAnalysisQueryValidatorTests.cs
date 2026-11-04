using FluentAssertions;
using NodaTime;
using Wayd.Work.Application.WorkTeams.Queries;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkTeams.Queries;

public sealed class GetTeamDependencyAnalysisQueryValidatorTests
{
    private readonly GetTeamDependencyAnalysisQueryValidator _validator = new();

    private static GetTeamDependencyAnalysisQuery Query(LocalDate from, LocalDate to) => new(Guid.NewGuid(), from, to);

    [Fact]
    public void Validate_TwoYearsWithALeapDayInclusive_IsValid()
    {
        // Arrange
        var query = Query(new LocalDate(2027, 1, 1), new LocalDate(2028, 12, 31));

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MoreThan731Days_IsInvalid()
    {
        // Arrange
        var query = Query(new LocalDate(2027, 1, 1), new LocalDate(2029, 1, 1));

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "The date range must be at most 731 days.");
    }

    [Fact]
    public void Validate_EndBeforeStart_IsInvalid()
    {
        // Arrange
        var query = Query(new LocalDate(2026, 9, 2), new LocalDate(2026, 9, 1));

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "The end date must not be before the start date.");
    }
}
