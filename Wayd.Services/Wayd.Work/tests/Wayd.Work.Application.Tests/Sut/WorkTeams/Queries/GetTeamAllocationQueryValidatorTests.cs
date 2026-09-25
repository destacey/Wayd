using FluentAssertions;
using NodaTime;
using Wayd.Work.Application.WorkTeams.Allocation;
using Wayd.Work.Application.WorkTeams.Queries;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkTeams.Queries;

public sealed class GetTeamAllocationQueryValidatorTests
{
    private static readonly AllocationOptions Options = new(
        AllocationDimension.Portfolio, AllocationMeasure.Count, UnestimatedHandling.Exclude, ThemeCounting.SplitEvenly);

    private readonly GetTeamAllocationQueryValidator _validator = new();

    private static GetTeamAllocationQuery Query(LocalDate from, LocalDate to) => new(Guid.NewGuid(), from, to, Options);

    [Fact]
    public void Validate_ALeapYearInclusive_IsValid()
    {
        // Arrange
        var query = Query(new LocalDate(2028, 1, 1), new LocalDate(2028, 12, 31));

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MoreThan366Days_IsInvalid()
    {
        // Arrange
        var query = Query(new LocalDate(2028, 1, 1), new LocalDate(2029, 1, 1));

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "The date range must be at most 366 days.");
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
